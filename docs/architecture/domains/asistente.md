# Domain: asistente

## Propósito

Responder en español preguntas sobre datos que ya viven en la base del sistema, en
modo solo lectura y acotadas al alcance de quien pregunta. Cubre la familia de
casos de uso —cobertura de cátedra, composición del plantel— que hoy **no tiene
endpoint equivalente** en ningún módulo.

## Roles que interactúan

Todos los roles del sistema salvo `docente`, según la siembra del permiso
`asistente.consultar`:

- **Jefe de Cátedra** — pregunta por su materia
- **Coordinador de Carrera** — pregunta por su carrera
- **Secretaría Académica** — pregunta por todo el Departamento
- **Decanato** — ídem
- **Administrativo** — ídem, dentro de lo que sus permisos de dominio habilitan
- **Administrador de Sistemas** — el rol existe en la siembra, pero es `NOINHERIT`
  y no hereda permisos: en la práctica no ve nada hasta que se le asigne otro rol

El alcance no lo aplica el módulo: lo aplican las policies RLS con el actor fijado.
El asistente no tiene ninguna rama de código que decida qué puede ver quién.

## Bounded context

- **Pertenece**: la traducción de lenguaje natural a consulta, la validación de esa
  consulta, la política de abstención y la redacción de la respuesta. El catálogo
  de ejemplos pregunta-SQL.
- **No pertenece**: los datos. Todos son de otros bounded contexts —`identity` y
  `designaciones`— y el asistente los lee a través de dos roles de PostgreSQL con
  privilegios enumerados columna por columna. No hay ninguna entidad canónica acá.

## Entidades principales

Ninguna de dominio, y ningún `DbContext`. Lo único que le pertenece son sus dos
registros —`asistente.registro_operativo` y `asistente.registro_analitico`—, que son
telemetría suya y no datos del sistema: nadie más los lee, y los dos roles de solo
lectura del propio asistente tienen ese schema revocado entero.

Lee, con `GRANT SELECT` por columna contra un manifiesto deny-by-default:

| Schema          | Tablas                                                                                         |
| --------------- | ---------------------------------------------------------------------------------------------- |
| `identity`      | `carreras`, `materias`, `personas`, `users`, `roles`, `user_roles`, `permisos`, `rol_permisos` |
| `designaciones` | `cargos`, `periodos`, `pedidos`, `pedido_adjuntos`, `pedido_historial`, `designaciones`        |

Denegadas explícitamente: `designaciones.idempotencia_comandos` (su `response_body`
JSONB guarda respuestas HTTP completas con datos de personas), el schema `audit`
entero, el schema `asistente` entero (sus propios registros), y las columnas
`pedidos.snapshot`, `pedido_adjuntos.uri`, `users.azure_oid` y
`user_roles.granted_by`.

Fuente de verdad: [`database/asistente/manifiesto-privilegios.json`](../../../database/asistente/manifiesto-privilegios.json).

## API pública (contract)

`Modules.Asistente.Contracts` **nace vacío**: ningún otro módulo consume al
asistente. Queda por convención, pendiente de decidir con el equipo si se conserva
o se declara la excepción.

## Endpoints HTTP

| Método | Path                  | Rol       | Descripción  |
| ------ | --------------------- | --------- | ------------ |
| GET    | `/api/asistente/ping` | (anónimo) | Health check |

El endpoint del turno —`POST /api/asistente/consultas`— llega con la épica de
superficie de usuario, junto con el contrato de cuatro estados y la
`Idempotency-Key`. Hoy el carril es un servicio del módulo.

## El carril SQL

Dos llamadas al modelo por turno; todo lo del medio, determinista.

