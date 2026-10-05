#!/usr/bin/env bash
# Elimina sólo el bucket y la identidad de un ambiente descartable.
# En el modelo híbrido nunca baja el SeaweedFS/ClamAV compartido.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"
: "${SEAWEEDFS_ROOT_ACCESS_KEY:?msg=\"falta SEAWEEDFS_ROOT_ACCESS_KEY\"}"
: "${SEAWEEDFS_ROOT_SECRET_KEY:?msg=\"falta SEAWEEDFS_ROOT_SECRET_KEY\"}"

scripts_dir="$(cd "$(dirname "$0")" && pwd)"
compose_file="$scripts_dir/../compose/compose.storage.yml"
storage_project="$(storage_project_for "$ambiente")"
storage_scope="$(storage_scope_suffix_for "$ambiente")"
bucket="${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
network="${RED_DATOS:-arsdocendi-datos}"
storage_host="$(seaweedfs_host_for "$ambiente")"

variable_ambiente="${ambiente^^}"
variable_ambiente="${variable_ambiente//-/_}"
access_variable="SEAWEEDFS_APP_ACCESS_KEY_${variable_ambiente}"
app_access_key="${!access_variable:-$(seaweedfs_app_access_for "$ambiente")}"

storage_env_file="$(mktemp)"
trap 'rm -f "$storage_env_file"' EXIT
printf '%s\n' \
  "SEAWEEDFS_IMAGE=$SEAWEEDFS_IMAGE" \
  "SEAWEEDFS_ROOT_ACCESS_KEY=$SEAWEEDFS_ROOT_ACCESS_KEY" \
  "SEAWEEDFS_ROOT_SECRET_KEY=$SEAWEEDFS_ROOT_SECRET_KEY" \
  "SEAWEEDFS_HOSTNAME=$storage_host" \
  "STORAGE_SCOPE=$storage_scope" \
  "RED_DATOS=$network" >"$storage_env_file"

storage_compose() {
  docker compose -p "$storage_project" \
    --env-file "$storage_env_file" \
    -f "$compose_file" "$@"
}

# El servicio compartido se mantiene vivo; si estaba detenido, se levanta para
# poder ejecutar el purge con la identidad administrativa correcta.
storage_compose up -d seaweedfs
for intento in {1..120}; do
  container_id="$(storage_compose ps -q seaweedfs)"
  if [[ -n "$container_id" ]]; then
    estado="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}starting{{end}}' "$container_id")"
    if [[ "$estado" == "healthy" ]]; then
      break
    fi
    if [[ "$estado" == "unhealthy" ]]; then
      fatal "msg=\"seaweedfs quedó unhealthy\" ambiente=\"$ambiente\""
    fi
  fi
  if [[ "$intento" == 120 ]]; then
    fatal "msg=\"seaweedfs no quedó disponible\" ambiente=\"$ambiente\""
  fi
  sleep 2
done

if seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api head-bucket --bucket "$bucket" >/dev/null 2>&1; then
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3 rm "s3://$bucket" --recursive --only-show-errors
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api delete-bucket --bucket "$bucket" >/dev/null
fi

container_id="$(storage_compose ps -q seaweedfs)"
if ! seaweedfs_remove_app "$container_id" "$app_access_key" >/dev/null 2>&1; then
  log_warn msg="identidad S3 no encontrada o no eliminada" ambiente="$ambiente" access_key="$app_access_key"
fi

log_info msg="bucket e identidad eliminados" ambiente="$ambiente" bucket="$bucket" storage_project="$storage_project" scope="$storage_scope" shared_service="preserved"
