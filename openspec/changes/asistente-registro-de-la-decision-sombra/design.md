## Context

`EnrutadorDeDominio` corre en modo sombra desde el change [`asistente-enrutador-de-dominio`](../asistente-enrutador-de-dominio/proposal.md): decide y no ejecuta. No hay a dónde enrutar hasta que existan los edges hacia los `Contracts` (ARS-46) y los adaptadores de respuesta, y el pedido de aprobación de esos edges se fundamenta con un número o no se fundamenta.

**Estado verificado hoy**, y es más pobre de lo que el spec anterior dio por hecho:

| Pieza                             | Qué tiene                                                        |
| --------------------------------- | ---------------------------------------------------------------- |
| `TurnoParaRegistrar`              | Ningún campo para la intención                                   |
| `asistente.registro_operativo`    | Ninguna columna                                                  |
| `CapaConversacional.CarrilDe`     | Deriva el carril del **estado** del turno, no de la ruta elegida |
| `EnrutadorDeDominio.DecidirAsync` | Un `LogInformation` — la única traza que existe                  |

El spec del change anterior declara que «queda registrado qué intención la habría capturado». Se cumplió contra el log, no contra el registro. Fundamentar ARS-46 hoy exige minar logs que rotan y que no se agregan.

**Restricciones que enmarcan el diseño:**

- §3.4 de [`asistente-conversacional-definicion.md`](../../../docs/product/designs/asistente-conversacional-definicion.md) desvincula los dos registros a propósito, y TD-012 declara el canal residual que queda (el orden físico de las filas correlaciona con el orden de inserción).
- El DDL del asistente **no puede alterar nada**, ni siquiera lo propio: hay un guard de arquitectura que lo sostiene.
- El módulo no lleva historial de migraciones; sus scripts son idempotentes por construcción y una base ya creada se vuelve a aprovisionar.

## Goals / Non-Goals

**Goals:**

- Que la decisión del enrutador sombra quede en un registro consultable, no en un log.
- Tomar **explícitamente** la decisión de privacidad sobre el registro analítico, con el motivo escrito.
- Producir el número que ARS-46 necesita **hoy**, sin esperar tráfico real y sin gastar una llamada al modelo.
- Que `carril` siga significando lo mismo antes y después de que el carril determinista se conecte.

**Non-Goals:**

- Conectar el carril determinista. Sigue haciendo falta ARS-46 y los adaptadores.
- Los edges hacia `Modules.<X>.Contracts`. Este change no agrega ninguna referencia de proyecto.
- Cambiar el contrato HTTP. La intención sombra no viaja al cliente.
- Afirmar que la intención capturada es la **correcta** para la pregunta. Ver D7.

## Decisions

### D1 — Columna propia en el registro operativo, no un valor nuevo de `carril`

`intencion_sombra text NULL` en `asistente.registro_operativo`, con el nombre de la intención del catálogo o nulo.

**No es un valor más de `carril`, y la distinción es la que sostiene todo el resto.** `carril` dice por dónde se resolvió el turno **de verdad**; la intención sombra dice por dónde se habría resuelto. Son dos hechos distintos sobre el mismo turno y ninguno implica al otro: un turno capturado por el catálogo se resuelve igual por `Sql`, y también puede terminar en `Aclaracion` o en `Fallo` sin dejar de haber sido capturado.

Meterla en `carril` haría que la serie de «cuántos turnos resolvió SQL» cambiara de significado sin que ninguna consulta se enterara — que es la peor forma de romper una métrica, porque sigue devolviendo un número.

**Nulo es el caso normal, no un dato faltante.** Un catálogo de cinco intenciones no captura la mayoría de las preguntas y no pretende hacerlo. Por eso la columna es anulable y va sin `DEFAULT`: un default convertiría «no capturó» en un valor inventado.

### D2 — La intención NO va al registro analítico

**La decisión de privacidad del change, tomada acá y con el motivo escrito.**

El registro analítico existe para responder «qué se pregunta» sin poder responder «quién preguntó qué». Su desvinculación no se sostiene con una política sino con la **ausencia de columnas para cruzar**: no tiene actor, no tiene hora —solo `dia`, de tipo `date`, para que el motor trunque aunque el código mande un timestamp— y su clave es un UUID aleatorio para que el orden de inserción no quede escrito.

