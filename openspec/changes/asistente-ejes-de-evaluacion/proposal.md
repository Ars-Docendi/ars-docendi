## Why

La evaluación tiene un eje —capacidad— y el documento de definición pide cuatro. Los tres que faltan no son «más de lo mismo»: cada uno mide algo que el de capacidad **no puede ver**.

- **Robustez de fraseo**: el eje de capacidad pregunta siempre bien escrito. Nadie escribe así. Sin este eje, un asistente que solo entiende preguntas de manual da el mismo número que uno que entiende a la gente.
- **Diálogo**: el eje de capacidad manda turnos autocontenidos, así que la capa conversacional entera —reescritor, aclaración, cambio de tema— no está medida por nada.
- **Social y meta**: el carril de cero tokens tiene tests de cableado, pero **nada mide qué proporción del tráfico real captura**, ni qué proporción de preguntas legítimas se come de más.

Y falta el gate. Sin línea de base, cada corrida es un número suelto: no se sabe si mejoró, si empeoró, ni qué se rompió.

## What Changes

- **Eje de robustez**, con perturbaciones de las preguntas del eje de capacidad —sin tildes, con errores de tipeo, con sinónimos, parciales, coloquiales—. Cada ítem **no declara su propia consulta**: la toma del ítem de origen, así que la reutilización byte-idéntica es **estructural** y no una convención que un test tenga que vigilar. Sin eso, un fallo sería ambiguo: ¿no entendió el fraseo, o no supo escribir la consulta?
- **Eje de diálogo**, con conversaciones de varios turnos y una propiedad que el eje de capacidad no tiene: **términos prohibidos en la pregunta interpretada**. Un diálogo puede dar 100% mientras el sistema arrastra el filtro del turno anterior; si el turno de prueba es autocontenido, el arrastre no se detecta. Incluye un **pivote duro**: turno uno sobre una entidad, turno dos sobre otra sin ninguna referencia anafórica.
- **Eje social y meta**, con tres clases y puntuación asimétrica. Los ítems sociales aprueban **solo si resuelven con cero tokens de entrada**; los negativos —preguntas legítimas del eje de capacidad— fallan si el enrutador los captura.
- **Trampa cubierta del otro lado**: sin crédito de API, el assert de cero tokens también daría verde con el proveedor caído. El runner social **aborta ruidosamente si todos los turnos consumen cero tokens**.
- **Gate de regresión con lock por ítem**, contra un archivo de línea de base versionado. Falla si **cualquier ítem que pasaba ahora falla**, aunque el agregado suba. Si cambió alguno de los tres hashes del sellado, exige regenerar en vez de comparar.
- **Cuatro reportes separados**, porque los ejes **no son comparables entre sí**.

## Capabilities

### New Capabilities

- `asistente-eje-robustez`: perturbaciones con consulta de referencia heredada del ítem de origen.
- `asistente-eje-dialogo`: conversaciones de varios turnos con chequeo negativo de arrastre y pivote duro.
- `asistente-eje-social`: las tres clases, el assert de costo cero y el aborto ante proveedor caído.
- `asistente-gate-de-regresion`: la línea de base por ítem y el gate que la compara.

## Impact

- `backend/src/ArsDocendi.Evaluacion.Nucleo/Dataset/` — los tres datasets nuevos.
- `backend/src/ArsDocendi.Evaluacion.Nucleo/Runner/` — los tres runners, el medidor de consumo y el gate.
- `backend/eval/datasets/` — `robustez.json`, `dialogo.json`, `social.json`.
- `backend/eval/lineas-de-base/` — el archivo del lock, versionado.
- `backend/eval/ArsDocendi.Evaluacion/Program.cs` — los cuatro ejes y el gate.
- `backend/tests/ArsDocendi.IntegrationTests/Evaluacion/` — los datasets, los runners y el gate.
- `backend/eval/README.md`.

## Rollback

Aditivo. El eje de capacidad no se toca: los tres nuevos son datasets, runners y reportes propios. El gate se apaga no invocándolo.
