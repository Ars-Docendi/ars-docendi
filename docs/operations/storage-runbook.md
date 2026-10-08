# Runbook de almacenamiento de archivos

Este runbook describe la operación del almacenamiento privado de archivos de
Ars Docendi. Los bytes viven en SeaweedFS mediante su API S3 y la metadata,
estados y asociaciones viven en PostgreSQL. Un backup válido recupera ambos
lados; respaldar solamente PostgreSQL deja referencias sin bytes.
Ejecutar los procedimientos sobre el host del ambiente: **prod sólo en Debian**;
**staging y pr-N sólo en Proxmox**. Sus bases y objetos son independientes,
sin replicación ni failover. Conservar los datos del prod anterior de Proxmox
durante el corte hasta aceptación y autorización expresa de su tratamiento.

## Límites operativos

- SeaweedFS y ClamAV sólo se exponen en la red interna `arsdocendi-datos`.
- Producción en Debian usa el proyecto `arsdocendi-storage-prod`, alias
  `seaweedfs-prod` y volumen `arsdocendi-seaweedfs-data-prod`.
- En Proxmox, `staging` y todos los `pr-N` comparten el proyecto
  `arsdocendi-storage-nonprod`, alias `seaweedfs-nonprod` y volumen
  `arsdocendi-seaweedfs-data-shared_nonprod`.
- Los ambientes locales usan el proyecto `arsdocendi-antivirus-shared`, alias
  `clamav-shared` y una instancia ClamAV **por host**.
- Cada ambiente mantiene su bucket y su credencial de aplicación; `purge` y
  `teardown` sólo pueden eliminar el bucket/identidad del ambiente descartable.
- Las credenciales se inyectan en runtime. No escribirlas en este documento,
  archivos `.env` versionados, comandos persistidos en tickets ni imágenes.
- `staging` y `pr-N` conservan datos sintéticos entre redeploys y también tienen
  backups pre-migración privados. Nunca reciben datos institucionales de prod.
- Nunca ejecutar `purge-storage.sh`, `teardown.sh` ni `restore-storage.sh`
  contra producción. El último script rechaza `prod` por diseño.
- La credencial administrativa de SeaweedFS sólo se usa para provisionamiento,
  backup y restore descartable. El backend recibe exclusivamente la credencial
  de aplicación del ambiente.

## Variables requeridas

Los procedimientos usan estas variables exportadas por el entorno seguro de
operación. Los valores no deben aparecer en logs ni en el historial del shell:

```text
AMBIENTE
RED_DATOS                                  # opcional; default: arsdocendi-datos
SEAWEEDFS_ROOT_ACCESS_KEY                  # admin S3 para infra/backup/restore
SEAWEEDFS_ROOT_SECRET_KEY                  # admin S3 para infra/backup/restore
SEAWEEDFS_APP_ACCESS_KEY / SECRET_KEY      # credencial del backend, no del backup
SEAWEEDFS_BUCKET_PREFIX                    # opcional; default: arsdocendi
STORAGE_SCOPE                              # prod o shared_nonprod, calculado por scripts
PGHOST PGPORT PGUSER PGPASSWORD            # admin PostgreSQL
PGDATABASE                                 # si se configura, debe coincidir con el ambiente
APP_DB_USER APP_DB_PASSWORD                # identidad app existente del destino
BACKUP_VOLUME_PREFIX                       # default arsdocendi-backups, var no secreta
BACKUP_RETENTION_DAYS                      # default 7, entero positivo, var no secreta
RECOVERY_AUTHORIZED                        # igual al destino aislado autorizado
RECOVERY_HOST_ROLE                         # principal, obligatorio para fuente prod
```

### Migración de secretos GitHub

Los workflows versionados consumen los nombres nuevos **desde los secrets del
repositorio**, no desde un secret diferente en cada Environment. La relación
de nombres legados es:

| Secreto actual        | Secreto requerido           | Uso                          |
| --------------------- | --------------------------- | ---------------------------- |
| `MINIO_ROOT_USER`     | `SEAWEEDFS_ROOT_ACCESS_KEY` | access key administrativa S3 |
| `MINIO_ROOT_PASSWORD` | `SEAWEEDFS_ROOT_SECRET_KEY` | secret key administrativa S3 |

Los secrets `MINIO_*` no son consumidos por los workflows actuales: retirarlos
del repositorio sólo tras confirmar que ningún consumidor antiguo los necesita.
Las credenciales del backend (`ALMACENAMIENTO_ACCESS_KEY` y
`ALMACENAMIENTO_SECRET_KEY`) son distintas y no deben apuntar a la identidad
administrativa. Los Environments `prod`, `staging` y `pr-preview` guardan sus
respectivas claves de base de aplicación, no una segunda copia de la raíz S3.

