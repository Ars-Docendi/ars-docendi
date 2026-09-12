## Why

El cambio anterior —[`asistente-carril-sql`](../asistente-carril-sql/proposal.md)— construyó el camino completo de la pregunta a la respuesta: prefijo derivado de los privilegios efectivos, generación, validación, ejecución acotada, política de abstención y redacción. El asistente ya responde.

Lo que ese camino **no** tiene es una frontera de salida. Hoy las filas que devuelve la base entran tal cual al prompt de redacción, así que **un documento, un CUIL o un teléfono viajan al proveedor externo del modelo** en cuanto el actor tiene permiso para leerlos. El permiso es real y la lectura es legítima; lo que no está decidido es que ese dato salga del sistema hacia un tercero.

La restricción de producto es explícita: el asistente solo opera sobre datos de la propia base, y los datos personales necesitan un mecanismo de enmascaramiento. `asistente-fundaciones` resolvió **quién puede leer qué** con dos roles y `GRANT` por columna. Este cambio resuelve la pregunta distinta de **qué sale hacia afuera**, que es ortogonal: un actor puede tener todo el derecho a ver un teléfono en pantalla y aun así no haber ninguna razón para que ese teléfono llegue al proveedor.

Este cambio construye el **enmascarador**, que se interpone entre la ejecución de la SQL y la llamada de redacción, más el **manifiesto de sensibilidad** que lo gobierna. Y cierra los dos huecos que quedaron abiertos en la selección de conexión por permiso de datos personales.

El enmascaramiento es **asimétrico y no cierra la entrada**: la pregunta cruda del usuario viaja al proveedor a través de la generación. Si alguien tipea un documento en la pregunta, llega al modelo igual. Protege el camino de vuelta, no el de ida. Se declara acá porque un lector que asuma simetría va a confiar de más.

## What Changes

- **Manifiesto de sensibilidad por columna**, hermano del de privilegios y en el mismo directorio, con tres categorías: `publica` viaja al modelo tal cual, `sensible-valor` viaja como marcador estable y `sensible-texto` no viaja en absoluto. Un test falla si aparece una columna legible sin clasificar, con la misma disciplina con que el manifiesto de privilegios se compara contra la base.
- **Resolución de la clasificación a identificadores del motor**. La clasificación se resuelve una vez a pares `(OID de tabla, número de atributo)` y el enmascarado es una búsqueda por ese par. Es lo que hace que **un alias no pueda esconder una columna**: el par lo emite PostgreSQL en la descripción de filas, no lo elige la consulta.
- **Enmascarador puro** entre la ejecución y la redacción. Las filas que llegan al modelo van enmascaradas; las filas reales siguen viaje al llamador del carril.
- **Marcadores estables dentro de una respuesta**: el mismo valor real recibe siempre el mismo marcador, para que el modelo pueda referirse a él sin conocerlo. El marcador es un contador por orden de aparición y **no se deriva del valor**, para que no sea invertible.
- **Cierre de la selección de conexión por permiso de datos personales**: el mecanismo ya está construido y funcionando, pero el riesgo residual está documentado solo en el documento de producto y no en el código, y faltan los tests de aceptación de punta a punta.

## Capabilities

### New Capabilities

- `asistente-manifiesto-sensibilidad`: clasificación versionada de cada columna legible en tres categorías, de una sola fuente, verificada contra las columnas que la base concede de verdad.
- `asistente-enmascarador`: frontera de salida entre la ejecución y la redacción, con marcadores estables no invertibles y supresión total de las columnas de texto libre.

### Modified Capabilities

- `asistente-ejecucion-acotada`: el resultado de la ejecución pasa a llevar la clasificación de sensibilidad de cada columna, resuelta por el motor.
- `asistente-redaccion`: el prompt de redacción se arma sobre el resultado enmascarado y no sobre el crudo.

## Impact

- `database/asistente/manifiesto-sensibilidad.json` — el manifiesto, embebido como recurso del módulo por el mismo mecanismo con que ya viaja el DDL.
- `backend/src/Modules.Asistente/Application/` — el manifiesto tipado, la clasificación, el enmascarador puro y su cableado en el orquestador.
- `backend/src/Modules.Asistente/Infrastructure/` — la resolución de la clasificación a identificadores del motor y su captura en el ejecutor.
- `backend/src/Modules.Asistente/Modules.Asistente.csproj` — el manifiesto como recurso embebido, con el mismo guard que ya protege al DDL de un glob vacío.
- `backend/tests/ArsDocendi.IntegrationTests/Asistente/` — cobertura del manifiesto, del enmascarador en memoria y del turno completo contra una base real.
- `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md` y `docs/quality/tech-debt.md`.
- **Grafo de dependencias**: sin edges nuevos. `Modules.Asistente` sigue dependiendo únicamente de `ArsDocendi.Shared`.

## Rollback

Aditivo y reversible por partes:

- El manifiesto es un archivo de datos. Sin él, el módulo no compila —el guard del csproj falla en build, no en runtime—, que es el modo de falla buscado.
- El enmascarador es una pieza nueva en el medio del orquestador. Quitar su llamada devuelve el comportamiento anterior exacto: el resultado crudo al prompt de redacción.
- No se modifica ninguna tabla, ningún `GRANT`, ninguna policy ni ninguna función de las que ya existen.
