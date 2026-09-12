## Context

El repo tiene dos registros de fronteras y solo uno funciona.

El que funciona es `database/asistente/manifiesto-privilegios.json`: un archivo declarativo, `ManifiestoPrivilegiosTests` lo compara contra los privilegios efectivos de la base en tres direcciones, y su tercera dirección —«objeto sin clasificar»— atrapó `__EFMigrationsHistory` en la primera corrida. La frontera del asistente es falsable porque hay un archivo y un test que lo confronta con la realidad.

El que no funciona es el «Edge registry» de `docs/architecture/dependency-graph.md`. Es una tabla markdown, nadie la lee desde `backend/tests/`, y ya acumuló las tres desviaciones posibles: dos aristas reales sin fila (`ArsDocendi.Evaluacion.Nucleo → Modules.Asistente` y `ArsDocendi.Host → ArsDocendi.Shared`) y una fila sin arista (`ArsDocendi.Host → Modules.Aulas/Portal/Tareas.Contracts`, que ningún `.csproj` referencia).

Lo que hoy sí se verifica, y conviene no duplicar:

- `ArquitecturaIdentityTests.Proyectos_de_modulo_no_referencian_internals_de_otro_modulo` — pero su glob es `Modules.*.csproj`, así que **no ve** a `ArsDocendi.Evaluacion.Nucleo`, que es precisamente el proyecto que referencia el interno de un módulo.
- `ArquitecturaAsistenteTests` — que el módulo del asistente solo referencie `ArsDocendi.Shared`.

Ninguno de los dos construye el grafo ni comprueba que sea acíclico. El invariante #2 se cumple hoy por inspección humana.

**El horizonte.** ARS-64 va a escribir el invariante #14 —la excepción del asistente a la frontera de Contracts, sostenida por el motor— y ARS-46 va a agregar aristas del carril determinista hacia `Contracts` ajenos. Las dos cosas se apoyan en un registro que no existe. El referente es Metabase: su grafo de módulos **no es un DAG** —tiene ciclos aceptados— y `:friends` es un bypass total, pero es **un dato que el linter lee y que falla el build**, no un párrafo. A diez veces nuestro tamaño renunciaron al invariante #2 y aun así conservaron la parte que importa: la excepción es verificable. Conviene diseñar la escapatoria mientras todavía se puede exigir que cada uso lleve motivo y ticket.

## Goals / Non-Goals

**Goals:**

- Que el grafo de proyectos del backend tenga **una sola declaración**, y que desviarse de ella ponga el CI en rojo.
- Que el invariante #2 lo verifique un test y no un lector atento.
- Que una excepción a un invariante sea un dato con motivo y ticket, sujeto al mismo test que el resto.
- Que `docs/architecture/dependency-graph.md` deje de contener una lista que puede mentir.

**Non-Goals:**

- **Arreglar el grafo.** El manifiesto se escribe con las aristas que hoy existen. Si deja a la vista una arista que el equipo no quiere, borrarla es otro cambio.
- **Escribir el invariante #14.** Este cambio construye el lugar donde su excepción va a vivir; el texto es ARS-64.
- **Agregar las aristas de ARS-46.**
- **Verificar el grafo de `backend/tests/`**, ni el de paquetes NuGet, ni el del frontend.

## Decisions

### D1 — El manifiesto vive en `backend/`, no en `docs/`

`backend/manifiesto-de-aristas.json`.

**Por qué ahí y no junto al documento que lo cita.** Por simetría con el precedente: el manifiesto de privilegios vive en `database/asistente/`, al lado de las migraciones que aplican los `GRANT` que declara. La declaración vive con lo que declara. Acá lo declarado son los `.csproj`, así que el manifiesto va con ellos.

Y hay una razón operativa que decide el empate: el CI filtra por paths, y `backend/**` dispara la suite del backend mientras que `docs/**` no. Un manifiesto bajo `docs/` se podría editar sin que corriera el test que lo defiende, que es exactamente el defecto que este cambio existe para cerrar.

### D2 — La forma del manifiesto espeja la del manifiesto de privilegios

Dos secciones y una versión:

- `proyectos` — todo `.csproj` de `backend/src`, con `estado` (`activo` u `huerfano`) y `motivo` cuando corresponda.
- `aristas` — `origen`, `destino`, `via`, `motivo`, y `excepcion` opcional con `invariante` y `ticket`.

**La clave de un proyecto es el nombre del `.csproj` sin extensión**, no su ruta relativa. Es lo que un humano escribe y lo que ya usa el diagrama. El costo es que el nombre deja de ser clave si dos proyectos lo comparten, así que el cargador **falla si encuentra dos `.csproj` homónimos** bajo `backend/src`. Una clave que se degrada en silencio no es una clave.

### D3 — El vocabulario dice «arista»