| Pieza                  | Qué hace                                                            | Cuesta |
| ---------------------- | ------------------------------------------------------------------- | ------ |
| `IPerfilDelActor`      | Alcance global y acceso a datos personales; valida el actor         | 0      |
| `IProveedorDeEsquema`  | Prefijo estable del prompt, derivado de los `GRANT` efectivos       | 0      |
| `ISelectorDeEjemplos`  | Ejemplos por similitud léxica, en proceso                           | 0      |
| `GeneradorDeSql`       | **Llamada 1**: temperatura 0, prefijo cacheado                      | 1      |
| `ValidadorDeSql`       | Tokeniza y rechaza funciones y palabras clave prohibidas            | 0      |
| `IEjecutorDeConsulta`  | Transacción nueva `READ ONLY`, actor transaction-local, `LIMIT n+1` | 0      |
| `PoliticaDeAbstencion` | Guard de vacío y decisión de reintento                              | 0      |
| `RedactorDeRespuesta`  | **Llamada 2**: temperatura 0,3, sin caché                           | 1      |

### Cuatro capas de defensa, independientes entre sí

1. **El rol** no tiene ningún privilegio de mutación (`42501`).
2. **La transacción** se declara `READ ONLY` (`25006`).
3. **Las policies RLS** filtran las filas según el actor fijado.
4. **El validador** rechaza la consulta antes de ejecutarla.

Las tres primeras las impone el motor. La cuarta sube el costo de un ataque; el
motor es lo que lo hace inútil. Cada capa tiene su propio test, aislada de las
otras.

Hallazgo del camino: la envoltura en subconsulta hace **estructuralmente**
imposible colar DML, porque PostgreSQL admite una CTE que modifica datos solo en el
nivel superior de la sentencia.

### La abstención

Siete casos, y uno central: RLS convierte «no tenés permiso» en cero filas, que es
la misma firma que «el literal no matcheó». Antes de gastar el reintento se
consulta si el actor es global; si no lo es, un vacío no lo gasta y la respuesta
dice «no encontré nada en tu alcance», nunca «no hay».

Un resultado vacío se resuelve **sin llamar al modelo**. Con cero filas no hay nada
que narrar, así que la distinción queda garantizada por código y no por una
instrucción del prompt.

Restricción dura: ninguna respuesta declara cuántas filas quedaron afuera. El
indicador de truncado es un booleano y no un número.

### La capa conversacional

Va **encima** del carril, no adentro. Esa separación es lo que deja intactos el
prefijo cacheado, el validador y los datasets: `CarrilSql.ResponderAsync` ya recibía
una pregunta autocontenida, y la capa es quien la calcula.

```
resolver hilo
  └─ enrutador social/meta        ← se SALTEA si hay aclaración pendiente
       └─ reconocedor de aclaración
            └─ detector de cambio de tema
                 └─ reescritor    ← única llamada al modelo de la capa; solo con historial
                      └─ detector de ambigüedad
                           └─ CARRIL SQL
```

Cada posición tiene un motivo:

- **El enrutador social se saltea con un menú abierto.** Si no, un «gracias» le
  robaría la respuesta a la aclaración y el menú quedaría colgado.
- **El reconocedor corre antes del reescritor** y le entrega la etiqueta canónica.
  Si le pasara el «2» que el usuario tipeó, el reescritor tendría que adivinar.
- **El reescritor corre antes del detector de ambigüedad.** «¿y en Análisis
  Matemático?» no contiene ninguna entidad ambigua hasta que se la reescribe.

**El cambio de tema se fuerza, no se pide.** Hay evidencia de modelos que detectan
el pivote y arrastran contexto rancio igual. Al marcarlo, al reescritor **no se le
pasa historial** — no se le pasa historial y una instrucción que diga «ignoralo».
La diferencia es que así el arrastre es imposible por construcción, y el test que lo
verifica no mira la salida del modelo sino qué se le mandó.

**La guarda del marcador anafórico** es lo que evita que «¿y en Sistemas?» se lea
como pivote: ese mensaje menciona un término del catálogo que no está activo, así
que sin la guarda rompería el caso de seguimiento más común que existe.

**La ambigüedad se resuelve con un `SELECT`, y solo con certeza.** Dos clases de
colisión: nombres de materia repetidos entre carreras, y apellidos compartidos. El
índice sale de la base. **No se extiende a la vaguedad**: preguntar tiene un costo
medido, y las aclaraciones de calidad baja son peores que no preguntar.

**El hilo guarda preguntas y nunca filas.** Guardar los resultados devolvería al
prompt, por la puerta del historial, los datos personales que el enmascarador sacó
del camino de salida. No se persiste: se pierde en cada redespliegue y eso está
aceptado.

