## Why

El change `almacenamiento-minio-adjuntos` dejó el almacenamiento privado de archivos acoplado a MinIO en el SDK, el Compose, los scripts de operación y los tests. MinIO dejó de ser una base conveniente para una dependencia estratégica de largo plazo, por lo que Ars Docendi necesita reemplazar el proveedor antes de sumar más comportamiento específico.

La migración no requiere conservar los objetos actuales: los ambientes podrán iniciar con un almacenamiento SeaweedFS vacío y reconstruir sus fixtures sintéticas. Se conserva el contrato funcional de archivos, de modo que Designaciones y Portal no tengan que conocer el proveedor concreto.

## What Changes

- Reemplazar el cliente y proveedor concreto de MinIO por un proveedor SeaweedFS compatible con la interfaz S3.
- Eliminar la dependencia de producción y tests con el SDK y los contenedores especializados de MinIO.
- Mantener `IProveedorObjetos`, `IAlmacenamientoArchivos`, el schema `storage`, los estados de revisión, el antivirus, la autorización y los endpoints públicos actuales.
- Mantener el flujo actual de carga y descarga mediado por el backend; no introducir en este change una migración adicional a URLs presignadas reales.
- Reemplazar el Compose, volumen, healthcheck, provisionamiento de buckets, credenciales, políticas, seed, teardown y backup específicos de MinIO.
- Usar configuración y nombres de secretos neutrales al proveedor, inyectados en runtime y separados por ambiente.
- Mantener un bucket lógico y una credencial por ambiente, con SeaweedFS dedicado para prod, SeaweedFS compartido para staging/pr-N y ClamAV compartido para todos.
- Arrancar prod y el pool no-productivo con volúmenes SeaweedFS nuevos; no implementar copia, dual-write ni reconciliación de objetos MinIO existentes.
- Actualizar los tests de integración para ejecutar contra SeaweedFS efímero y conservar la cobertura actual de almacenamiento.
- Actualizar runbooks, documentación de arquitectura y workflows de CI/CD.
- **BREAKING para operación:** los nombres de secretos, la imagen del servicio y el volumen de almacenamiento cambiarán; ningún cambio rompe los contratos HTTP de Designaciones o Portal.

## Capabilities

### New Capabilities

Ninguna. El change reemplaza una implementación de infraestructura y no introduce una capacidad funcional nueva.

### Modified Capabilities

Ninguna. Los requisitos funcionales de almacenamiento, Portal y Designaciones permanecen sin cambios; las referencias específicas a MinIO pertenecen a la implementación y a la operación. `.openspec.yaml` declara `skip_specs: true`.

## Impact

- **Backend:** `ArsDocendi.Storage`, su registro DI, opciones de runtime, paquete NuGet del proveedor y manejo de errores del cliente S3.
- **Tests:** fixtures de Testcontainers, cliente de prueba y pruebas de aislamiento, carga, confirmación, descarga y limpieza.
- **Infraestructura:** `compose.storage.yml`, scripts compartidos, provisionamiento, seed, teardown, backup/restore y nombres de variables.
- **CI/CD:** secrets documentados e inyectados para staging, producción y previews; no se modifican los gates existentes.
- **Documentación:** arquitectura, runbook de almacenamiento, deploy y onboarding operativo.
- **Datos:** no se migran bytes existentes; el reinicio limpio debe evitar metadata huérfana en los ambientes que también se reconstruyan.
- **Rollback:** conservar el volumen MinIO anterior hasta validar SeaweedFS permite volver al despliegue previo sin intentar convertir formatos internos entre proveedores.
