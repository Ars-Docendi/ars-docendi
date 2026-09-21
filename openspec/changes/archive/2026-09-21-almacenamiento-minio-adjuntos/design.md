## Context

El cambio parte del modelo actual donde `designaciones.pedido_adjuntos`, `portal.cvs` y `portal.proyecto_documentos` guardan nombres y URI, mientras los bytes no tienen un dueño operativo. Los ambientes ya usan Docker Compose, una red interna `arsdocendi-datos`, PostgreSQL por ambiente lógico y secretos inyectados en runtime. La solución debe respetar la frontera `Controller -> Service -> Repository`, no agregar I/O a `ArsDocendi.Shared` fuera de `identity`/`audit` y mantener aislados los módulos.

## Goals / Non-Goals

**Goals:**

- Incorporar MinIO self-hosted como proveedor privado de objetos.
- Permitir cargas directas con autorizaciones temporales y confirmación backend.
- Mantener en PostgreSQL solo metadata, estados y asociaciones de dominio.
- Validar PDF/JPEG/PNG, tamaño, hash y antivirus antes de disponibilidad.
- Aislar buckets y credenciales por ambiente.
- Exponer descargas autorizadas sin URLs permanentes ni claves internas.
- Soportar limpieza de cargas abandonadas, backups y restauración.
- Mantener compatibilidad legible con registros históricos que solo contienen metadata.

**Non-Goals:**

- No se implementará en este cambio un repositorio de archivos público.
- No se almacenarán binarios dentro de PostgreSQL.
- No se incorporará OCR, compresión avanzada, edición de PDF ni generación de thumbnails como requisito de negocio.
- No se migrarán mágicamente URI históricas a objetos: solo se migrarán filas cuya fuente binaria esté disponible y autorizada.
- No se resolverá la autenticación institucional Azure AD; se reutilizará la autenticación existente.

## Decisions

### 1. Frontera de almacenamiento

Se crearán `ArsDocendi.Storage.Contracts` y `ArsDocendi.Storage` como biblioteca de infraestructura transversal, no como módulo de dominio. Los consumidores Designaciones y Portal referenciarán únicamente Contracts; el Host registrará la implementación MinIO por DI. `ArsDocendi.Shared` no recibirá esta I/O porque su única I/O permitida es identity/audit.

El contrato expondrá operaciones de iniciar carga, confirmar, abrir descarga autorizada, asociar/desasociar y eliminar. Los DTOs de dominio recibirán `archivoId`, nunca bucket, key, URL ni credenciales.

### 2. MinIO y topología por ambiente

Se desplegará una instancia MinIO privada en un Compose project de infraestructura dedicado, conectada solo a `arsdocendi-datos`, con volumen persistente. No se conectará a la red pública `traefik` y no se publicarán sus puertos ni consola mediante Cloudflare.

Cada ambiente tendrá un bucket separado, por ejemplo `arsdocendi-prod`, `arsdocendi-staging` y `arsdocendi-pr-123`, junto con una credencial y política de mínimo privilegio. Las claves de objetos serán UUIDs o rutas derivadas de UUIDs, nunca nombres o documentos personales.

La política de destrucción será explícita: staging y previews pueden reconstruirse y purgar sus objetos; producción no será alcanzada por reset ni seed.

### 3. Metadata y estados

Se incorporará el schema `storage` con una tabla `storage.archivos` auditada. Como mínimo conservará:

- `id`, propósito y ambiente;
- bucket/key internos;
- nombre original sanitizado;
- MIME declarado y MIME detectado;
- tamaño en bytes y SHA-256;
- estado `pendiente`, `cuarentena`, `disponible`, `rechazado` o `eliminado`;
- timestamps y datos de revisión sin contenido sensible.

Las tablas de dominio conservarán sus datos propios (`tipo` en adjuntos de pedidos y la cardinalidad CV/documento de proyecto) y referenciarán el `archivoId` mediante referencia lógica. No se introducirán FKs cross-schema salvo que una prueba de integridad y el modelo final lo justifiquen; la asociación se validará mediante el contrato de Storage.

Las URI actuales se conservarán solo como información legacy durante la transición, sin convertirlas automáticamente en destinos descargables.

### 4. Protocolo de carga

1. El cliente pide una sesión de carga indicando propósito, nombre, MIME y tamaño.
2. El backend valida actor, propósito, límites y extensión permitida; crea metadata `pendiente`.
3. MinIO entrega una URL firmada de PUT con expiración corta y key generada por el servidor.
4. El cliente sube directamente el objeto.
5. El cliente confirma con `archivoId` y, si corresponde, hash/tamaño observados.
6. El backend verifica existencia, tamaño, hash, MIME real y antivirus.
7. Solo después pasa a `disponible` y puede asociarse al pedido o perfil.

La confirmación será idempotente. Un objeto sin confirmación quedará sujeto a limpieza. Las mutaciones de dominio no aceptarán un archivo que no esté `disponible` y autorizado para su propósito.

