## Why

Los adjuntos de las novedades de designaciones y los documentos del Portal se persisten hoy como metadata y URI, sin un almacenamiento de bytes administrado por el backend. Esto deja sin resolver la carga real de PDFs e imágenes sensibles —CV, DNI y justificativos—, impide validar integridad y antivirus, y permite que el cliente declare una URI que el sistema no controla.

## What Changes

- Incorporar una capacidad de almacenamiento privado de archivos basada en MinIO self-hosted y aislada por ambiente.
- Agregar un ciclo de carga explícito: iniciar, subir, confirmar, validar y asociar el archivo al dominio.
- Reemplazar las URI arbitrarias de adjuntos por identificadores de archivo emitidos por el backend.
- Persistir metadata, hash, tamaño, MIME detectado, estado de revisión y referencia al objeto; los bytes no vivirán en PostgreSQL.
- Validar PDFs e imágenes por contenido real, tamaño, hash y análisis antivirus antes de hacerlos disponibles.
- Exponer descargas autorizadas mediante streaming backend o URLs firmadas de corta duración, sin buckets públicos.
- Aislar buckets, credenciales y limpieza por ambiente en local, staging, previews y producción.
- Mantener los registros legacy que solo tienen metadata sin inventar archivos retroactivamente; documentar la migración y el tratamiento de esos registros.
- Actualizar contratos, seed sintético, infraestructura, auditoría, backups y pruebas de Designaciones y Portal.

## Capabilities

### New Capabilities

- `almacenamiento-adjuntos`: ciclo de vida, seguridad, validación, autorización y operación del almacenamiento privado de archivos.

### Modified Capabilities

- `pedidos-designacion`: los adjuntos obligatorios de Alta y Baja deben referenciar archivos confirmados y disponibles, no URI arbitrarias.
- `portal-docente-api`: CV y documentos de proyectos dejan de ser solo metadata/URI y pasan a referenciar objetos privados administrados por el almacenamiento.
- `cv-docente`: la carga, reemplazo y eliminación del CV deben operar sobre un archivo real almacenado de forma privada.
- `trayectoria-docente`: la documentación PDF de proyectos debe operar sobre un archivo real almacenado de forma privada, manteniendo DOI opcional.

## Impact

- **Backend:** nuevo contrato y proveedor de almacenamiento; endpoints de sesión de carga, confirmación y descarga; validación, autorización y limpieza.
- **Base de datos:** metadata de archivos y referencias desde `designaciones.pedido_adjuntos`, `portal.cvs` y `portal.proyecto_documentos`; migración SQL auditada.
- **Infraestructura:** servicio MinIO privado, volumen persistente, buckets por ambiente, políticas de acceso, secretos de runtime, backup y teardown de previews.
- **Frontend:** adaptación de payloads y flujo de carga; no podrá enviar URI ni marcar un archivo como confirmado por sí mismo.
- **Dependencias cross-module:** Designaciones y Portal consumirán contratos de almacenamiento sin referenciar implementaciones internas de otro módulo.
- **Seguridad:** tratamiento de PII documental, antivirus, URLs temporales, logs sin contenido sensible y recuperación ante fallos.