### La frontera de salida

Los `GRANT` deciden **quién puede leer qué**, y eso lo impone el motor. El
enmascarador decide **qué sale hacia un tercero**, que es una pregunta distinta: un
actor puede tener todo el derecho a ver un teléfono en pantalla y no haber ninguna
razón para que ese teléfono llegue al proveedor del modelo.

Se interpone entre la ejecución de la SQL y la llamada de redacción. Las filas que
llegan al modelo van enmascaradas; las reales siguen viaje al llamador.

`database/asistente/manifiesto-sensibilidad.json` —hermano del de privilegios—
clasifica cada columna legible en tres categorías:

| Categoría        | Qué pasa                                                                |
| ---------------- | ----------------------------------------------------------------------- |
| `publica`        | Viaja al modelo tal cual                                                |
| `sensible-valor` | Al modelo va un marcador estable; el valor real sigue viaje al llamador |
| `sensible-texto` | No viaja en absoluto: se suprime la columna entera, nombre incluido     |

La tercera es para texto libre —`pedido_historial.comentario`,
`pedidos.justificacion`, `pedidos.tipo_baja_detalle`—, donde redactar con reglas es
frágil y no mandarlo es simple: un comentario de rechazo puede nombrar a cualquiera.

**Cómo se identifica la columna.** No por su nombre en el resultado, que es el
alias que eligió la consulta generada: un `SELECT p.documento AS codigo` produce una
columna llamada `codigo`, y comparar nombres la dejaría pasar entera. Se usa el par
`(OID de tabla, número de atributo)` que PostgreSQL emite en la descripción de
filas, que identifica el origen sin importar el alias y sobrevive tanto a la
envoltura en subconsulta del ejecutor como a un `WITH`. El riesgo residual —una
expresión sobre una columna personal deja el par vacío— está en TD-009.

**El marcador es un contador, no un hash del valor.** Un hash de un documento se
invierte por fuerza bruta en segundos, así que viajaría al proveedor y sería el
dato con un paso más. El contador es por orden de primera aparición, lleva la
etiqueta del manifiesto —`«documento 1»`— y no sobrevive al turno.

**Consecuencia de diseño**: con columnas sensibles, la narración deja de ser el
vehículo del dato. El modelo redacta el marco («encontré 4 docentes») y el dato lo
renderiza la interfaz.

**El enmascaramiento es asimétrico.** La pregunta cruda del usuario viaja al
proveedor a través de la generación: si alguien tipea un documento en la pregunta,
llega al modelo igual. Protege el camino de vuelta, no el de ida.

## Presupuesto y degradación

Tres cotas, más un estado propio para cuando alguna se agota.

### La cuota por actor

Se mide en **llamadas al modelo**, no en requests HTTP. Un turno con reescritor
cuesta tres llamadas y con reintento de transporte hasta cuatro requests por
llamada: contar requests del cliente subestimaría el consumo por un factor de tres.

Con una sola clave de API por ambiente, el proveedor factura al ambiente entero y no
puede atribuir consumo a nadie. Si la cuota no vive en la aplicación, no vive en
ningún lado.

Se acota por **identidad autenticada** y nunca por dirección de origen: todo el
tráfico entra por un túnel, así que un departamento tras NAT compartiría cupo con
sus vecinos.

El chequeo va **antes** del pipeline: superado el cupo no se emite ninguna llamada,
no una que falle. El cargo, en cambio, se hace al terminar el turno, en un `finally`
—así un turno que se cae a la mitad paga igual lo que llegó a gastar—.

Vive en memoria y se pierde en cada redespliegue. Es un mecanismo de equidad entre
usuarios, no la última línea contra una factura: esa es el techo de gasto en la
consola del proveedor. Registrado como TD-011.

### Las dos cotas de tiempo

| Cota                                 | Dónde                                            |
| ------------------------------------ | ------------------------------------------------ |
| Timeout de una llamada al proveedor  | `ProveedorConBreaker`                            |
| Presupuesto total del turno (RNF-09) | `PresupuestoDelTurno`, en la capa conversacional |