### 5. Validación y antivirus

El backend realizará validaciones estructurales antes de asociar un archivo:

- PDF válido para CV, justificativo y documento de proyecto.
- JPEG o PNG válido para imágenes de DNI.
- límites de tamaño configurables por propósito;
- MIME detectado por contenido, no solo por extensión;
- SHA-256 para integridad e idempotencia;
- análisis antivirus en cuarentena, inicialmente mediante un servicio interno compatible con ClamAV.

Los resultados negativos serán consumibles por formularios y no expondrán detalles innecesarios del scanner.

### 6. Descarga y autorización

El backend resolverá primero autorización por propietario, pedido, etapa y ámbito. Solo entonces devolverá streaming o una URL firmada GET de corta duración. El cliente nunca verá credenciales, bucket, key ni una URL estable de MinIO.

La descarga se mantendrá en los endpoints de dominio cuando eso simplifique autorización. El endpoint transversal de Storage solo operará con una autorización interna ya resuelta o con un contrato que incluya el contexto autorizado.

### 7. Infraestructura y secretos

Se agregarán variables de runtime para endpoint interno, bucket, access key, secret key, expiraciones y límites. Los secretos provendrán de `.env` local ignorado o GitHub Actions secrets; no se escribirán en Compose versionado ni en imágenes.

Los scripts de provisionamiento crearán buckets y políticas de forma idempotente. `spin-up.sh` deberá asegurar el bucket antes de migraciones y seed; `teardown.sh` y `drop-db.sh` coordinarán la eliminación de objetos del ambiente descartable. Los backups de MinIO serán independientes del dump de PostgreSQL y deberán poder restaurarse en un entorno de prueba.

### 8. Migración de datos históricos

Los registros actuales con `synthetic://` u otras URI metadata no recibirán bytes inventados. Se clasificarán como legacy y su UI mostrará disponibilidad limitada. Si existe una fuente autorizada de bytes, se podrá ejecutar una migración explícita que cargue el objeto, calcule metadata y vincule el `archivoId`; esa migración no formará parte del arranque normal.

### 9. Alternativas consideradas

- **Filesystem/NFS/ZFS:** menor complejidad inicial, pero acopla el dominio a paths y complica escalado y URLs temporales.
- **PostgreSQL `bytea`/Large Objects:** transaccional y simple para un prototipo, pero aumenta backups/WAL y escala peor con PII documental.
- **Azure Blob o R2:** opciones válidas si se decide contratar almacenamiento externo; la abstracción de Contracts permitirá reemplazar MinIO posteriormente.

Se elige MinIO porque encaja con Proxmox/Docker, mantiene los datos bajo control institucional y usa un protocolo portable.

## Risks / Trade-offs

- **[Riesgo] Pérdida del volumen MinIO** → backups periódicos, prueba de restauración y monitoreo de capacidad.
- **[Riesgo] Credencial con acceso entre ambientes** → una política y credencial por bucket, sin credenciales administrativas en la aplicación.
- **[Riesgo] Archivo malicioso o MIME falso** → cuarentena, validación por contenido y antivirus antes de `disponible`.
- **[Riesgo] Objetos huérfanos por fallos entre upload y DB** → expiración de sesiones, reconciliación periódica y confirmación idempotente.
- **[Riesgo] Enumeración de PII por nombres o URLs** → keys UUID, nombres sanitizados y respuestas indistinguibles para recursos fuera de ámbito.
- **[Trade-off] Un MinIO compartido por ambientes** → se compensa con buckets, políticas, credenciales y procesos de reset aislados.

## Migration Plan

1. Crear contratos, schema `storage`, migraciones y políticas de autorización.
2. Levantar MinIO privado en local y crear buckets de desarrollo.
3. Implementar sesión de carga, confirmación, validación y descarga.
4. Adaptar Designaciones y Portal para usar `archivoId`.
5. Adaptar frontend y seed para cargar fixtures reales pequeñas en ambientes no productivos.
6. Ejecutar migraciones y pruebas de integración con MinIO efímero.
7. Configurar staging, backup y restauración antes de producción.
8. Habilitar la nueva ruta solo cuando el proveedor esté disponible; ante fallo de carga, rechazar de forma explícita sin confirmar mutaciones.

Rollback: mientras no haya asociaciones nuevas, se puede deshabilitar el proveedor y volver a mostrar metadata legacy. Una vez que existan archivos nuevos, el rollback debe conservar el schema y el volumen; retirar solo endpoints dejaría datos inaccesibles, por lo que se requiere exportar metadata y objetos antes de revertir.

## Open Questions

No quedan decisiones de arquitectura bloqueantes para la revisión. Los límites exactos por propósito, expiraciones, retención y frecuencia de backup serán configuración explícita y deberán ser aprobados antes de producción; no cambian la frontera ni el protocolo definidos aquí.
