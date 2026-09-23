## Context

Ver `proposal.md` para la motivación. El código actual ya separa el dominio del proveedor mediante `IProveedorObjetos`, pero `ModuleExtensions` registra el cliente MinIO, el proveedor usa tipos de excepción del SDK de MinIO, los tests usan `Testcontainers.Minio` y la infraestructura opera el proveedor anterior mediante `mc`, buckets y un volumen persistente.

El change anterior mantiene la metadata y las asociaciones en PostgreSQL y los bytes fuera de la base. Designaciones y Portal reciben `archivoId`, mientras el backend valida tamaño, MIME, hash, antivirus, autorización y estado antes de permitir asociaciones o descargas.

## Goals / Non-Goals

**Goals:**

- Sustituir MinIO por SeaweedFS sin cambiar los contratos HTTP ni las reglas de archivos.
- Mantener almacenamiento privado, aislamiento por ambiente y mínimo privilegio.
- Eliminar el acoplamiento de producción y tests con el SDK de MinIO.
- Proveer una instalación Compose reproducible con volumen nuevo y configuración runtime.
- Mantener la posibilidad de probar el proveedor con operaciones S3 básicas.
- Dejar restaurables PostgreSQL y los objetos SeaweedFS desde un entorno limpio.

**Non-Goals:**

- No migrar objetos ni metadata binaria existente desde el volumen MinIO.
- No copiar datos productivos a staging o previews.
- No cambiar los endpoints, DTOs, estados, validaciones, autorización ni antivirus.
- No rediseñar en este change el flujo backend-mediado de carga y descarga.
- No introducir un clúster SeaweedFS distribuido ni resolver alta disponibilidad más allá del despliegue actual de un servicio persistente por scope (prod y no-prod).
- No conservar formatos internos ni intentar montar el volumen de MinIO con SeaweedFS.

## Decisions

### 1. Mantener la frontera `IProveedorObjetos`

`Designaciones`, `Portal` y `ServicioAlmacenamientoArchivos` continuarán consumiendo contratos existentes. Se reemplazará únicamente la implementación concreta y su registro DI. El proveedor nuevo deberá traducir ausencia de objeto a `null` y conservar las operaciones de subir, obtener, listar y eliminar.

**Alternativas consideradas:** modificar los servicios de dominio para llamar directamente a S3 o SeaweedFS. Se descartan porque romperían la frontera de módulos y aumentarían el coste de futuras migraciones.

### 2. Usar una API S3 estándar contra SeaweedFS

La implementación concreta usará un cliente S3 estándar para .NET, configurado con endpoint explícito, credenciales runtime y addressing compatible con el despliegue de SeaweedFS. No se conservará el paquete `Minio` solo como cliente genérico.

El cliente se configurará con path-style cuando sea necesario para el endpoint interno y validará de forma explícita región, SSL y timeout. Las excepciones del SDK se traducirán dentro del proveedor para que el resto de la aplicación no dependa de tipos externos.

**Alternativas consideradas:** mantener `MinioClient` apuntando a SeaweedFS o introducir un SDK propietario de SeaweedFS. Se descartan porque mantienen acoplamiento semántico a MinIO o a otro proveedor y no mejoran la portabilidad S3.

### 3. Topología híbrida SeaweedFS y ClamAV privado

El Compose usa dos proyectos de SeaweedFS sobre la red interna `arsdocendi-datos`:

- `arsdocendi-storage-prod`: instancia y volumen exclusivos de producción,
  alias `seaweedfs-prod`.
- `arsdocendi-storage-nonprod`: instancia y volumen compartidos por `staging`
  y todos los `pr-N`, alias `seaweedfs-nonprod`.

ClamAV se ejecuta una sola vez en `arsdocendi-antivirus-shared`, con alias
`clamav-shared`, y es consumido por prod, staging y previews. Ningún servicio
publica puertos ni se conecta a Traefik o Cloudflare Tunnel.

Cada ambiente mantiene un bucket y una credencial de aplicación propios. El
backend sólo recibe su endpoint, bucket y credencial; nunca recibe la identidad
administrativa. En el SeaweedFS compartido, `weed shell s3.configure` registra
y elimina identidades dinámicamente sin reiniciar la instancia, limitando cada
identidad a sus acciones y bucket.

La separación por proyectos y volúmenes protege producción, mientras el storage
no productivo conserva el modelo eficiente de servicio compartido. El teardown
de un preview elimina sólo su bucket y su identidad; jamás ejecuta `down -v`
sobre el proyecto no-prod compartido.

**Alternativas consideradas:** un servicio completo por ambiente, que aumenta
el consumo y duplica ClamAV, y un único servicio para todos los ambientes, que
amplía el impacto de un error administrativo sobre producción. El modelo
híbrido conserva una frontera fuerte para prod sin multiplicar servicios de
previews.
Las credenciales se mantendrán fuera del repositorio. Los nombres públicos de configuración serán neutrales al proveedor; la identidad administrativa solo se inyecta en SeaweedFS y en los comandos efímeros de provisionamiento/backup.

### 5. Reinicio limpio, sin migración de objetos

El despliegue nuevo usará un volumen SeaweedFS distinto. No habrá `mirror`, dual-write, importación de objetos ni conversión de metadatos internos. En staging y previews, `spin-up`, seed y teardown reconstruirán el bucket y las fixtures junto con la base del ambiente.

Producción conservará su protección contra operaciones destructivas. El rollout debe validar SeaweedFS antes de retirar el volumen MinIO anterior; conservarlo durante la ventana de rollback no implica migrar sus objetos.

