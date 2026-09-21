## 1. Contrato y fronteras

- [x] 1.1 Crear `ArsDocendi.Storage.Contracts` con DTOs para iniciar, confirmar, consultar y eliminar archivos; verificar que no contenga I/O ni dependencias del SDK de MinIO.
- [x] 1.2 Crear `ArsDocendi.Storage` con la abstracción `IAlmacenamientoArchivos` y registrar la implementación por DI; verificar que Designaciones y Portal solo referencien Contracts y que el análisis de arquitectura no detecte referencias internas cruzadas.
- [x] 1.3 Definir los propósitos permitidos (`cv`, `dni_frente`, `dni_dorso`, `justificativo`, `documento_proyecto`) y políticas de MIME/tamaño; verificar que un propósito o MIME no admitido produzca un error estable.

## 2. Schema y metadata

- [x] 2.1 Agregar el SQL versionado del schema `storage.archivos`, índices, estados y `audit.attach`; verificar migración desde base limpia y ausencia de cambios pendientes de modelo.
- [x] 2.2 Agregar referencias `archivo_id` a `designaciones.pedido_adjuntos`, `portal.cvs` y `portal.proyecto_documentos` preservando metadata legacy; verificar que el modelo no borre registros históricos ni cree asociaciones parciales.
- [x] 2.3 Actualizar DTOs y contratos para reemplazar URI controladas por el cliente con `archivoId` y metadata de lectura; verificar compilación de todos los consumidores y Problem Details para referencias inválidas.

## 3. MinIO e infraestructura

- [x] 3.1 Agregar el Compose project privado de MinIO con volumen persistente y sin conexión a `traefik`; verificar con `docker compose config` que no publique puertos públicos ni la consola.
- [x] 3.2 Implementar provisionamiento idempotente de buckets, políticas y credenciales por ambiente; verificar aislamiento entre producción, staging y un preview con una prueba ejecutable de permisos.
- [x] 3.3 Integrar endpoint, bucket, credenciales y límites como variables de runtime; verificar que ningún secreto aparezca en archivos versionados, imágenes o logs.
- [x] 3.4 Extender `spin-up.sh`, `teardown.sh` y la reconstrucción de staging/PR para crear o eliminar solo los objetos del ambiente indicado; verificar que producción nunca alcance reset ni seed de archivos.
- [x] 3.5 Documentar backup, restore, capacidad y prueba de recuperación en el runbook de infraestructura; verificar restauración de un objeto y su metadata en un entorno descartable.

## 4. Carga, validación y seguridad

- [x] 4.1 Implementar inicio de sesión de carga con key generada por servidor y URL firmada de expiración corta; verificar rechazo de URI, bucket, key o propósito enviados por el cliente.
- [x] 4.2 Implementar confirmación idempotente con verificación de existencia, tamaño, SHA-256 y MIME real; verificar reintentos, expiración, objeto ausente y hash incorrecto.
- [x] 4.3 Integrar cuarentena y análisis antivirus interno; verificar que PDFs e imágenes válidos lleguen a `disponible` y que contenido inválido o infectado no pueda descargarse ni asociarse.
- [x] 4.4 Implementar descargas autorizadas por streaming o URL firmada temporal; verificar propietario, pedido, rol, ámbito, recurso inexistente y que no se filtren bucket, key ni URLs permanentes.
- [x] 4.5 Implementar limpieza idempotente de cargas pendientes, objetos huérfanos y archivos desvinculados según retención; verificar que no elimine objetos asociados.

## 5. Integración con Designaciones

- [x] 5.1 Cambiar la creación y edición de pedidos para recibir `archivoId` y asociar solo archivos disponibles del propósito correcto; verificar Alta con CV/DNI, Baja con justificativo y rechazo de archivos pendientes o ajenos.
- [x] 5.2 Mantener las reglas de novedad, historial, idempotencia y rollback transaccional cuando falle MinIO o la asociación; verificar que una falla de storage no deje pedido ni historial parcial.
- [x] 5.3 Adaptar el detalle y descarga de adjuntos de pedidos; verificar que el actor vea metadata permitida y solo pueda descargar dentro de su ámbito.

## 6. Integración con Portal

- [x] 6.1 Cambiar CV y documentos de proyectos para usar sesiones de carga y `archivoId`, manteniendo DOI opcional y reemplazo/eliminación del CV; verificar CRUD y autorización por propietario.
- [x] 6.2 Adaptar frontend Portal para cargar, confirmar, mostrar estados de validación y descargar sin exponer URI de MinIO; verificar errores recuperables y rollback visual tras rechazo.
- [x] 6.3 Actualizar seed sintético para cargar fixtures PDF/JPEG/PNG pequeñas en ambientes no productivos; verificar reejecución idempotente y ausencia de binarios productivos.

## 7. Legacy, documentación y contratos

- [x] 7.1 Clasificar registros con URI histórica sin bytes y evitar descargas falsas; verificar que la UI los muestre como metadata legacy sin inventar archivos.
- [x] 7.2 Actualizar `api-contracts`, `data-model`, dominios, grafo de dependencias y runbook con el contrato de archivos, referencias y límites; verificar que no quede una documentación que prometa almacenamiento binario en PostgreSQL.
- [x] 7.3 Documentar la política de PII, retención, nombres de archivos, auditoría, backup y restauración; verificar que los logs y respuestas no contengan bytes, credenciales ni claves internas.

## 8. Verificación integral

- [x] 8.1 Agregar tests unitarios de MIME, tamaño, hash, propósito, estados y sanitización; verificar casos válidos, falsos y límites.
- [x] 8.2 Agregar tests de integración con MinIO efímero para cargar, confirmar, escanear, asociar, descargar, reemplazar y limpiar; verificar aislamiento por ambiente y reintentos idempotentes.
- [x] 8.3 Ejecutar `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check` y `openspec validate --all --strict`; registrar resultados y cualquier limitación del entorno.
- [x] 8.4 Ejecutar `git diff --check` y revisar `git status --short`; verificar que los cambios ajenos preexistentes permanezcan intactos y que no se hayan agregado secretos ni binarios al repositorio.