Para rotar `SEAWEEDFS_ROOT_*`: preparar identidades S3 compatibles en **ambos
hosts**, actualizar los dos secretos de repositorio, ejecutar despliegues
controlados por ambiente y verificar provisionamiento en ambos hosts y
backup de producción en Debian. Preservar el acceso al backup del prod antiguo
de Proxmox mientras siga siendo necesario para el corte o rollback.
No cambiar únicamente GitHub si el storage ya fue inicializado con otra clave.
No publicar valores en logs ni en comentarios de PR. El inventario por scope
está en [github-pr-deploy.md](github-pr-deploy.md).

## Verificación de salud

```bash
source infra/scripts/_comun.sh
network="${RED_DATOS:-arsdocendi-datos}"
bucket="${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${AMBIENTE}"
storage_host="$(seaweedfs_host_for "$AMBIENTE")"

seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
  s3api head-bucket --bucket "$bucket"
seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
  s3api list-objects-v2 --bucket "$bucket" --max-items 5

docker compose -p "$(storage_project_for "$AMBIENTE")" ps
docker compose -p "$(antivirus_project)" ps
```

Si falla `head-bucket`, revisar el contenedor SeaweedFS, el proyecto
`$(storage_project_for "$AMBIENTE")`, el volumen correspondiente a
`$(storage_scope_suffix_for "$AMBIENTE")`, el proyecto
`$(antivirus_project)` y la red interna. Si falla sólo el bucket, no crearlo
manualmente en producción sin registrar el incidente: revisar primero el
provisionamiento del ambiente.

## Backup verificable

El procedimiento versionado es `infra/scripts/backup-storage.sh <ambiente>`.
Produce un snapshot privado en el volumen Docker
`${BACKUP_VOLUME_PREFIX:-arsdocendi-backups}-<ambiente>` del host de la base,
no en el workspace del runner. Dump y objetos se transmiten directamente por
stdio, sin binds. El formato conjunto existente conserva:

- `postgres.dump`: dump custom de la base del ambiente;
- `objects/`: bytes descargados desde SeaweedFS;
- `manifest.json`: base/bucket, claves, tamaño, ETag, content type, metadata y hash;
- `checksums.sha256`: integridad del conjunto;
- `status`: `incomplete` hasta verificar todo, después `complete` inmutable.

Detener escritores antes del snapshot (el script rechaza backend activo del
ambiente). El deploy lo hace automáticamente con pendientes en base existente.
Sin pendientes/base nueva no se fabrica un backup. Ejemplo **en Debian** con
credenciales inyectadas desde el canal seguro:

```bash
snapshot="$(infra/scripts/backup-storage.sh prod)"
infra/scripts/backup-volume.sh prod list
infra/scripts/backup-volume.sh prod verify "$snapshot"
```

Stdout devuelve sólo ID; diagnósticos no incluyen claves de objetos o secretos.
No subir dumps/metadata/bytes como artifacts públicos ni a comentarios de PR.
El volumen requiere acceso administrativo restringido; no implementa cifrado
por sí solo ni aislamiento frente a administradores Docker. Cifrar cualquier
exportación externa según política aprobada; la exportación a directorio del
script está limitada a datos no productivos.

Retención default 7 días positivos (`BACKUP_RETENTION_DAYS`) sólo para snapshots
completos de **deploys exitosos** vencidos del ambiente solicitado. Un snapshot
completo sin marca de deploy exitoso y cualquier snapshot incompleto quedan
retenidos para limpieza manual autorizada. Teardown/reset no borran respaldos.
Una copia fuera del host, SLA y frecuencia de drills requieren decisión
institucional aparte; no confundir retención local con garantía ante pérdida
física. Conservar el backup del prod anterior hasta aceptar y autorizar el corte.

## Corte de producción de Proxmox a Debian

1. El operador registra una decisión explícita: **transferir los datos existentes**
   o **iniciar vacío con aprobación**. La ausencia de datos en Debian no autoriza
   descartar el prod anterior; no ejecutar comandos de borrado de producción.
2. Acordar ventana de mantenimiento, controlar/detener escrituras del origen y
   obtener backup coherente de `arsdocendi_prod` y sus objetos. Registrar host,
   versión de imágenes, manifiesto y hashes sin secretos.
3. Si se transfieren datos, preparar un procedimiento productivo de restore
   revisado y verificarlo antes del corte. `restore-storage.sh` rechaza `prod`:
   no quitar esa protección ni usar teardown/reset de previews en producción.
   Validar integridad PostgreSQL/objetos, credenciales y una descarga autenticada.
4. Si se aprueba inicio vacío, documentar su aceptación y la custodia de los
   datos anteriores; la aprobación no implica borrarlos. Revisar callbacks SSO,
   allowed origins y enlaces absolutos reales, sin inventar callbacks.
5. El operador cambia apex/ingress a Debian, verifica frontend, API, login,
   escrituras y adjuntos y registra aceptación. Bloquear `prod.<DOMINIO>` en
   Proxmox antes del wildcard; retirar su runtime sólo después de la aceptación.
   Preservar base, volúmenes, objetos y backups anteriores hasta autorización.