**No son la misma cosa y la segunda no se deriva de la primera.** Cuatro llamadas de
diez segundos son cuarenta segundos de espera y cada una habría respetado su límite.
El presupuesto es un único `CancellationTokenSource` encadenado al token del
request, creado al entrar a la capa y propagado hacia abajo.

Las etapas conservan sus propios timeouts —el de sentencia libera el backend de la
base, cosa que cancelar un token no hace—, pero ninguno de ellos es la cota del
turno.

`PresupuestoDelTurno.Vencio` distingue «se acabó el tiempo» de «el usuario cerró la
pestaña». Sin esa distinción, cada abandono se registraría como una caída del
servicio.

### El circuit breaker

Tres estados —cerrado, abierto, en prueba—, con el estado en el proceso y no en el
request. Cuenta **fallos de transporte y de timeout**, nunca rechazos semánticos: un
modelo que devuelve una respuesta que el validador descarta está sano, y cortarle las
llamadas por eso apagaría el asistente cada vez que alguien pregunta algo difícil.

En prueba deja pasar **una sola** llamada, no una por turno: con varios turnos
concurrentes, «una por turno» sería una avalancha contra un proveedor que recién se
levanta.

El proveedor se envuelve de afuera hacia adentro, de más barato a más caro:

```
ProveedorConTechoDeLlamadas   ← techo del turno
  └─ ProveedorConBreaker      ← estado del proveedor + timeout por llamada
       └─ proveedor real
```

### El modo degradado no se inventa: se expone

Cinco de los ocho pasos del pipeline no necesitan proveedor. La falta de modelo **no
corta el turno**: la capa conversacional resuelve el veredicto una vez, antes de
empezar, y lo consulta solo donde hace falta.

| Con el modelo caído o sin cupo     | Qué pasa                       |
| ---------------------------------- | ------------------------------ |
| Un saludo o un agradecimiento      | Responde, cero llamadas        |
| Una pregunta con entidad ambigua   | Devuelve su menú de aclaración |
| La respuesta a un menú abierto     | Se reconoce y se cierra        |
| Una pregunta de seguimiento        | Se responde sin reescribir     |
| Una pregunta que exige generar SQL | Servicio degradado             |

El texto distingue las dos causas. Con la cuota agotada el sistema **sabe** cuándo
vuelve el cupo y lo dice; con el proveedor caído no lo sabe nadie y no promete plazo.

## Los dos registros

Dos tablas en el schema `asistente` que **no se cruzan**, con retención de 90 días y
purga automática.

| Registro             | Guarda                                                                          | No guarda                |
| -------------------- | ------------------------------------------------------------------------------- | ------------------------ |
| `registro_operativo` | actor, momento, carril, estado, llamadas, tokens, latencia, reintento, truncado | El texto de la pregunta  |
| `registro_analitico` | pregunta, categoría, estado, **fecha redondeada al día**                        | El actor, la hora exacta |

**Ninguno guarda las filas devueltas ni la consulta generada.** Ni por defecto ni
detrás de un flag: son exactamente los datos que el enmascaramiento acaba de sacar
del camino de salida, y un `WHERE` puede llevar un documento. No están en el tipo
que recibe el escritor, así que no se pueden persistir por accidente.

**Por qué la fecha va redondeada**: con alrededor de treinta usuarios, un timestamp
preciso en las dos tablas permitiría reidentificar al autor de cada pregunta con un
join por tiempo. Desvincular sin quitar la hora no desvincula nada.

**Por qué el analítico no tiene clave secuencial**: una identidad autoincremental
sería, ella misma, la clave del join —la fila _n_ de una y la fila _n_ de la otra
serían el mismo turno—. Usa un UUID aleatorio. Queda un residual: el orden físico de
las filas todavía correlaciona. Está declarado, y es TD-012.

**Sin `audit.attach`, y declarado explícito en la migración.** Todas las tablas del
repositorio lo llaman al final de su archivo; acá no, porque `audit.change_log`
guarda la fila entera en JSON y no tiene política de retención: el texto de cada
pregunta sobreviviría a la purga en otro lado. La ausencia es una decisión, y hay un
test que falla si a alguna de las dos tablas le aparece el disparador.

