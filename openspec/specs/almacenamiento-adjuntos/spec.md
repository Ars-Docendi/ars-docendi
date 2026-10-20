# almacenamiento-adjuntos Specification

## Purpose

Provee almacenamiento privado, verificable y aislado por ambiente para PDFs e imágenes que forman parte de novedades de designaciones y perfiles docentes, sin convertir PostgreSQL en un repositorio de binarios ni exponer documentación sensible mediante URLs públicas.

## Requirements

### Requirement: Almacenamiento privado aislado por ambiente

El sistema SHALL almacenar los bytes de los adjuntos en SeaweedFS self-hosted mediante buckets privados separados por ambiente. Cada ambiente SHALL usar credenciales y una política que impidan leer, escribir o borrar objetos de otro ambiente. SeaweedFS MUST ser accesible únicamente desde la red interna de la aplicación y MUST NOT publicarse por Traefik, Cloudflare Tunnel ni ningún hostname de usuario.

#### Scenario: Ambiente de producción aislado

- **GIVEN** existen buckets de producción y staging en la misma instancia SeaweedFS
- **WHEN** el backend de staging intenta leer, escribir o eliminar un objeto de producción
- **THEN** la operación es rechazada por la política del bucket y no revela si el objeto existe

#### Scenario: SeaweedFS no está expuesto públicamente

- **WHEN** un cliente solicita el endpoint de SeaweedFS a través del hostname público de cualquier ambiente
- **THEN** no existe una ruta pública hacia la consola ni hacia la API de SeaweedFS
- **AND** las únicas rutas públicas de archivos son las descargas autorizadas por Ars Docendi

#### Scenario: Teardown de un preview

- **WHEN** se destruye el ambiente `pr-N`
- **THEN** se eliminan sus objetos y credenciales sin afectar los buckets de producción, staging ni otros previews

### Requirement: Carga controlada y confirmable

El backend SHALL iniciar cada carga y emitir un identificador de archivo único junto con una autorización temporal de subida. El cliente MUST NOT elegir la URI, bucket, clave de objeto ni estado final del archivo. Un archivo solo SHALL poder asociarse a una novedad o perfil después de que el backend confirme que la subida terminó y que el objeto pertenece al ambiente y propósito solicitados.

#### Scenario: Iniciar una carga

- **GIVEN** un actor autenticado y autorizado solicita cargar un CV, DNI, justificativo o documento de proyecto
- **WHEN** envía propósito, nombre, MIME declarado y tamaño esperado
- **THEN** el backend devuelve un `archivoId`, una autorización temporal de subida y la fecha de expiración
- **AND** crea metadata en estado `pendiente`

#### Scenario: URI arbitraria rechazada

- **WHEN** un cliente intenta guardar una URI externa o una clave de objeto elegida por él como adjunto confirmado
- **THEN** el backend rechaza la solicitud y no crea una asociación de dominio

#### Scenario: Confirmación de carga

- **GIVEN** una carga iniciada y un objeto subido dentro de su autorización temporal
- **WHEN** el cliente solicita confirmar el archivo
- **THEN** el backend verifica que el objeto existe en el bucket y clave esperados, calcula o valida su hash y pasa el archivo al estado de revisión

#### Scenario: Autorización de subida expirada

- **GIVEN** una autorización de subida vencida o ya utilizada
- **WHEN** se intenta subir o confirmar el archivo
- **THEN** la operación es rechazada y el archivo no puede asociarse a una entidad de negocio

### Requirement: Validación de contenido y cuarentena

El backend SHALL admitir como mínimo documentos PDF e imágenes JPEG o PNG según el propósito autorizado. MUST validar el contenido real mediante firma o parser, tamaño máximo configurable, MIME detectado y hash SHA-256. Todo archivo nuevo SHALL permanecer en cuarentena hasta completar un análisis antivirus; un archivo inválido, infectado o incompatible MUST quedar rechazado y no ser descargable.

#### Scenario: PDF válido

- **GIVEN** una carga declarada como PDF cuyo contenido es un PDF válido y pasa el análisis antivirus
- **WHEN** el backend confirma la carga
- **THEN** el archivo queda en estado `disponible` y puede asociarse al propósito permitido

#### Scenario: Imagen con extensión falsa

- **GIVEN** un archivo con extensión `.pdf` cuyo contenido real es una imagen o datos no reconocidos
- **WHEN** el backend valida la carga
- **THEN** la carga es rechazada por incompatibilidad de contenido y no se publica

