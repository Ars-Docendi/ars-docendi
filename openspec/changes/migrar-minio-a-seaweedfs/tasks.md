## 1. Fijar la superficie técnica

- [x] 1.1 Seleccionar SeaweedFS `4.47` con digest fijado; verificarlo con un contenedor limpio y operaciones `put`, `stat`, `get` y `delete`.
- [x] 1.2 Confirmar el formato de identidades, credenciales y políticas S3 de la versión elegida; verificar credenciales de aplicación, aislamiento de bucket y rechazo de acceso cruzado.
- [x] 1.3 Añadir el cliente S3 estándar para .NET y retirar el paquete `Minio` de producción y tests; verificar restore, build y ausencia de referencias compilables al namespace `Minio`.

## 2. Proveedor de objetos del backend

- [x] 2.1 Implementar el proveedor SeaweedFS detrás de `IProveedorObjetos`; verificar subida, obtención, listado por prefijo, eliminación y ausencia de objeto mediante tests de integración.
- [x] 2.2 Configurar endpoint, SSL, addressing, credenciales, bucket y ambiente con opciones neutrales al proveedor; verificar que producción/staging fallen explícitamente cuando falte configuración obligatoria y que Development use credenciales dummy aisladas para tests sin storage.
- [x] 2.3 Traducir errores del cliente S3 a la semántica actual (`null` para objeto inexistente y errores de aplicación para fallos operativos); verificar que ningún tipo de excepción de MinIO atraviese `ArsDocendi.Storage`.
- [x] 2.4 Mantener sin cambios los contratos HTTP, estados de archivo, validación de MIME/tamaño/hash, antivirus, autorización, asociaciones y streaming; verificar build y suite backend completa.

## 3. Compose y operación del almacenamiento

- [x] 3.1 Reemplazar `compose.storage.yml` por servicios SeaweedFS/ClamAV persistentes, internos y sin publicación en Traefik; verificar `docker compose config`, volumen nuevo, red `arsdocendi-datos` y ausencia de puertos públicos.
- [x] 3.2 Reemplazar `mc` y `mc admin` por AWS CLI contra S3/SeaweedFS; verificar provisionamiento idempotente, operaciones S3 y rechazo de operaciones destructivas sobre `prod`.
- [x] 3.3 Renombrar variables y secretos específicos de MinIO a nombres SeaweedFS/neutrales y actualizar `.env.example`, workflows y documentación; verificar que no queden referencias operativas obsoletas fuera del change histórico.
- [x] 3.4 Actualizar `spin-up.sh`, `teardown.sh` y el reprovisionamiento de ambientes para crear buckets y fixtures desde cero; verificar reset y cleanup completo de un preview descartable.
- [x] 3.5 Actualizar backup y restore para el endpoint S3 de SeaweedFS; verificar una restauración operativa completa de objetos y metadata en un entorno descartable con comparación de SHA-256, tamaño, content type y metadata S3.

## 4. Tests de integración

- [x] 4.1 Reemplazar `Testcontainers.Minio` por un contenedor SeaweedFS genérico con healthcheck, persistencia temporal y configuración de credenciales; verificar arranque y destrucción del fixture.
- [x] 4.2 Adaptar los tests de almacenamiento a SeaweedFS y conservar cobertura de carga, confirmación, hash, MIME, antivirus, autorización, asociación, descarga, reemplazo y limpieza; verificar 11/11 tests.
- [x] 4.3 Verificar aislamiento negativo entre buckets y credenciales, y reinicio limpio sin objetos previos; confirmar que no se descargue metadata sin objeto ni se acceda a otro ambiente.
- [x] 4.4 Ejecutar la suite backend completa y las pruebas frontend relacionadas con adjuntos; verificar 169/169 tests backend y 246/246 tests frontend, además de lint y build frontend.

## 5. Documentación y validación final

- [x] 5.1 Actualizar runbook, deploy de GitHub PR, variables runtime, workflows y referencias operativas para describir SeaweedFS, sus secretos, backup y rollback; verificar que no mencionen comandos `mc` ejecutables.
- [x] 5.2 Revisar contratos API, modelo de datos y límites de módulos; verificar que no se hayan añadido cambios de schema ni referencias cross-module nuevas.
- [x] 5.3 Ejecutar `bash -n infra/scripts/*.sh`, `docker compose --env-file infra/compose/.env.example -f infra/compose/compose.storage.yml config`, build backend, `dotnet vstest` de la suite backend, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build` y `pnpm format:check`.
- [x] 5.4 Ejecutar la validación final posterior a este checklist, confirmar ausencia de secretos/binarios productivos y revisar el estado final del working tree.
