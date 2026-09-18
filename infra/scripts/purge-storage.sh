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
minio_mc "$network" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" rb --force "local/$bucket" || true
log_info msg="objetos del ambiente eliminados" ambiente="$ambiente" bucket="$bucket"
