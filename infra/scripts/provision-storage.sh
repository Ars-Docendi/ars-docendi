#!/usr/bin/env bash
# Provisiona SeaweedFS y ClamAV para un ambiente.
#
# Topología híbrida:
#   - prod usa su propia instancia y volumen SeaweedFS.
#   - staging y pr-N comparten una instancia/volumen SeaweedFS no-prod.
#   - todos los ambientes comparten una instancia ClamAV.
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

scripts_dir="$(cd "$(dirname "$0")" && pwd)"
compose_file="$scripts_dir/../compose/compose.storage.yml"
antivirus_file="$scripts_dir/../compose/compose.antivirus.yml"
storage_project="$(storage_project_for "$ambiente")"
antivirus_project="$(antivirus_project)"
storage_scope="$(storage_scope_suffix_for "$ambiente")"
bucket="${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
network="${RED_DATOS:-arsdocendi-datos}"
storage_host="$(seaweedfs_host_for "$ambiente")"
clamav_host="$(clamav_host_for)"

storage_env_file="$(mktemp)"
antivirus_env_file="$(mktemp)"
trap 'rm -f "$storage_env_file" "$antivirus_env_file"' EXIT
printf '%s\n' \
  "SEAWEEDFS_IMAGE=$SEAWEEDFS_IMAGE" \
  "SEAWEEDFS_ROOT_ACCESS_KEY=$SEAWEEDFS_ROOT_ACCESS_KEY" \
  "SEAWEEDFS_ROOT_SECRET_KEY=$SEAWEEDFS_ROOT_SECRET_KEY" \
  "SEAWEEDFS_HOSTNAME=$storage_host" \
  "STORAGE_SCOPE=$storage_scope" \
  "RED_DATOS=$network" >"$storage_env_file"
printf '%s\n' \
  "CLAMAV_HOSTNAME=$clamav_host" \
  "RED_DATOS=$network" >"$antivirus_env_file"

storage_compose() {
  docker compose -p "$storage_project" \
    --env-file "$storage_env_file" \
    -f "$compose_file" "$@"
}

antivirus_compose() {
  docker compose -p "$antivirus_project" \
    --env-file "$antivirus_env_file" \
    -f "$antivirus_file" "$@"
}

storage_compose up -d seaweedfs
antivirus_compose up -d clamav

for servicio in seaweedfs; do
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

for intento in {1..120}; do
  container_id="$(antivirus_compose ps -q clamav)"
  if [[ -n "$container_id" ]]; then
    estado="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}starting{{end}}' "$container_id")"
    if [[ "$estado" == "healthy" ]]; then
      break
    fi
    if [[ "$estado" == "unhealthy" ]]; then
      fatal "msg=\"clamav quedó unhealthy\" ambiente=\"$ambiente\""
    fi
  fi
  if [[ "$intento" == 120 ]]; then
    fatal "msg=\"clamav no quedó disponible\" ambiente=\"$ambiente\""
  fi
  sleep 2
done

container_id="$(storage_compose ps -q seaweedfs)"
for intento in {1..30}; do
  if seaweedfs_configure_app "$container_id" "$app_access_key" "$app_secret_key" "$bucket" >/dev/null 2>&1; then
    break
  fi
  if [[ "$intento" == 30 ]]; then
    fatal "msg=\"no se pudo registrar la identidad S3\" ambiente=\"$ambiente\" bucket=\"$bucket\""
  fi
  sleep 2
done

# El bucket se crea con la identidad administrativa; la aplicación sólo recibe
# la identidad registrada para su propio bucket.
if ! seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api head-bucket --bucket "$bucket" >/dev/null 2>&1; then
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api create-bucket --bucket "$bucket" >/dev/null
fi

log_info msg="storage provisionado" ambiente="$ambiente" bucket="$bucket" endpoint="$storage_host:8333" clamav="$clamav_host:3310" storage_project="$storage_project" antivirus_project="$antivirus_project" scope="$storage_scope"
