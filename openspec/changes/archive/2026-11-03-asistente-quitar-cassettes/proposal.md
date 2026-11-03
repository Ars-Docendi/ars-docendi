## Why

Anthropic dejó de ser proveedor de producción: el asistente corre sólo con el modelo local. Los cassettes del proveedor grababan y reproducían únicamente el formato de cable de Anthropic (`ClaveDeCassette` exige el `system` en la raíz del cuerpo), así que no podían proteger el camino que usa producción. Además se vencían con cada cambio de esquema: los 129 del corpus quedaron sellados contra un fixture y un prefijo que ya no existen, y mantenerlos verdes exigía una corrida financiada contra un proveedor que no se usa.

## What Changes

- **BREAKING**: se eliminan las opciones `Asistente__DirectorioDeCassettes` y `Asistente__RegrabarCassettes`.
- Se eliminan el grabador del cliente HTTP del proveedor, el almacén, la clave y el sello de los cassettes, el corpus versionado y los tests que lo custodiaban.
- `RedaccionSinEnmascarar` pierde la condición «sin directorio de cassettes»: rige con la opción encendida y un proveedor en hardware propio.
- El evaluador deja de registrar la huella del fixture para el grabador; la sigue estampando en sus reportes.

## Capabilities

### Removed Capabilities

- `asistente-cassettes-del-proveedor`: la grabación y reproducción de respuestas del proveedor.

### Modified Capabilities

- `asistente-redaccion-sin-enmascarado-local`: la opción ya no depende de que no haya cassettes.
- `asistente-enmascarador`: la excepción de la redacción sin enmascarar se describe sin cassettes.
- `asistente-generacion-sql`: cambiar las instrucciones fijas ya no exige regrabar un corpus.
- `asistente-glosario-institucional`: el prefijo por defecto sigue idéntico; deja de mencionar los cassettes.
- `asistente-modelo-en-caliente`: la selección de hardware propio respeta la opción del ambiente, sin condición de cassettes.

## Impact

- Código: `Modules.Asistente` (`ModuleExtensions`, `OpcionesAsistente`, `AnuncioDeLaRedaccion`), `ArsDocendi.Evaluacion`.
- Tests: se borran las clases de cassettes y `ApagadoPorDefaultTests`; se ajustan las de opciones y enmascarado.
- Docs: README del módulo, `backend/eval/README.md`, `docs/architecture/domains/asistente.md`, `docs/architecture/modelo-local.md`, `docs/quality/tech-debt.md` (cierra TD-017).
