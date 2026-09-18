#!/usr/bin/env bash
# Provisiona el MinIO privado y el bucket/política de un ambiente.
# El proyecto de almacenamiento es común; el aislamiento se hace por bucket y
# credencial. Nunca se invoca con prod para purgar datos.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"
: "${MINIO_ROOT_USER:?msg=\"falta MINIO_ROOT_USER\"}"
: "${MINIO_ROOT_PASSWORD:?msg=\"falta MINIO_ROOT_PASSWORD\"}"
variable_ambiente="${ambiente^^}"
variable_ambiente="${variable_ambiente//-/_}"
access_variable="MINIO_APP_ACCESS_KEY_${variable_ambiente}"
secret_variable="MINIO_APP_SECRET_KEY_${variable_ambiente}"
app_access_key="${!access_variable:-${MINIO_APP_ACCESS_KEY:-}}"
app_secret_key="${!secret_variable:-${MINIO_APP_SECRET_KEY:-}}"
if [[ -z "$app_access_key" || -z "$app_secret_key" ]]; then
  if [[ "$ambiente" == "prod" || "$ambiente" == "staging" ]]; then
    app_access_key="$(minio_app_access_for "$ambiente")"
    app_secret_key="$(minio_app_secret_for "$ambiente" "$MINIO_ROOT_PASSWORD")"
  else
    fatal "msg=\"faltan credenciales MinIO de aplicación\" variable=\"$access_variable/$secret_variable\""
  fi
fi

compose_file="$(cd "$(dirname "$0")/../compose" && pwd)/compose.storage.yml"
project="${MINIO_COMPOSE_PROJECT:-arsdocendi-storage}"
bucket="${MINIO_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
network="${RED_DATOS:-arsdocendi-datos}"
root_host="http://${MINIO_ROOT_USER}:${MINIO_ROOT_PASSWORD}@minio:9000"

docker compose -p "$project" --env-file <(printf 'MINIO_ROOT_USER=%s\nMINIO_ROOT_PASSWORD=%s\n' "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD") -f "$compose_file" up -d

for intento in {1..30}; do
  if docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest ready local >/dev/null 2>&1; then
    break
  fi
  if [[ "$intento" == 30 ]]; then fatal 'msg="MinIO no quedó disponible"'; fi
  sleep 2
done

docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest mb --ignore-existing "local/$bucket"
if ! docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest admin user info local "$app_access_key" >/dev/null 2>&1; then
  docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest admin user add local "$app_access_key" "$app_secret_key"
fi

policy="$(printf '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Action":["s3:GetBucketLocation"],"Resource":["arn:aws:s3:::%s"]},{"Effect":"Allow","Action":["s3:ListBucket"],"Resource":["arn:aws:s3:::%s"]},{"Effect":"Allow","Action":["s3:GetObject","s3:PutObject","s3:DeleteObject"],"Resource":["arn:aws:s3:::%s/*"]}]}' "$bucket" "$bucket" "$bucket")"
if ! printf '%s' "$policy" | docker run -i --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest admin policy create local "arsdocendi-$ambiente" /dev/stdin; then
  docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest admin policy info local "arsdocendi-$ambiente" >/dev/null
fi
docker run --rm --network "$network" -e "MC_HOST_local=$root_host" quay.io/minio/mc:latest admin policy attach local "arsdocendi-$ambiente" --user "$app_access_key"
log_info msg="storage provisionado" ambiente="$ambiente" bucket="$bucket"
