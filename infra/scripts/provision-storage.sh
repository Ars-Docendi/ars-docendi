#!/usr/bin/env bash
# Provisiona SeaweedFS S3 y ClamAV aislados para un ambiente.
# Cada ambiente usa un Compose project, credencial y volumen independientes.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"
: "${SEAWEEDFS_ROOT_ACCESS_KEY:?msg=\"falta SEAWEEDFS_ROOT_ACCESS_KEY\"}"
: "${SEAWEEDFS_ROOT_SECRET_KEY:?msg=\"falta SEAWEEDFS_ROOT_SECRET_KEY\"}"

variable_ambiente="${ambiente^^}"
variable_ambiente="${variable_ambiente//-/_}"
access_variable="SEAWEEDFS_APP_ACCESS_KEY_${variable_ambiente}"
secret_variable="SEAWEEDFS_APP_SECRET_KEY_${variable_ambiente}"
app_access_key="${!access_variable:-${SEAWEEDFS_APP_ACCESS_KEY:-}}"
app_secret_key="${!secret_variable:-${SEAWEEDFS_APP_SECRET_KEY:-}}"
if [[ -z "$app_access_key" || -z "$app_secret_key" ]]; then
  if [[ "$ambiente" == "prod" || "$ambiente" == "staging" ]]; then
    app_access_key="$(seaweedfs_app_access_for "$ambiente")"
    app_secret_key="$(seaweedfs_app_secret_for "$ambiente" "$SEAWEEDFS_ROOT_SECRET_KEY")"
  else
    fatal "msg=\"faltan credenciales SeaweedFS de aplicación\" variable=\"$access_variable/$secret_variable\""
  fi
fi

compose_file="$(cd "$(dirname "$0")/../compose" && pwd)/compose.storage.yml"
project="arsdocendi-storage-${ambiente//-/_}"
bucket="${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
network="${RED_DATOS:-arsdocendi-datos}"
storage_host="$(seaweedfs_host_for "$ambiente")"
clamav_host="$(clamav_host_for "$ambiente")"

storage_env_file="$(mktemp)"
trap 'rm -f "$storage_env_file"' EXIT
printf '%s\n' \
  "SEAWEEDFS_IMAGE=$SEAWEEDFS_IMAGE" \
  "SEAWEEDFS_ROOT_ACCESS_KEY=$SEAWEEDFS_ROOT_ACCESS_KEY" \
  "SEAWEEDFS_ROOT_SECRET_KEY=$SEAWEEDFS_ROOT_SECRET_KEY" \
  "SEAWEEDFS_APP_ACCESS_KEY=$app_access_key" \
  "SEAWEEDFS_APP_SECRET_KEY=$app_secret_key" \
  "SEAWEEDFS_BUCKET=$bucket" \
  "SEAWEEDFS_HOSTNAME=$storage_host" \
  "CLAMAV_HOSTNAME=$clamav_host" \
  "STORAGE_SUFFIX=${ambiente//-/_}" \
  "RED_DATOS=$network" >"$storage_env_file"

storage_compose() {
  docker compose -p "$project" \
    --env-file "$storage_env_file" \
    -f "$compose_file" "$@"
}

storage_compose up -d

for servicio in seaweedfs clamav; do
  for intento in {1..120}; do
    container_id="$(storage_compose ps -q "$servicio")"
    if [[ -n "$container_id" ]]; then
      estado="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}starting{{end}}' "$container_id")"
      if [[ "$estado" == "healthy" ]]; then
        break
      fi
      if [[ "$estado" == "unhealthy" ]]; then
        fatal "msg=\"$servicio quedó unhealthy\" ambiente=\"$ambiente\""
      fi
    fi
    if [[ "$intento" == 120 ]]; then
      fatal "msg=\"$servicio no quedó disponible\" ambiente=\"$ambiente\""
    fi
    sleep 2
  done
done

# El bucket se crea con la identidad administrativa; la aplicación nunca recibe
# esa identidad ni puede operar sobre otro ambiente.
if ! seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api head-bucket --bucket "$bucket" >/dev/null 2>&1; then
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api create-bucket --bucket "$bucket" >/dev/null
fi

log_info msg="storage provisionado" ambiente="$ambiente" bucket="$bucket" endpoint="$storage_host:8333" clamav="$clamav_host:3310" project="$project"