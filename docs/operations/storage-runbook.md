# Runbook de almacenamiento de archivos

Este runbook describe la operación del almacenamiento privado de archivos de
Ars Docendi. Los bytes viven en MinIO y la metadata, estados y asociaciones viven
en PostgreSQL. Una restauración válida debe recuperar ambos lados.

## Límites operativos

- MinIO y ClamAV solo se exponen en la red interna `arsdocendi-datos`.
- Cada ambiente usa un bucket propio: `arsdocendi-prod`, `arsdocendi-staging` o
  `arsdocendi-pr-N`.
- Las credenciales se inyectan en runtime. No escribirlas en este documento,
  archivos `.env` versionados, comandos persistidos en tickets ni imágenes.
- `staging` y `pr-N` son descartables y contienen datos sintéticos; no se
  respaldan como ambientes de recuperación.
- Nunca ejecutar `purge-storage.sh`, `teardown.sh` ni `mc rb --force` contra
  producción.

## Variables requeridas

Los procedimientos usan estas variables exportadas por el entorno seguro de
operación. Los valores no deben aparecer en logs ni en el historial del shell:

```text
AMBIENTE
MINIO_ROOT_USER
MINIO_ROOT_PASSWORD
MINIO_APP_ACCESS_KEY / MINIO_APP_SECRET_KEY  # opcionales; prod/staging se derivan
MINIO_BUCKET_PREFIX   # opcional; default: arsdocendi
RED_DATOS             # opcional; default: arsdocendi-datos
PGHOST PGPORT PGUSER PGPASSWORD
PGDATABASE
```

## Verificación de salud

Ejecutar desde el host de infraestructura:

```bash
network="${RED_DATOS:-arsdocendi-datos}"
bucket="${MINIO_BUCKET_PREFIX:-arsdocendi}-${AMBIENTE}"

# La imagen quay.io/minio/mc ya trae `mc` como entrypoint. Configurar el alias
# con las credenciales separadas evita romper el parsing de passwords con
# caracteres reservados de URI.
mc_privado() {
  docker run --rm -i --network "$network" \
    --entrypoint /bin/sh \
    -e "MINIO_MC_ACCESS_KEY=$MINIO_ROOT_USER" \
    -e "MINIO_MC_SECRET_KEY=$MINIO_ROOT_PASSWORD" \
    quay.io/minio/mc:latest \
    -c 'set -eu
      mc alias set local http://minio:9000 "$MINIO_MC_ACCESS_KEY" "$MINIO_MC_SECRET_KEY" >/dev/null
      exec mc "$@"' \
    arsdocendi-runbook "$@"
}

mc_privado ready local
mc_privado ls "local/$bucket"
```

Si falla `mc ready`, revisar el contenedor MinIO, el volumen
`arsdocendi-minio-data` y la red interna. Si falla solo el bucket, no crearlo
manualmente en producción sin registrar el incidente: revisar primero el
provisionamiento del ambiente.

## Backup de producción

### Metadata PostgreSQL

Respaldar la base completa del ambiente, incluyendo schemas `storage`, `portal`,
`designaciones`, `identity` y `audit`:

```bash
pg_dump --format=custom --no-owner --file="/secure/backups/${PGDATABASE}-$(date -u +%Y%m%dT%H%M%SZ).dump" "$PGDATABASE"
```

El archivo debe cifrarse antes de salir del nodo y conservarse según la política
aprobada por UNLaM: 7 respaldos diarios, 4 semanales y 6 mensuales. El backup
no se considera completo si solo incluye PostgreSQL.

### Objetos MinIO

Usar un destino de backup cifrado y fuera del volumen operativo. `mc mirror`
debe ejecutarse con una identidad de backup con permisos de lectura, no con la
credencial de la aplicación:

```bash
backup="/secure/backups/minio/${AMBIENTE}/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$backup"

# El job institucional inyecta las credenciales y configura el alias `local`
# dentro de un contenedor temporal; no escribirlas en un script.
docker run --rm --network "$network" \
  -v "$backup:/backup" quay.io/minio/mc:latest \
  mirror --overwrite "local/$bucket" /backup
```

En producción, el job de backup debe configurar el alias dentro del mismo
contenedor, inyectar secretos desde el gestor institucional y cifrar el destino.
El comando anterior muestra la operación de copia, no una autorización para
pegar secretos en la terminal compartida.

Registrar: ambiente, bucket, instante UTC, cantidad de objetos, tamaño total,
hash del artefacto de backup y ubicación cifrada. No registrar claves de objeto
si pueden contener información sensible.

## Restore y prueba de recuperación

La restauración se realiza primero en un ambiente descartable:

1. Crear una base vacía y un bucket vacío para el ambiente de prueba.
2. Restaurar PostgreSQL con `pg_restore`.
3. Restaurar los objetos del backup mediante `mc mirror --overwrite`.
4. Ejecutar las migraciones pendientes; no ejecutar seed productivo.
5. Verificar que cada fila `storage.archivos` en estado `disponible` tenga su
   objeto en el bucket correcto.
6. Descargar una muestra de PDF e imagen mediante la API autenticada.
7. Comparar SHA-256 calculado contra `storage.archivos.sha256`.
8. Verificar que una fila legacy sin `archivo_id` no genere una descarga falsa.
9. Registrar duración, objetos restaurados, filas restauradas y discrepancias.

Ejemplo de restauración de metadata:

```bash
pg_restore --exit-on-error --no-owner --dbname="$PGDATABASE" \
  /secure/backups/arsdocendi-prod-<timestamp>.dump
```

No restaurar directamente sobre producción como primera prueba. Una
restauración sobre producción requiere una ventana aprobada, backup previo y
plan explícito de rollback.

## Capacidad y retención

Revisar semanalmente:

```bash
df -h /var/lib/docker
docker system df

docker run --rm --network "$network" quay.io/minio/mc:latest \
  admin info local
```

Alertar antes de alcanzar 70% de uso del volumen; planificar expansión o
retención antes de 80%; detener cargas nuevas y escalar el incidente por encima
de 90%. No borrar objetos manualmente para recuperar espacio: usar la política
de retención y `LimpiarAsync`, preservando todo archivo asociado.

## Recuperación ante incidente

- **MinIO no disponible:** mantener cargas nuevas fuera de servicio o en error
  recuperable; no marcar archivos como `disponible` manualmente.
- **ClamAV no disponible:** en producción `RechazarSiAntivirusNoDisponible`
  debe estar habilitado. Los archivos quedan rechazados, no publicados.
- **Objeto ausente con metadata disponible:** conservar la fila para auditoría,
  registrar la discrepancia y restaurar desde backup; no inventar bytes.
- **Metadata ausente con objeto presente:** no publicar el objeto; aislarlo,
  identificar el ambiente y resolverlo con el procedimiento de reconciliación.
- **Pérdida de volumen:** restaurar PostgreSQL y MinIO como una unidad lógica y
  repetir la prueba de hash antes de abrir el ambiente.

## Evidencia requerida

Cada backup o restore debe dejar un registro operativo con fecha UTC, ambiente,
operador o job, artefactos usados, conteos, resultado de la muestra de descarga
y cualquier discrepancia. Este runbook documenta el procedimiento; la prueba
manual de recuperación sigue siendo un gate operativo pendiente hasta que se
realice en infraestructura descartable.