6. Rollback coordinado: restaurar DNS/ingress y workflow anteriores, pero si
   Debian recibió escrituras, decidir cómo recuperarlas antes de reabrir el
   origen antiguo. No descartar esas escrituras ni mantener dos prod públicas.
   No hay failover automático ni sincronización que resuelva la discrepancia.

No ejecutar esta migración desde la documentación: requiere decisiones y
evidencia del operador. Al retirar Proxmox en el futuro, decidir por separado
el destino de staging/previews y el tratamiento de datos antes de apagar la VM;
no se trasladan implícitamente a Debian.

## Restore y prueba de recuperación

`restore-storage.sh` exige autorización explícita del destino y nunca restaura
sobre `prod`, DROPpea ni recrea un destino poblado. Antes de escribir verifica
hashes y rechaza base poblada, bucket no vacío o runtime activo. Restaura
PostgreSQL/objetos por stdio y verifica SHA-256, tamaños, content type y metadata.
Una falla conserva destino parcial para diagnóstico; no publica ni resetea.

Ejemplo sintético y aislado en el host no productivo (identidad app existente,
credenciales seguras inyectadas, destino nuevo sin ingress):

```bash
RECOVERY_AUTHORIZED=recovery-drill \
  infra/scripts/restore-storage.sh recovery-drill "volume:staging:$snapshot"
```

Para datos prod: únicamente desde `volume:prod:ID` a `recovery-<id>` aislado del
**mismo principal**, con `RECOVERY_AUTHORIZED` coincidente y
`RECOVERY_HOST_ROLE=principal`. No se permite un dump prod desde directorio ni
copiarlo a Proxmox, staging o PR públicos. La variable de rol no acredita por sí
sola el host físico: el operador verifica ubicación y autorización. No publicar
el destino por wildcard/Traefik ni habilitar fixtures/autenticación de desarrollo.

Tras el restore, validar historia y versión compatible, filas de
`storage.archivos`, hash de muestras descargadas, content type y metadata.
El script preserva referencias originales en PostgreSQL; no cambia silenciosamente
buckets/ambientes ni genera credenciales de recovery. Preparar esa configuración
restringida y la adaptación explícita de referencias en el plan de corte
aprobado. No reabrir una base que aceptó nuevas escrituras suponiendo que el
snapshot las contiene. Mantener escritores detenidos y obtener backup de
cualquier estado posterior antes de decidir un corte.

Los ensayos automatizados usan PostgreSQL 18/SeaweedFS reales y datos sintéticos,
nunca secretos/datasets reales. Comando, aislamiento y cobertura en
[migrations-persistence.md](migrations-persistence.md#ensayos-reproducibles).
El corte productivo y un drill institucional requieren aprobación/evidencia
independiente; este cambio no los ejecuta.

## Capacidad y retención

Revisar semanalmente:

```bash
df -h /var/lib/docker
docker system df
docker volume inspect "arsdocendi-seaweedfs-data-$(storage_scope_suffix_for "$AMBIENTE")"
docker compose -p "$(storage_project_for "$AMBIENTE")" ps
docker compose -p "$(antivirus_project)" ps
```

Alertar antes de alcanzar 70% de uso del volumen; planificar expansión o
retención antes de 80%; detener cargas nuevas y escalar el incidente por encima
de 90%. No borrar objetos manualmente para recuperar espacio: usar la política
de retención y `LimpiarAsync`, preservando todo archivo asociado.

## Recuperación ante incidente

- **SeaweedFS no disponible:** mantener cargas nuevas fuera de servicio o en
  error recuperable; no marcar archivos como `disponible` manualmente.
- **ClamAV no disponible:** en producción `RechazarSiAntivirusNoDisponible`
  debe estar habilitado. Los archivos quedan rechazados, no publicados.
- **Objeto ausente con metadata disponible:** conservar la fila para auditoría,
  registrar la discrepancia y restaurar desde backup; no inventar bytes.
- **Metadata ausente con objeto presente:** no publicar el objeto; aislarlo,
  identificar el ambiente y resolverlo con reconciliación.
- **Pérdida de volumen:** restaurar PostgreSQL y SeaweedFS como una unidad
  lógica y repetir la prueba de hash antes de abrir el ambiente.
- **Aislamiento fallido:** detener el backend afectado, rotar su credencial y
  verificar que una credencial de ambiente no pueda leer el bucket de otro.

## Evidencia requerida

Cada backup o restore debe dejar un registro operativo con fecha UTC, ambiente,
operador o job, artefactos usados, conteos, SHA-256, resultado de la muestra de
descarga y cualquier discrepancia. La prueba ejecutada en un entorno descartable
debe conservar su manifiesto y resumen sin conservar secretos.