**Los escribe la conexión dueña.** Los dos roles de solo lectura tienen el schema
`asistente` revocado entero: un asistente que pudiera consultar el registro analítico
respondería «qué le preguntó fulano al asistente» a cualquiera con el permiso de
consulta.

**El registro nunca hace fallar un turno.** Un registro que rompe el turno que estaba
registrando convierte la observabilidad en una fuente de indisponibilidad. Es la
decisión inversa a la del enmascarador, y a propósito: ahí un fallo silencioso filtra
datos, acá un fallo ruidoso niega un servicio que funciona.

## Reglas de negocio (BR-\*)

Ninguna propia. El asistente no decide nada del dominio: expone lo que otros
módulos ya decidieron. Las reglas que lo acotan son de seguridad, no de negocio, y
viven en el manifiesto de privilegios y en las policies RLS.

## Dependencias

- **Hacia adentro**: solo `ArsDocendi.Shared` (cadenas tipadas, permisos, migración
  de módulo). Ningún edge hacia otro módulo: el carril determinista de API, que sí
  agrega edges hacia `Modules.<X>.Contracts`, es de la épica E6.
- **Hacia afuera**: nadie lo consume.
- **Externas**: un proveedor de modelo de lenguaje, detrás de `IProveedorDeModelo`.
  Hoy la única implementación es la simulada. **El asistente no accede a ninguna
  otra fuente externa**: opera exclusivamente sobre la base del propio sistema.

## Specs activas

- `openspec/changes/asistente-fundaciones/` — roles, manifiesto, permiso, funciones
  del actor, RLS, cadenas tipadas y módulo base
- `openspec/changes/asistente-carril-sql/` — este carril
- `openspec/changes/asistente-evaluacion/` — el eje de capacidad y la exclusión del CI
- `openspec/changes/asistente-enmascaramiento/` — el manifiesto de sensibilidad y la frontera de salida
- `openspec/changes/asistente-capa-conversacional/` — el hilo, lo social, la aclaración y el seguimiento
- `openspec/changes/asistente-presupuesto-degradacion/` — cuota, topes, breaker y los dos registros

## Evaluación

La métrica primaria del proyecto es **corrección con abstención**, y se mide con el
evaluador de [`backend/eval/`](../../../backend/eval/README.md).

Está partido en dos por **qué cuesta dinero**, no por qué es «de evaluación»:
`ArsDocendi.Evaluacion.Nucleo` —generador del fixture, dataset, puntuación,
preflight, reporte— está en la solución y tiene tests en el CI;
`ArsDocendi.Evaluacion` —el ejecutable, lo único que instancia un proveedor real—
está **fuera**, con un guard adentro que falla si vuelve a entrar. El CI corre los
tests de la solución sin filtro, y el síntoma de olvidarlo sería una factura, no un
test rojo.

Hoy no se puede correr: no hay ninguna implementación de proveedor de modelo real.
Está registrado como TD-008.

## Decisiones registradas

- **El esquema del prompt se deriva de los privilegios efectivos** — una lista
  embebida en código se desincroniza en silencio y falla en las dos direcciones:
  describe columnas revocadas y omite columnas nuevas.
- **Los `COMMENT ON` viven en el DDL de cada módulo dueño** — mismo criterio con
  que las policies RLS viven en el de `designaciones`.
- **Similitud léxica y no embeddings** — con decenas de ejemplos, un vector store
  es un servicio, un modelo y una llamada de red más por turno para elegir entre
  pocas opciones.
- **La fecha de referencia es un parámetro del turno** — hace el eval determinista
  y permite prohibir el reloj entero en el validador sin romper ningún caso.
- **El límite pide una fila de más** — sin la fila sonda, «devolvió N» y «se
  recortó» son indistinguibles y la redacción afirma totales falsos.
- **El actor va transaction-local** — uno de sesión sobreviviría al pool y un turno
  heredaría el actor del anterior, respondiendo con el alcance equivocado sin
  tirar error.
- **El carril es un servicio y no un endpoint** — construir el contrato antes de
  tener los cuatro estados y el hilo conversacional obligaría a inventarlo dos
  veces.