El invariante #13 pide identificadores en español, y el ticket ya usa «arista». El archivo, las claves del JSON y los tipos de C# dicen `arista`. Las secciones de `dependency-graph.md` que este cambio reescribe también. «Edge» sobrevive solo en el identificador del change, que ya estaba creado.

### D4 — Tres direcciones, no dos

El ticket pide dos: arista sin fila, y fila sin arista. Se agrega la tercera del precedente —**proyecto sin clasificar**— porque sin ella hay un agujero concreto y ya poblado: `Modules.Asistente.Contracts` no aparece en ninguna arista, ni como origen ni como destino. Hoy su ausencia del registro está explicada en un párrafo del documento. Con la tercera dirección es una fila con estado `huerfano` y motivo, y un proyecto nuevo que nadie referencia rompe el CI en lugar de pasar inadvertido.

Es la misma dirección que atrapó `__EFMigrationsHistory`, aplicada a proyectos en vez de a tablas.

**Y el barrido no puede quedarse vacío.** Si el glob deja de alcanzar `backend/src`, el test tiene que fallar, no pasar en verde con cero aristas. Es el mismo guard que ya escriben `ArquitecturaIdentityTests` (`Assert.NotEmpty(controllers)`) y `ManifiestoPrivilegiosTests`, y por el mismo motivo: un test que no distingue «no hay infracciones» de «no miré nada» no es un test.

### D5 — La aciclicidad se afirma sobre el código, no sobre el manifiesto

El DFS corre sobre las aristas leídas de los `.csproj`.

Con las tres direcciones en verde los dos conjuntos son idénticos, así que la elección parece indiferente. No lo es cuando algo está roto: si el manifiesto quedó desactualizado, lo que interesa saber es si **el código** tiene un ciclo, y afirmarlo sobre el papel respondería sobre un grafo que ya no existe. El invariante #2 es una propiedad de lo que compila.

**Alternativa descartada**: correr el DFS sobre lo declarado, que permitiría detectar un ciclo _antes_ de escribirlo en el `.csproj`. No sirve: D1 ya prohíbe filas sin arista real, así que un manifiesto no puede adelantarse al código.

### D6 — `dependency-graph.md` **cita** el manifiesto; no se genera desde él

La alternativa real era generar la tabla markdown desde el JSON, con un script y un guard de CI que falle si regenerar produce diff. El repo tiene precedente (`scripts/generate-indexes.ts`).

**Se descarta.** El objetivo del ticket es dejar de tener dos verdades, y generar conserva dos representaciones con un robot en el medio. El modo de fallar es peor de lo que parece: entre la edición del manifiesto y la regeneración, el documento muestra una tabla **plausible y equivocada**, que es exactamente lo que un lector confía. Y una tabla generada dentro de un `.md` invita a que alguien la edite a mano y pierda el cambio.

Sin tabla no hay nada que creer de más: el lector va al único archivo que un test defiende.

**Costo aceptado**: leer JSON es peor que leer una tabla. Se compensa con el `motivo` por fila —que la tabla actual también tenía— y con que el documento conserve la prosa que explica el grafo, que es lo que un humano realmente lee.

### D7 — El diagrama Mermaid queda, marcado como no normativo

Borrar el diagrama sería consistente pero empobrece el documento: es lo que orienta a alguien que llega al repo. Se queda con un rótulo explícito de dibujo de orientación y con el puntero al manifiesto.

**Y el costo se declara en lugar de disimularse**: el diagrama puede desincronizarse y ningún test lo va a notar. Es aceptable porque un dibujo con subgrafos y líneas punteadas no es una lista, y porque el lector queda advertido de dónde está la verdad. Parsear Mermaid para verificarlo sería más frágil que el problema que resuelve. Queda anotado como deuda.

### D8 — El barrido cubre `backend/src` entero y excluye `backend/tests`

`backend/src` completo, sin filtrar por prefijo `Modules.`: es la única forma de que `ArsDocendi.Evaluacion.Nucleo` entre al grafo, y es la desviación que motivó el ticket.

`backend/tests` queda afuera: un proyecto de tests referencia todo por definición, su grafo no dice nada sobre las fronteras del sistema y meterlo obligaría a declarar aristas que nadie va a revisar. Un manifiesto con filas que nadie mira se degrada igual que una tabla.

### D9 — La excepción es un campo verificado, aunque todavía no exista el invariante #14

`excepcion: { invariante, ticket }` más el `motivo` de la propia arista. El test falla si una arista se declara como excepción sin ticket o sin motivo, con la misma forma que `Toda_denegacion_explicita_lleva_motivo_escrito`.

**El manifiesto nace con una excepción usada**: `ArsDocendi.Evaluacion.Nucleo → Modules.Asistente`, un proyecto que no es módulo referenciando el interno de uno. El invariante #1 no la cubre literalmente —igual que no cubría la escritura a `identity`—, su motivo está escrito en un comentario del `.csproj` y ningún test la alcanza. Registrarla es el caso de uso del campo antes de que llegue ARS-64.

