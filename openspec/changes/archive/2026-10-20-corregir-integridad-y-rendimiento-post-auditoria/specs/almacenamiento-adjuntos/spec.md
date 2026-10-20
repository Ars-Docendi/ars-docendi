# Spec Delta

## ADDED Requirements

### Requirement: Los bytes publicados corresponden a la versión analizada

La confirmación SHALL publicar únicamente la versión exacta cuyos bytes, tamaño, MIME y hash fueron verificados y analizados. Ninguna carga tardía o concurrente MUST poder sustituirla. Confirmar nuevamente una versión ya disponible SHALL ser idempotente; una versión distinta MUST NOT reutilizar su validación.

#### Scenario: PUT iniciado antes de confirmar

- **GIVEN** una carga A y una segunda carga B del mismo archivo detenida tras superar su control inicial
- **WHEN** A se confirma y B completa después su cuerpo
- **THEN** la descarga conserva exactamente los bytes y hash analizados de A, o B se rechaza, sin publicar sus bytes bajo la validación de A

#### Scenario: Dos confirmaciones simultáneas

- **GIVEN** dos confirmaciones de la misma versión cargada
- **WHEN** se ejecutan concurrentemente
- **THEN** existe una única publicación coherente y ambas respuestas exitosas, si las hay, identifican esa misma versión

#### Scenario: Revisión interrumpida

- **GIVEN** la revisión se interrumpe antes de publicar
- **WHEN** se consulta o reintenta la sesión
- **THEN** no se ofrecen bytes como disponibles sin análisis completo y la recuperación no autoriza una sobrescritura de otra versión publicada

### Requirement: Eliminación y asociación coordinadas

La eliminación lógica, el borrado remoto y la limpieza SHALL coordinarse con asociaciones y revisión concurrentes. Un archivo referenciado MUST NOT perder sus bytes; un archivo reservado para eliminación MUST NOT admitir referencias nuevas ni quedar publicado como disponible. Los fallos remotos SHALL ser explícitos y reintentables sin resucitar estados obsoletos.

#### Scenario: El propietario borra un adjunto de un pedido

- **GIVEN** un archivo disponible todavía referenciado por un pedido
- **WHEN** su propietario solicita eliminarlo directamente
- **THEN** recibe conflicto seguro y el pedido mantiene su descarga autorizada

#### Scenario: Asociación contra eliminación

- **GIVEN** una asociación válida y una eliminación del mismo archivo intercaladas
- **WHEN** ambas intentan confirmar
- **THEN** no pueden dejar una referencia confirmada a bytes borrados; una de las operaciones se rechaza o se serializa de forma coherente

#### Scenario: Confirmación contra eliminación

- **GIVEN** una confirmación y una eliminación intercaladas
- **WHEN** la eliminación obtiene la reserva definitiva
- **THEN** la confirmación no vuelve a publicar el archivo ni responde disponibilidad de un objeto inexistente

#### Scenario: Fallo del proveedor al borrar

- **GIVEN** el archivo ya fue reservado sin referencias para eliminación y el proveedor falla
- **WHEN** se consulta o reintenta el borrado
- **THEN** no vuelve a aparecer disponible y el reintento puede terminar la eliminación sin modificar archivos ajenos

## MODIFIED Requirements

### Requirement: Auditoría, limpieza y retención

El sistema SHALL auditar creación, confirmación, rechazo, asociación, descarga, reemplazo y eliminación lógica sin registrar bytes ni contenido sensible. SHALL limpiar cargas pendientes y objetos huérfanos de manera idempotente. La desvinculación de una asociación MUST NOT borrar ni indisponibilizar el archivo para otros consumidores que lo referencian; la eliminación física SHALL respetar esas referencias y la política de retención, conservando metadata mínima de trazabilidad.

#### Scenario: Carga abandonada

- **GIVEN** una carga pendiente cuyo vencimiento superó el umbral configurado
- **WHEN** corre el proceso de limpieza
- **THEN** se elimina el objeto temporal y la metadata queda marcada o eliminada según la política, sin afectar archivos asociados ni una carga/revisión vigente concurrente

#### Scenario: Archivo reemplazado

- **GIVEN** un CV o documento de proyecto reemplazado por otro y sin referencias restantes
- **WHEN** corre la política de retención que permite su eliminación
- **THEN** el objeto anterior deja de estar disponible y su eliminación queda auditada

#### Scenario: Registro sin objeto histórico

- **GIVEN** un registro legacy que solo contiene nombre y URI histórica sin objeto SeaweedFS
- **WHEN** se consulta
- **THEN** se conserva como metadata histórica y no se inventa ni descarga un archivo inexistente

#### Scenario: CV anterior conservado por un trámite

- **GIVEN** el CV anterior del perfil también está asociado a un pedido
- **WHEN** el docente reemplaza o elimina su asociación de CV
- **THEN** el perfil deja de referenciarlo pero el pedido conserva sus bytes y descarga autorizada

#### Scenario: Consulta de referencias incompleta

- **GIVEN** falla la consulta de un consumidor registrado
- **WHEN** limpieza o eliminación evalúan si pueden borrar el archivo
- **THEN** se rechaza el borrado y no se interpreta el fallo como cero referencias
