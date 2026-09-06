## Why

Un seguimiento que se refiere a algo que apareció en la **respuesta** no se puede resolver hoy.

Pasó así, con un actor de decanato y datos reales:

1. «dame 3 materias con profesores de ingeniería informática» → el asistente contesta **Ingeniería de Software**, la única que hay.
2. «armame una lista con los profesores de esa materia» → **«No puedo responder eso con la información que tengo disponible.»**

El reescritor hizo su trabajo y falló igual: recibe únicamente las **preguntas** anteriores (D1 de `asistente-capa-conversacional`), y «Ingeniería de Software» nunca estuvo en una pregunta — estuvo en la respuesta. Lo mejor que pudo hacer fue expandir la anáfora sin resolverla: _«los profesores de esa materia mencionada entre las 3 materias con profesores de ingeniería informática»_. Sigue sin entenderse sola, y el generador se abstuvo, que es lo correcto sobre una pregunta que no se entiende.

**D1 no está equivocado y no se revierte.** El hilo no guarda filas porque el enmascarador saca los datos personales del camino hacia el proveedor, y guardarlos los devolvería al prompt por la puerta del historial. Eso sigue siendo cierto y sigue siendo la regla.

Lo que este cambio observa es que **no hace falta guardar filas para resolver el caso**. El estado del arte en text-to-SQL conversacional arrastra la **consulta anterior**, no sus resultados: CoE-SQL genera el turno actual editando la consulta previa y reporta explícitamente que los enfoques basados en resultados de ejecución le funcionaron peor; EditSQL e IGSQL hacen variantes de lo mismo. Y la consulta anterior ya define «esas materias» como un conjunto: la respuesta correcta a la pregunta 2 es **anidar la consulta 1 como subconsulta**, sin necesitar el valor.

El SQL no agrega ningún dato al hilo que no estuviera ya: sus literales salen de las preguntas del usuario, que el hilo ya guarda. Ninguna fila leída de la base entra.

## What Changes

- **`TurnoDelHilo` gana la consulta ejecutada.** Pasa de `(Pregunta, Cuando)` a `(Pregunta, SqlEjecutado, Cuando)`. Sólo se anota la consulta del turno que efectivamente produjo la respuesta mostrada — si el reintento reemplazó la primera, se anota la segunda.
- **El generador recibe la consulta del turno anterior** cuando el turno es un seguimiento, con la instrucción de editarla o anidarla. Es un bloque más del mensaje de usuario, no del prefijo estable: no puede tocar la huella cacheada.
- **D1 se enmienda explícitamente**, no se reinterpreta. El texto vigente dice «lo que se guarda por turno es la pregunta interpretada y su marca de tiempo. Nada más», y este cambio lo contradice: se reescribe la decisión nombrando qué se suma y por qué eso no reabre lo que D1 protege.
- **La consulta arrastrada nunca se muestra sin permiso.** `asistente.ver_consulta` ya gobierna el campo `Sql` de la respuesta; el arrastre es interno al turno y no cambia esa frontera.
- **Ventana y segmento se respetan**: se arrastra la consulta de los turnos del **segmento vigente** y dentro del `TopeDeTurnosDelHistorial`, así que un pivote la suelta igual que suelta las preguntas.

## Capabilities

### Modified Capabilities

- `asistente-hilo`: el hilo pasa a guardar, además de la pregunta interpretada, la consulta que la respondió — y sigue sin guardar ninguna fila.
- `asistente-seguimiento`: un seguimiento se resuelve editando o anidando la consulta del turno anterior, con el mismo recorte por segmento y tope que ya rige para las preguntas.

## Impact

- `Modules.Asistente`: `HiloConversacional` (`TurnoDelHilo`, `Agregar`), `CapaConversacional`, `GeneradorDeSql`, `CarrilSql` (devuelve qué consulta se ejecutó), `RenderizadorDeEsquema` (regla del seguimiento).
- **Cassettes**: la regla nueva cambia el prefijo estable, así que el corpus se regraba y el gate de regresión exige una corrida financiada antes de comparar.
- `openspec/changes/asistente-capa-conversacional/design.md`: D1 enmendado.
- Costo: ~150 tokens por turno arrastrado, contra ~500 de una tabla de resultados. Del orden de +3% sobre el turno medido.