Contra la intuición de que «una categórica cerrada de cinco o seis valores es poca entropía», tres razones para no ponerla:

**1. La reidentificación vive en las colas, no en el promedio.** Los ~2,6 bits de una categórica de seis valores son un promedio, y el promedio es la magnitud equivocada. Por construcción los valores no son equiprobables: el banco negativo del change anterior prueba que el catálogo no captura **ninguna** de las preguntas de los datasets de evaluación, así que sobre tráfico real las capturas van a ser la minoría y cada intención concreta va a ser rara. Una fila analítica con `intencion_sombra = plantel-de-una-materia` en un `dia` dado, cruzada con las filas operativas de ese día, deja el conjunto anónimo en quien haya preguntado por un plantel. Con ~30 usuarios eso no es un conjunto, es un nombre.

**2. Le da valor al canal residual que TD-012 declara.** Hoy el orden físico de las filas correlaciona con el orden de inserción, y la deuda se aceptó porque explotarlo exige emparejar dos listas sin nada que las distinga. Un valor raro en el analítico deja de ser una fila más: es un **selector** que reduce el emparejamiento a un puñado de candidatos. La deuda se aceptó con el analítico que existe hoy; agregarle una dimensión rara cambia la evaluación de riesgo que la sostiene, sin haberla rehecho.

**3. No compra el número.** La cobertura sobre tráfico real es la proporción de filas del operativo con `intencion_sombra` no nula, y los dos registros escriben **exactamente una fila por turno** cada uno: numerador y denominador ya están en el operativo, solos. Lo único que el analítico agregaría es el **texto de la pregunta** al lado de la intención — es decir, la mitad «cuántas veces se equivoca».

**Y esa mitad sale gratis por otro lado.** El resolutor es determinista y cuesta cero llamadas al modelo, así que se lo corre sobre los datasets de evaluación (D5) y se obtiene el mismo diagnóstico sobre un corpus que escribió otra tarea con otro objetivo, sin un solo dato personal y sin esperar tráfico. **La vía cara en privacidad y la vía gratis miden lo mismo, y solo una de las dos cuesta.** Elegir la que cuesta habría que justificarlo, y no hay con qué.

**Alternativa considerada y descartada**: ponerla en el analítico y quitarle `categoria`, para no sumar dimensiones netas. Se descartó porque `categoria` es lo que hace legible al analítico —es la columna por la que se agrupa para saber qué se pregunta— y cambiar una dimensión útil por una que se puede obtener gratis en otro lado es un mal canje.

**Cuándo revisar esta decisión**: si aparece el marco institucional de datos personales que §8 de la definición marca como pendiente, la revisión correcta es hacia **más** restricción, no hacia menos.

### D3 — `carril` no cambia de significado, y la columna sobrevive al cutover

`carril` es la ruta **real** del turno. Hoy el carril determinista no resuelve ninguno, así que ninguna fila lo lleva; cuando ARS-46 se apruebe y el enrutador deje la sombra, `carril` va a ganar el valor que corresponda — y eso es trabajo de ARS-46, no de este change.

**La columna `intencion_sombra` no se borra en el cutover: pasa a registrar la intención que sí enrutó.** Borrarla partiría la serie justo en el momento en que se vuelve interesante: la pregunta «qué proporción del tráfico captura el catálogo» tiene que poder responderse **cruzando** el antes y el después, porque comparar los dos es la única forma de saber si la sombra predijo bien.

Corolario deliberado: después del cutover el nombre de la columna miente un poco. Se acepta, y no se renombra. Un `RENAME` es un `ALTER` —que el guard prohíbe— y además rompería toda consulta escrita contra la serie que la columna existe para preservar. Lo que se actualiza es su `COMMENT ON COLUMN`.

### D4 — La columna entra en el `CREATE TABLE` de `002`, no en un `003` con `ALTER`

**Esto se aparta de la forma que el ticket sugería** (un `database/asistente/003_*.sql` nuevo), y la evidencia es concluyente:

