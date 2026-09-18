#!/usr/bin/env bash
# Elimina solo los objetos de un ambiente descartable. Nunca acepta prod.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"
ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"
: "${MINIO_ROOT_USER:?msg=\"falta MINIO_ROOT_USER\"}"
: "${MINIO_ROOT_PASSWORD:?msg=\"falta MINIO_ROOT_PASSWORD\"}"
network="${RED_DATOS:-arsdocendi-datos}"
bucket="${MINIO_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
root_host="http://${MINIO_ROOT_USER}:${MINIO_ROOT_PASSWORD}@minio:9000"
docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest rb --force "local/$bucket" || true
log_info msg="objetos del ambiente eliminados" ambiente="$ambiente" bucket="$bucket"