**El reparto entre archivos es deliberado**: el manifiesto guarda las **instancias** de excepción; `CLAUDE.md` guarda el **texto del invariante**. Ninguno de los dos repite al otro.

### D10 — El test va donde ya viven los tests de arquitectura

`backend/tests/ArsDocendi.IntegrationTests/Backend/`, junto a `ArquitecturaIdentityTests`, aunque no toque la base: ese es el precedente del repo para tests que leen archivos del repositorio. Usa `RaizRepositorio` —el destino al que TD-007 quiere migrar las nueve búsquedas privadas de raíz— en vez de escribir la décima.

**Gotcha conocido**: los `.csproj` escriben las rutas con separador de Windows (`..\Modules.Asistente\...`). `ArquitecturaAsistenteTests` ya tiene el comentario que lo advierte; el lector de aristas normaliza el separador antes de resolver el nombre del proyecto destino.

### D11 — El guard del asistente convive con el manifiesto; no lo reemplaza

`ArquitecturaAsistenteTests.El_modulo_solo_referencia_ArsDocendi_Shared` fija que `Modules.Asistente` tenga exactamente una referencia. El manifiesto lee los mismos `.csproj`, así que la tentación es borrarlo por duplicado.

**Se conserva, porque afirman cosas distintas.** El manifiesto afirma que _toda arista real tiene su fila_: se pone en rojo si alguien agrega una referencia sin registrarla, y vuelve a verde en cuanto escribe la fila. El guard del asistente afirma una _prohibición_: hasta que llegue ARS-46 con el carril determinista, cualquier referencia nueva de ese módulo es un error y no una decisión. Registrar no es aprobar.

El caso que lo decide: si el manifiesto lo reemplazara, agregar `Modules.Asistente → Modules.Designaciones.Contracts` **con** su fila pasaría en verde, y eso es exactamente lo que hoy está prohibido.

**Costo aceptado**: el día que ARS-46 agregue esa arista habrá que tocar dos lugares —la fila del manifiesto y esa aserción—. Es la fricción buscada: el segundo lugar es donde alguien lee «cualquier referencia nueva es un error, no una decisión» y tiene que decidir borrarlo.

## Risks / Trade-offs

- **El manifiesto se escribe con el grafo tal como está, desviaciones incluidas.** Alguien puede leerlo como aprobación retroactiva de las dos aristas que no tenían fila → cada una entra con su motivo escrito, y la de `Evaluacion.Nucleo` entra marcada como excepción con ticket. Registrar no es aprobar, pero registrar con motivo obliga a escribir por qué.
- **El diagrama Mermaid puede desincronizarse y nada lo detecta** (D7) → queda rotulado como no normativo y anotado en `docs/quality/tech-debt.md`.
- **Un manifiesto que hay que editar a mano cansa, y el atajo es aflojar el test.** El día que alguien agregue una arista y el CI se ponga en rojo, la salida barata es marcarla como excepción → el test exige ticket, así que la salida barata cuesta abrir un ticket, que es exactamente la fricción buscada.
- **La tercera dirección puede volverse ruidosa** si el repo suma proyectos auxiliares seguido → hoy son trece proyectos y el costo por proyecto nuevo es una fila. Si algún día molesta, la respuesta es acotar el barrido con un motivo escrito, no borrar la dirección.
- **`backend/tests` afuera deja un hueco real**: un proyecto de tests podría abrir un camino que el grafo de `src` no muestra → es un hueco aceptado y declarado en el alcance, no un olvido.

## Migration Plan

No hay migración: el cambio no toca runtime, ni schema, ni API. El manifiesto llega ya sincronizado con el código, así que el test nace en verde salvo por lo que la implementación decida corregir en el documento.

**Rollback**: borrar el archivo, el test y revertir los tres documentos. Nada depende de ellos en ejecución.

**Orden de la implementación**: el test primero y en rojo —con el manifiesto vacío o incompleto—, para comprobar que las tres direcciones y el DFS fallan cuando tienen que fallar. Un verificador que nace en verde no demostró nada.

## Open Questions

- **¿La arista `ArsDocendi.Evaluacion.Nucleo → Modules.Asistente` se conserva o se corta?** Este cambio la registra con motivo y ticket; decidir si se queda es trabajo del equipo y de otro change. La fila es lo que hace que la pregunta sea visible.
- **¿El vocabulario de `via` va a necesitar un segundo valor antes de ARS-46?** Hoy `project-reference` es el único que el verificador sabe comprobar. Si ARS-46 introduce una vía distinta, agregarla exige enseñarle al verificador a comprobarla en el mismo cambio, no antes.
