#!/usr/bin/env bash
# Elimina el almacenamiento completo de un ambiente descartable. Nunca acepta prod.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"
compose_file="$(cd "$(dirname "$0")/../compose" && pwd)/compose.storage.yml"
project="arsdocendi-storage-${ambiente//-/_}"

storage_env_file="$(mktemp)"
trap 'rm -f "$storage_env_file"' EXIT
printf '%s\n' \
  "SEAWEEDFS_IMAGE=$SEAWEEDFS_IMAGE" \
  "SEAWEEDFS_ROOT_ACCESS_KEY=placeholder" \
  "SEAWEEDFS_ROOT_SECRET_KEY=placeholder" \
  "SEAWEEDFS_APP_ACCESS_KEY=placeholder" \
  "SEAWEEDFS_APP_SECRET_KEY=placeholder" \
  "SEAWEEDFS_BUCKET=placeholder" \
  "SEAWEEDFS_HOSTNAME=$(seaweedfs_host_for "$ambiente")" \
  "CLAMAV_HOSTNAME=$(clamav_host_for "$ambiente")" \
  "STORAGE_SUFFIX=${ambiente//-/_}" \
  "RED_DATOS=${RED_DATOS:-arsdocendi-datos}" >"$storage_env_file"

storage_compose() {
  docker compose -p "$project" \
    --env-file "$storage_env_file" \
    -f "$compose_file" "$@"
}

storage_compose down -v --remove-orphans || log_warn msg="storage compose down no encontró el project (ok, idempotente)" ambiente="$ambiente"
log_info msg="storage del ambiente eliminado" ambiente="$ambiente" project="$project"