#### Scenario: Archivo infectado

- **GIVEN** el análisis antivirus detecta contenido malicioso
- **WHEN** finaliza la revisión
- **THEN** el archivo queda en estado `rechazado`, no se puede descargar ni asociar y se registra el motivo sin almacenar el contenido en logs

### Requirement: Asociación atómica con Designaciones y Portal

Las mutaciones de Designaciones y Portal SHALL recibir referencias a archivos confirmados mediante `archivoId`. El backend MUST validar que el archivo esté disponible, pertenezca al actor o entidad autorizada y corresponda al propósito requerido antes de confirmar la mutación. Una mutación rechazada MUST no dejar asociaciones parciales ni archivos confirmados sin dueño lógico.

#### Scenario: Alta con documentación completa

- **GIVEN** un Alta con archivos disponibles para CV, DNI frente y DNI dorso
- **WHEN** el Jefe de Cátedra guarda o envía el pedido
- **THEN** la API asocia exactamente esos archivos al pedido y conserva sus metadata

#### Scenario: Alta con archivo pendiente

- **GIVEN** un Alta que referencia un archivo aún pendiente, rechazado o perteneciente a otro propósito
- **WHEN** se intenta guardar o enviar
- **THEN** la API rechaza la operación sin modificar el pedido ni su historial

#### Scenario: Reemplazo de CV

- **GIVEN** un docente tiene un CV disponible y carga otro que supera la validación
- **WHEN** confirma el reemplazo
- **THEN** el perfil referencia solo el nuevo archivo y el anterior queda desvinculado según la política de retención

#### Scenario: Proyecto con documento opcional

- **GIVEN** un proyecto con DOI, con PDF, con ambos o sin documentación
- **WHEN** el docente guarda el proyecto
- **THEN** la API conserva la combinación solicitada y solo vincula un archivo cuando está disponible y validado

### Requirement: Descarga autorizada y URLs temporales

Las descargas SHALL validar autenticación, autorización por propietario, pedido, rol y ámbito antes de devolver contenido. El sistema MUST entregar el archivo mediante streaming del backend o una URL firmada de corta duración. Las respuestas y metadata visibles NO SHALL exponer claves internas, credenciales, buckets ni URLs permanentes.

#### Scenario: Descarga autorizada

- **GIVEN** un actor que puede consultar el pedido o perfil y un archivo disponible asociado
- **WHEN** solicita la descarga
- **THEN** recibe el contenido con nombre y MIME validados o una URL firmada de corta duración

#### Scenario: Descarga fuera de ámbito

- **GIVEN** un actor sin acceso al pedido, perfil o ámbito del archivo
- **WHEN** solicita la descarga usando el `archivoId`
- **THEN** recibe una respuesta de recurso no disponible o no autorizado sin confirmar la existencia del objeto

#### Scenario: Archivo no disponible

- **GIVEN** un archivo pendiente, rechazado, eliminado o sin asociación válida
- **WHEN** se solicita la descarga
- **THEN** el sistema no devuelve bytes ni URL firmada

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

### Requirement: Operación, backup y recuperación

La infraestructura SHALL provisionar SeaweedFS con volumen persistente, credenciales inyectadas en runtime y procedimientos versionados para crear buckets, políticas y backups. Los ambientes descartables SHALL poder reconstruir sus objetos junto con la base y el seed sintético. Producción MUST tener un backup verificable de los objetos y una prueba documentada de restauración antes de considerarse operativa.

#### Scenario: Provisionamiento repetible

- **WHEN** se aprovisiona un ambiente nuevo
- **THEN** se crean su bucket, política y credenciales de mínimo privilegio sin almacenar secretos en el repositorio

#### Scenario: Restauración de un backup

- **GIVEN** un backup válido de los objetos y metadata del ambiente
- **WHEN** se restaura sobre un entorno de recuperación
- **THEN** los archivos disponibles vuelven a ser descargables con la misma metadata e integridad verificable

#### Scenario: Fallo de almacenamiento

- **GIVEN** SeaweedFS no está disponible durante una mutación
- **WHEN** el backend intenta confirmar o asociar un archivo
- **THEN** la operación falla de forma explícita, no confirma la mutación ni deja el pedido en un estado que aparente tener documentación disponible

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
