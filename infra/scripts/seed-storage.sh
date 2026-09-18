#!/usr/bin/env bash
# Carga fixtures binarias mínimas en MinIO y las vincula con metadata sintética.
# Solo se ejecuta en ambientes no productivos después de seed.sql.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"
[[ "$ambiente" != "prod" ]] || fatal 'msg="fixtures de storage prohibidas en prod"'

: "${MINIO_ROOT_USER:?msg=\"falta MINIO_ROOT_USER\"}"
: "${MINIO_ROOT_PASSWORD:?msg=\"falta MINIO_ROOT_PASSWORD\"}"
variable_ambiente="${ambiente^^}"
variable_ambiente="${variable_ambiente//-/_}"
access_variable="MINIO_APP_ACCESS_KEY_${variable_ambiente}"
secret_variable="MINIO_APP_SECRET_KEY_${variable_ambiente}"
app_access_key="${!access_variable:-${MINIO_APP_ACCESS_KEY:-}}"
app_secret_key="${!secret_variable:-${MINIO_APP_SECRET_KEY:-}}"
: "${app_access_key:?msg=\"falta credencial MinIO de aplicación\"}"
: "${app_secret_key:?msg=\"falta secreto MinIO de aplicación\"}"

network="${RED_DATOS:-arsdocendi-datos}"
bucket="${MINIO_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
base="$(nombre_base "$ambiente")"
mc() {
  docker run --rm -i --network "$network" \
    -e "MC_HOST_local=http://${app_access_key}:${app_secret_key}@minio:9000" \
    quay.io/minio/mc:latest "$@"
}

# IDs reservados al dataset sintético; no contienen PII ni se reutilizan en prod.
cv_id="f1000000-0000-4000-8000-000000000001"
doc_id="f1000000-0000-4000-8000-000000000002"
dni_id="f1000000-0000-4000-8000-000000000003"

cv_bytes='%PDF-1.7\nfixture cv sintetica\n'
doc_bytes='%PDF-1.7\nfixture proyecto sintetica\n'
dni_bytes=$'\xFF\xD8\xFF\xE0fixture dni sintetica'

subir() {
  local id="$1" contenido="$2"
  printf '%s' "$contenido" | mc pipe "local/$bucket/archivos/seed/$id"
}
subir "$cv_id" "$cv_bytes"
subir "$doc_id" "$doc_bytes"
printf '%s' "$dni_bytes" | mc pipe "local/$bucket/archivos/seed/$dni_id"

cv_hash="$(printf '%s' "$cv_bytes" | sha256sum | cut -d' ' -f1)"
doc_hash="$(printf '%s' "$doc_bytes" | sha256sum | cut -d' ' -f1)"
dni_hash="$(printf '%s' "$dni_bytes" | sha256sum | cut -d' ' -f1)"

# psql recibe solo metadata sintética y conserva la ejecución idempotente del seed.
sql="$(printf '%s\n' \
  "INSERT INTO storage.archivos (id, proposito, ambiente, bucket, clave_objeto, nombre_original, mime_declarado, mime_detectado, tamano_bytes, sha256, estado, propietario_id, expira_en, confirmado_at, revisado_at) VALUES" \
  "('$cv_id', 'cv', '$ambiente', '$bucket', 'archivos/seed/$cv_id', 'cv-sintetico.pdf', 'application/pdf', 'application/pdf', ${#cv_bytes}, '$cv_hash', 'disponible', 'a0000000-0000-4000-8000-000000000001', now() + interval '10 years', now(), now())," \
  "('$doc_id', 'documento_proyecto', '$ambiente', '$bucket', 'archivos/seed/$doc_id', 'proyecto-sintetico.pdf', 'application/pdf', 'application/pdf', ${#doc_bytes}, '$doc_hash', 'disponible', 'a0000000-0000-4000-8000-000000000001', now() + interval '10 years', now(), now())," \
  "('$dni_id', 'dni_frente', '$ambiente', '$bucket', 'archivos/seed/$dni_id', 'dni-frente-sintetico.jpg', 'image/jpeg', 'image/jpeg', ${#dni_bytes}, '$dni_hash', 'disponible', 'a0000000-0000-4000-8000-000000000001', now() + interval '10 years', now(), now())" \
  "ON CONFLICT (id) DO UPDATE SET bucket = EXCLUDED.bucket, clave_objeto = EXCLUDED.clave_objeto, estado = EXCLUDED.estado, sha256 = EXCLUDED.sha256;" \
  "UPDATE portal.cvs SET archivo_id = '$cv_id', uri = NULL WHERE id = 'f0200000-0000-4000-8000-000000000001';" \
  "UPDATE portal.proyecto_documentos SET archivo_id = '$doc_id', uri = NULL WHERE id = 'f0700000-0000-4000-8000-000000000001';")"
psql_en_docker -e "PGDATABASE=$base" "$IMAGEN_PSQL" psql -v ON_ERROR_STOP=1 -c "$sql"
log_info msg="fixtures de storage sembradas" ambiente="$ambiente" bucket="$bucket"