- `ArquitecturaAsistenteTests.El_DDL_del_asistente_no_borra_ni_altera_nada_ni_siquiera_lo_propio` corre `\bDROP\s+\w+\b|\bALTER\s+TABLE\b` sobre **todos** los `database/asistente/*.sql`. El fixture positivo del guard es, literalmente, `009_alter.sql` → `ALTER TABLE asistente.registro_analitico ADD COLUMN x text;`.
- Hay **dos precedentes exactos**: `proveedor` (commit `4a00edb`) y `tokens_de_cache` (commit `f53c9ab`) se agregaron editando el `CREATE TABLE` de `002`. El cuerpo de `4a00edb` lo dice con todas las letras: «La columna entra en el CREATE TABLE y no por un ALTER. El módulo no lleva historial de migraciones y hay un guard de arquitectura que lo sostiene: una base ya creada no se migra, se vuelve a aprovisionar».
- El propio `002` lo tiene escrito al lado de la columna anterior: «Va sin DEFAULT y sin un ALTER que la agregue a una tabla ya creada, porque este archivo no puede alterar nada … los ambientes de este sistema son efímeros y esa es la vía prevista».

Un `003` que altere o falla el guard, o hay que evadirlo con SQL dinámico. **Evadir un guard para satisfacer una convención de nombres de archivo es el canje al revés**: se pierde la propiedad que el guard protege —que el esquema no dependa del orden de aplicación— a cambio de nada.

**Alternativa considerada**: un `003` que cree una **tabla nueva** para las decisiones sombra, sin alterar nada. Se descartó por dos motivos. Sería un **tercer** registro, con su propia obligación de retención y purga. Y o lleva una clave para volver al operativo —exactamente el cruce que §3.4 existe para impedir— o no la lleva, y entonces la cobertura hay que contarla por día entre dos tablas, para no ganar nada.

### D5 — La tabla dorada offline, y por qué subsume al banco negativo

Un test corre el enrutador sobre las preguntas de `capacidad.json` (24 ítems) y `robustez.json` (paráfrasis) y compara el mapeo `id → intención capturada o nulo` contra una **tabla dorada** versionada. Cualquier diferencia falla en rojo nombrando qué ítem se movió y en qué dirección.

**Hoy la tabla es enteramente nula**, porque el banco negativo del change anterior ya prueba que el catálogo no captura ninguna de esas preguntas. Eso no la hace redundante: la hace la **línea de base**. El número que ARS-46 pide arranca en «0 de N sobre el corpus de evaluación», y ese cero es un dato, no un vacío.

**Reemplaza al assert booleano `Ninguna_pregunta_del_dataset_se_captura` en vez de convivir con él.** Los dos fallan en las mismas situaciones, pero ante un rojo piden arreglos opuestos:

|                                                                 | Banco negativo (booleano)                                                | Tabla dorada                                           |
| --------------------------------------------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------ |
| Qué afirma                                                      | «no captura ninguna»                                                     | «captura exactamente esto»                             |
| Qué produce                                                     | un veredicto                                                             | un número                                              |
| Ante una intención nueva legítima que **debe** capturar un ítem | solo se satisface debilitando la intención o sacando el ítem del dataset | se actualiza la entrada, y el diff muestra la decisión |

Mantener los dos dejaría al booleano forzando el arreglo equivocado. La tabla dorada hereda su guard: **una tabla vacía daría verde para siempre**, así que el test verifica también que cubra todos los ítems de los dos datasets.

**El archivo, y no una tabla embebida en el test**: el diff del archivo es el artefacto que el pedido de ARS-46 cita. Hereda la disciplina que [`backend/eval/lineas-de-base/README.md`](../../../backend/eval/lineas-de-base/README.md) ya documenta — lock por ítem y no umbral, regeneración **nunca automática**, y el diff como lo que se revisa. Lo que no hereda es la ubicación: aquellos son del gate de regresión, que corre contra el modelo real y cuesta dinero; este corre en CI y cuesta cero, así que vive con el test que lo posee, en la suite de integración.

### D6 — La decisión llega al registro por un portador con alcance de turno

La decisión se toma dentro de `ResolverAsync` (paso 5) y el registro se escribe en `ResponderAsync`, **incluidas sus dos ramas de `catch`** —presupuesto vencido y excepción no prevista—. Un valor de retorno no sirve: en esas ramas no hay `ResultadoDelTurno` del pipeline.

La forma ya está establecida en el módulo: `ContadorDeLlamadasDelTurno` es un objeto `AddScoped` que se muta durante el turno y que `RegistrarAsync` lee. La decisión sombra viaja igual, y hereda la misma semántica útil: si el turno se cayó **después** del paso 5, la fila conserva la decisión que alcanzó a tomarse — exactamente como conserva las llamadas que alcanzó a emitir.