Si un ambiente reconstruye también su PostgreSQL, se eliminarán las filas de metadata asociadas junto con la base para no dejar `archivo_id` apuntando a un objeto inexistente. El esquema y sus migraciones no cambian.

### 6. Preservar el protocolo HTTP actual

El backend seguirá recibiendo la carga, almacenándola temporalmente, enviándola al proveedor y ejecutando confirmación, validación y antivirus. Las descargas seguirán pasando por autorización del backend y streaming.

El diseño anterior menciona URLs presignadas, pero la implementación actual usa rutas backend. Esta migración no mezclará ambos cambios; una futura optimización de streaming o presignado deberá tener su propio change.

### 7. Tests como contrato de proveedor

Los tests existentes de `AlmacenamientoSeaweedFsTests` se ejecutan contra un contenedor SeaweedFS genérico fijado por digest (`4.47`, digest `sha256:ce9e796f…`). La configuración S3 se copia al contenedor mediante Testcontainers, evitando depender de bind mounts del workspace. Se cubrirán como mínimo:

- subida y lectura;
- ausencia de objeto;
- listado por prefijo;
- eliminación idempotente;
- aislamiento de buckets;
- hash, MIME, tamaño y antivirus;
- autorización y asociación Portal/Designaciones;
- limpieza de pendientes y huérfanos;
- reinicio limpio con bucket vacío.

La suite debe validar primero la operación S3 del contenedor y luego el servicio de archivos. Un fallo de autenticación no se interpretará como fallo de política.

## Risks / Trade-offs

- **[Riesgo] Diferencias en credenciales o políticas S3** → validar provisionamiento con una credencial de aplicación y pruebas negativas contra otro bucket antes de habilitar el ambiente.
- **[Riesgo] Comando o imagen SeaweedFS incompatible con el endpoint esperado** → fijar versión/digest, ejecutar healthcheck y probar `put/stat/get/delete` antes de migrar el backend.
- **[Riesgo] Objetos antiguos quedan fuera del nuevo volumen** → declarar explícitamente el reinicio limpio, no presentar metadata vieja como descargable y conservar el volumen MinIO solo para rollback durante la ventana acordada.
- **[Riesgo] Eliminación incompleta de variables MinIO** → buscar referencias en código, Compose, scripts, workflows y documentación; el gate de CI debe fallar si reaparecen nombres obsoletos fuera de notas históricas.
- **[Riesgo] Pérdida de aislamiento entre ambientes** → una identidad dinámica por bucket, acciones explícitas `Read,Write,List,Tagging`, prueba negativa cruzada y prohibición de credenciales administrativas en el backend.
- **[Riesgo] Purge de un preview afecta storage compartido** → `purge-storage.sh` sólo borra el bucket y la identidad destino; nunca ejecuta `down -v` sobre `arsdocendi-storage-nonprod`.
- **[Riesgo] ClamAV compartido no disponible** → healthcheck único, bloqueo de cargas según `RechazarSiAntivirusNoDisponible` y monitoreo del proyecto `arsdocendi-antivirus-shared`.
- **[Trade-off] SeaweedFS compartido en no-prod** → reduce consumo frente a una instancia por PR, pero exige pruebas estrictas de políticas y lifecycle de identidades.
- **[Trade-off] SeaweedFS dedicado para prod** → consume un servicio/volumen adicional, pero evita que un error administrativo no-prod alcance producción.
- **[Trade-off] Cliente S3 estándar y AWS CLI efímero** → agregan dependencias nuevas, pero eliminan el acoplamiento directo a MinIO y facilitan futuros proveedores.

## Migration Plan

1. Fijar SeaweedFS `4.47` con el digest `sha256:ce9e796f…` y validar localmente su endpoint S3, healthcheck, persistencia y configuración de credenciales.
2. Implementar el proveedor S3 para SeaweedFS detrás de `IProveedorObjetos` y eliminar el SDK MinIO.
3. Reemplazar Compose, opciones, variables, provisionamiento, seed, purge, teardown y backup/restore.
4. Cambiar los tests de Testcontainers a SeaweedFS y ejecutar la suite de almacenamiento.
5. Levantar un ambiente limpio no productivo, aplicar migraciones, sembrar fixtures y ejecutar smoke tests HTTP.
6. Verificar que las descargas, asociaciones, limpieza y antivirus mantienen el comportamiento actual.
7. Validar la topología híbrida: prod con volumen dedicado, staging/pr-N con SeaweedFS no-prod compartido y ClamAV compartido; ejecutar pruebas negativas de buckets y credenciales.
8. Verificar que `purge-storage.sh` y `teardown.sh` no detengan ni eliminen el proyecto compartido al destruir un preview.
9. Tras la aceptación del despliegue limpio, retirar referencias operativas obsoletas y eliminar volúmenes antiguos según la política decidida por ambiente.

**Rollback:** detener el despliegue SeaweedFS, restaurar la imagen/Compose y secretos del proveedor anterior y volver a conectar el volumen MinIO conservado. No se intentará reutilizar un volumen SeaweedFS con MinIO ni viceversa.

## Open Questions

No quedan decisiones técnicas abiertas para la implementación local. La versión,
digest, formato S3 y estrategia de aislamiento ya están fijados en este diseño.
La migración de producción y la retención/eliminación de cualquier volumen del
proveedor anterior siguen siendo gates operativos fuera de esta ejecución local.
