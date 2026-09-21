# Runbook de almacenamiento de archivos

Este runbook describe la operación del almacenamiento privado de archivos de
Ars Docendi. Los bytes viven en SeaweedFS mediante su API S3 y la metadata,
estados y asociaciones viven en PostgreSQL. Un backup válido recupera ambos
lados; respaldar solamente PostgreSQL deja referencias sin bytes.

## Límites operativos

- SeaweedFS y ClamAV sólo se exponen en la red interna `arsdocendi-datos`.
- Cada ambiente usa un Compose project, volumen, alias de red, bucket y
  credencial de aplicación propios: `prod`, `staging` o `pr-N`.
- Las credenciales se inyectan en runtime. No escribirlas en este documento,
  archivos `.env` versionados, comandos persistidos en tickets ni imágenes.
- `staging` y `pr-N` son descartables y contienen datos sintéticos; no se
  respaldan como ambientes de recuperación institucional.
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
PGHOST PGPORT PGUSER PGPASSWORD            # admin PostgreSQL
PGDATABASE                                 # opcional; default: arsdocendi_<ambiente>
APP_DB_USER APP_DB_PASSWORD                # restore descartable
```

### Migración de secretos GitHub

Los workflows versionados ya consumen los nombres nuevos. En cada GitHub
Environment (`prod`, `staging` y `pr-preview` cuando corresponda), reemplazar:

| Secreto actual        | Secreto requerido           | Uso                          |
| --------------------- | --------------------------- | ---------------------------- |
| `MINIO_ROOT_USER`     | `SEAWEEDFS_ROOT_ACCESS_KEY` | access key administrativa S3 |
| `MINIO_ROOT_PASSWORD` | `SEAWEEDFS_ROOT_SECRET_KEY` | secret key administrativa S3 |

El valor actual puede reutilizarse como valor inicial de la identidad S3 si
cumple la política institucional de longitud y rotación. No se deben conservar
los nombres `MINIO_*` en los workflows después del corte. Las credenciales del
backend (`ALMACENAMIENTO_ACCESS_KEY` y `ALMACENAMIENTO_SECRET_KEY`) son distintas
y no deben apuntar a la identidad administrativa.

La rotación recomendada es: crear los dos secretos nuevos, ejecutar un deploy
controlado, verificar provisionamiento y backup, y recién entonces eliminar los
dos secretos antiguos. No publicar valores en logs ni en comentarios de PR.

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

docker compose -p "arsdocendi-storage-${AMBIENTE//-/_}" ps
```

Si falla `head-bucket`, revisar el contenedor SeaweedFS, el volumen
`arsdocendi-seaweedfs-data-${AMBIENTE//-/_}` y la red interna. Si falla sólo
el bucket, no crearlo manualmente en producción sin registrar el incidente:
revisar primero el provisionamiento del ambiente.

## Backup verificable

El procedimiento versionado es `infra/scripts/backup-storage.sh`. Genera un
directorio autocontenido con:

- `postgres.dump`: dump custom de la base del ambiente;
- `objects/`: bytes descargados desde SeaweedFS;
- `manifest.json`: bucket, base, claves de objeto, tamaño, ETag, content type,
  metadata S3 y SHA-256 por objeto;
- `checksums.sha256`: SHA-256 de todos los artefactos.

Ejemplo para producción, desde el nodo de infraestructura:

```bash
export AMBIENTE=prod
export PGDATABASE=arsdocendi_prod
infra/scripts/backup-storage.sh prod \
  "/secure/backups/arsdocendi/prod/$(date -u +%Y%m%dT%H%M%SZ)"
```

El destino debe estar cifrado antes de salir del nodo y conservarse según la
política aprobada por UNLaM: 7 respaldos diarios, 4 semanales y 6 mensuales. El
backup no se considera completo si sólo incluye PostgreSQL o sólo objetos.

El job debe registrar únicamente ambiente, instante UTC, cantidad de objetos,
tamaño total, SHA-256 del dump/manifiesto y ubicación cifrada. No registrar
claves de objetos si pueden contener información sensible.

## Restore y prueba de recuperación

`infra/scripts/restore-storage.sh` sólo permite `staging` y `pr-N`. Destruye y
recrea la base destino descartable, exige un bucket vacío, verifica primero
`checksums.sha256`, restaura PostgreSQL y después repone cada objeto mediante
S3. Para cada objeto comprueba SHA-256 y tamaño; también repone content type y
metadata S3 presentes en el manifiesto.

Ejemplo de drill mensual en un ambiente descartable:

```bash
export RED_DATOS=arsdocendi-datos
export PGHOST=arsdocendi-postgres
export PGPORT=5432
export PGUSER=postgres
export PGPASSWORD='[inyectar desde el gestor seguro]'
export APP_DB_USER=app_pr_123
export APP_DB_PASSWORD='[inyectar desde el gestor seguro]'
export SEAWEEDFS_ROOT_ACCESS_KEY='[inyectar desde el gestor seguro]'
export SEAWEEDFS_ROOT_SECRET_KEY='[inyectar desde el gestor seguro]'

infra/scripts/provision-db.sh pr-123
infra/scripts/provision-storage.sh pr-123
infra/scripts/restore-storage.sh pr-123 \
  /secure/backups/arsdocendi/prod/<timestamp>
```

Después del restore:

1. ejecutar migraciones compatibles si el despliegue lo requiere;
2. consultar una fila de `storage.archivos` en estado `disponible`;
3. descargar una muestra de PDF e imagen mediante la API autenticada;
4. comparar el SHA-256 descargado con `storage.archivos.sha256`;
5. confirmar que `Content-Type` y metadata S3 coincidan con el manifiesto;
6. verificar que una fila legacy sin `archivo_id` no genere una descarga falsa;
7. registrar duración, objetos restaurados, filas restauradas y discrepancias;
8. destruir el ambiente descartable con `teardown.sh` cuando termine el drill.

No restaurar directamente sobre producción como primera prueba. Una
restauración productiva requiere ventana aprobada, backup previo y plan de
rollback explícito.

## Capacidad y retención

Revisar semanalmente:

```bash
df -h /var/lib/docker
docker system df
docker volume inspect "arsdocendi-seaweedfs-data-${AMBIENTE//-/_}"
docker compose -p "arsdocendi-storage-${AMBIENTE//-/_}" ps
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