**Alternativas descartadas:**

- **Ponerla en `ResultadoDelTurno`.** Es el valor de retorno del carril y llega al controller. Telemetría que viaja en el objeto de la respuesta está a un mapeo de distancia de aparecer en el cuerpo HTTP.
- **Que `EnrutadorDeDominio` recuerde su última decisión.** Funcionaría —ya es `AddScoped`—, pero el change anterior pagó con un test de dependencias la propiedad «decide y no ejecuta», y agregarle estado mutable la vuelve más difícil de leer.

### D7 — Qué mide la tabla dorada, y qué NO afirma

Mide dos cosas, las dos sin tráfico real y sin llamadas al modelo:

- **Cobertura**: cuántos ítems del corpus captura el catálogo.
- **Consistencia de fraseo**: cada ítem de `robustez.json` declara `origen`, apuntando al ítem de `capacidad.json` del que es paráfrasis. Es la **misma pregunta dicha de otra manera**, así que el enrutador tiene que decidir lo mismo para las dos. Una divergencia es un error del enrutador —capturó el fraseo canónico y no la paráfrasis, o al revés—, y es la mitad «cuántas veces se equivoca» que se puede medir hoy.

**Lo que NO afirma**: que la intención capturada sea la **correcta** para la pregunta. Los datasets llevan `sql_referencia`, no una intención esperada, y escribirla sería redactar la clave de respuestas de lo que se está midiendo. La corrección de la intención elegida se sigue juzgando como siempre: un humano leyendo un diff rojo. El test lo dice en su mensaje de fallo, para que nadie lea la tabla como un veredicto de corrección.

### D8 — El README documenta la consulta, no la describe

El README del módulo lleva la consulta ejecutable que produce el número de cobertura sobre tráfico real desde el registro operativo, junto a la advertencia de que `carril` e `intencion_sombra` responden preguntas distintas. Una métrica descrita en prosa se reconstruye distinta cada vez que alguien la necesita; escrita, se copia y pega.

## Risks / Trade-offs

- **La tabla dorada se puede actualizar para que el rojo se vuelva verde** → El test nombra el ítem y la dirección del cambio (`nulo → intención` es posible laxitud; `intención → nulo` es una captura perdida) y dice cuál es la lectura de cada una. La regeneración es a mano y el diff es lo que se revisa, igual que en las líneas de base del gate.
- **Alguien construye un tablero leyendo `intencion_sombra` como si fuera el carril** → El `COMMENT ON COLUMN` lo dice, el README lo dice al lado de la consulta, y los nombres de las dos columnas no se parecen.
- **La columna solo existe en bases aprovisionadas después de este change** → Es la vía prevista y documentada del módulo: ambientes efímeros, sin historial de migraciones. Nada que rellenar hacia atrás: nulo en un turno anterior sería inventar una decisión que nunca se tomó.
- **La decisión de D2 se revisa si aparece el marco institucional de datos personales** → §8 de la definición ya lo tiene marcado como pendiente. La revisión correcta desde esta posición es hacia más restricción.
- **El número offline no es el número del tráfico real** → Y no pretende serlo. El corpus de evaluación se escribió para medir traducción a SQL, no para parecerse a la demanda. Da la cota que se puede tener hoy y la línea de base contra la que comparar la del tráfico cuando la haya; el pedido de ARS-46 tiene que citar las dos y decir cuál es cuál.

## Migration Plan

1. La columna entra en el `CREATE TABLE` de `002_asistente_registros.sql` (D4). Los ambientes efímeros la toman al reaprovisionar; no hay migración inversa que correr.
2. `docs/architecture/data-model.md` y el README del módulo se actualizan en el **mismo commit** (invariante #6).
3. **Rollback**: la columna es anulable y ninguna respuesta la lee. Revertir el código deja filas con un valor que nada consulta.

## Open Questions

- **Cuándo se corta la sombra.** Depende de ARS-46 y de los adaptadores, no de este change. Lo que queda escrito acá es qué pasa con la columna cuando eso ocurra (D3).
- **Si el número offline alcanza para fundamentar ARS-46.** Este change lo produce; si el equipo pide además cobertura sobre tráfico real, la consulta del README ya la responde en cuanto haya filas.
