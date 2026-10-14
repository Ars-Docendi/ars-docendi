#!/usr/bin/env bash
# Restaura metadata PostgreSQL y objetos SeaweedFS en un ambiente descartable.
# Nunca restaura directamente sobre prod.
#
# Uso:
#   restore-storage.sh <ambiente-destino> <directorio-backup>
#
# Variables requeridas:
#   SEAWEEDFS_ROOT_ACCESS_KEY SEAWEEDFS_ROOT_SECRET_KEY
#   PGHOST PGUSER PGPASSWORD APP_DB_USER APP_DB_PASSWORD
# Variables opcionales:
#   PGPORT RED_DATOS SEAWEEDFS_BUCKET_PREFIX
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
backup_dir_input="${2:-}"
[[ $# -eq 2 ]] || fatal "msg=\"uso: restore-storage.sh <ambiente-destino> <directorio-backup>\""
[[ "$ambiente" =~ ^recovery-[a-z0-9][a-z0-9-]{0,30}$ ]] || exigir_ambiente_destruible "$ambiente"
[[ "${RECOVERY_AUTHORIZED:-}" == "$ambiente" ]] || fatal 'msg="falta RECOVERY_AUTHORIZED igual al destino aislado"'
# Los destinos recovery no tienen ingress; usar el lock del origen más abajo.
: "${SEAWEEDFS_ROOT_ACCESS_KEY:?msg=\"falta SEAWEEDFS_ROOT_ACCESS_KEY\"}"
: "${SEAWEEDFS_ROOT_SECRET_KEY:?msg=\"falta SEAWEEDFS_ROOT_SECRET_KEY\"}"
: "${PGHOST:?msg=\"falta PGHOST\"}"
: "${PGUSER:?msg=\"falta PGUSER\"}"
: "${PGPASSWORD:?msg=\"falta PGPASSWORD\"}"
: "${APP_DB_USER:?msg=\"falta APP_DB_USER para recrear la base destino\"}"
: "${APP_DB_PASSWORD:?msg=\"falta APP_DB_PASSWORD para recrear la base destino\"}"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"; liberar_lock_ambiente' EXIT
vol_script="$(dirname "$0")/backup-volume.sh"
if [[ "$backup_dir_input" == volume:* ]]; then
  IFS=: read -r modo origen snapshot extra <<< "$backup_dir_input"
  [[ -z "${extra:-}" ]] || fatal 'msg="referencia de volumen inválida"'
  validar_ambiente "$origen"
  adquirir_lock_ambiente "$origen"
  "$vol_script" "$origen" verify "$snapshot"
  backup_read() { "$vol_script" "$origen" read "$snapshot" "$1"; }
  backup_read manifest.json > "$work_dir/manifest.json"
  manifest="$work_dir/manifest.json"
else
  backup_dir="$(cd "$backup_dir_input" && pwd)"
  manifest="$backup_dir/manifest.json"
  backup_read() { cat "$backup_dir/$1"; }
fi
[[ -f "$manifest" ]] || fatal 'msg="falta manifiesto de backup"'
source_environment="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["environment"])' "$manifest")"
validar_ambiente "$source_environment"
if [[ "$source_environment" == prod ]]; then
  [[ "$ambiente" == recovery-* && "$backup_dir_input" == volume:prod:* && "${RECOVERY_HOST_ROLE:-}" == principal ]] ||
    fatal 'msg="datos prod requieren volumen prod y recovery aislado autorizado en principal"'
fi
if [[ -z "${LOCK_AMBIENTE:-}" ]]; then adquirir_lock_ambiente "$source_environment"; fi
[[ "$ambiente" == "$source_environment" ]] || adquirir_lock_ambiente "$ambiente"
[[ -z "$(docker ps -q --filter "label=com.docker.compose.project=$ambiente" --filter "network=$RED_DATOS")" ]] || fatal 'msg="destino recovery activo; detener escritores"'
if [[ "$backup_dir_input" == volume:* ]]; then
  [[ "$source_environment" == "$origen" ]] || fatal 'msg="ambiente del manifiesto no corresponde al volumen"'
fi

network="${RED_DATOS:-arsdocendi-datos}"
target_bucket="${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
storage_host="$(seaweedfs_host_for "$source_environment")"
target_base="$(nombre_base "$ambiente")"
export PGPORT="${PGPORT:-5432}"

plan="$work_dir/plan.tsv"

python3 - "$manifest" "$plan" <<'PY'
import base64
import json
import sys
from pathlib import Path

manifest_path, plan_path = sys.argv[1:]
with open(manifest_path, encoding="utf-8") as stream:
    manifest = json.load(stream)

if manifest.get("format") != "arsdocendi-storage-backup/v1":
    raise SystemExit("formato de backup no soportado")
if not isinstance(manifest.get("objects"), list):
    raise SystemExit("manifest.objects no es una lista")

with open(plan_path, "w", encoding="utf-8") as plan:
    for index, obj in enumerate(manifest["objects"], start=1):
        key = obj.get("key")
        relative_file = obj.get("file")
        sha256 = obj.get("sha256")
        if not isinstance(key, str) or not key:
            raise SystemExit(f"objeto {index}: key inválida")
        if not isinstance(relative_file, str) or not relative_file.startswith("objects/"):
            raise SystemExit(f"objeto {index}: file fuera de objects/")
        parts = Path(relative_file).parts
        if any(part in ("", ".", "..") for part in parts) or len(parts) != 2:
            raise SystemExit(f"objeto {index}: file inseguro")
        if not isinstance(sha256, str) or len(sha256) != 64:
            raise SystemExit(f"objeto {index}: sha256 inválido")

        key_b64 = base64.b64encode(key.encode("utf-8")).decode("ascii")
        content_type_b64 = base64.b64encode((obj.get("content_type") or "application/octet-stream").encode("utf-8")).decode("ascii")
        metadata_b64 = base64.b64encode(
            json.dumps(obj.get("metadata") or {}, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        ).decode("ascii")
        fields = (
            f"{index:08d}",
            relative_file,
            key_b64,
            sha256,
            str(int(obj.get("size", 0))),
            content_type_b64,
            metadata_b64,
        )
        plan.write("\t".join(fields) + "\n")
PY

expected_objects="$(python3 - "$manifest" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as stream:
    print(json.load(stream)["object_count"])
PY
)"

bucket_object_count() {
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api list-objects-v2 --bucket "$1" --output json \
    | python3 -c 'import json, sys; print(len(json.load(sys.stdin).get("Contents", [])))'
}

# Verificar la integridad del conjunto antes de modificar el destino.
if [[ "$backup_dir_input" != volume:* ]]; then
  python3 "$(dirname "$0")/verificar-backup.py" "$backup_dir"
fi
if existe_base "$target_base"; then
  poblado="$(psql_en_docker -e "PGDATABASE=$target_base" "$IMAGEN_PSQL" psql -X -tAc "SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema');")"
  [[ "$poblado" == 0 ]] || fatal 'msg="destino de recovery poblado; no se sobrescribe"'
fi

if ! seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api head-bucket --bucket "$target_bucket" >/dev/null 2>&1; then
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api create-bucket --bucket "$target_bucket" >/dev/null
fi

existing_objects="$(bucket_object_count "$target_bucket")"
[[ "$existing_objects" == "0" ]] ||
  fatal "msg=\"el bucket destino no está vacío\" bucket=\"$target_bucket\" objects=\"$existing_objects\""

# Nunca DROP ni reset. La identidad autorizada ya existe; no rotar su clave.
if ! existe_base "$target_base"; then
  psql_admin --set=base="$target_base" --set=app_db_user="$APP_DB_USER" <<'SQL'
CREATE DATABASE :"base" OWNER :"app_db_user";
SQL
fi

# El dump se transmite por stdin para no depender de que el daemon Docker
# comparta el filesystem del runner.
backup_read postgres.dump | \
  psql_en_docker -e PGDATABASE="$target_base" "$IMAGEN_PSQL" \
  pg_restore --exit-on-error --no-owner --role="$APP_DB_USER" --dbname="$target_base"

# Ejecuta AWS CLI contra el endpoint privado; los bytes se transmiten por stdin.
seaweedfs_restore_aws() {
  docker run --rm -i --network "$network" \
    -e "AWS_ACCESS_KEY_ID=$SEAWEEDFS_ROOT_ACCESS_KEY" \
    -e "AWS_SECRET_ACCESS_KEY=$SEAWEEDFS_ROOT_SECRET_KEY" \
    -e AWS_DEFAULT_REGION=us-east-1 \
    -e AWS_S3_ADDRESSING_STYLE=path \
    "$AWS_CLI_IMAGE" \
    --endpoint-url "http://$storage_host:8333" "$@"
}

restored=0
restored_bytes=0
mapfile -t plan_lines < "$plan"
for plan_line in "${plan_lines[@]}"; do
  IFS=$'\t' read -r index relative_file key_b64 expected_sha expected_size content_type_b64 metadata_b64 <<< "$plan_line"
  [[ -n "$index" ]] || continue
  key="$(printf '%s' "$key_b64" | base64 --decode)"
  content_type="$(printf '%s' "$content_type_b64" | base64 --decode)"
  metadata_json="$(printf '%s' "$metadata_b64" | base64 --decode)"

  copy_args=(--only-show-errors)
  [[ -n "$content_type" ]] && copy_args+=(--content-type "$content_type")
  [[ "$metadata_json" != "{}" ]] && copy_args+=(--metadata "$metadata_json")
  backup_read "$relative_file" | \
    seaweedfs_restore_aws s3 cp - "s3://$target_bucket/$key" "${copy_args[@]}"

  actual_sha="$(seaweedfs_restore_aws s3 cp "s3://$target_bucket/$key" - --only-show-errors | sha256sum | cut -d' ' -f1)"
  [[ "$actual_sha" == "$expected_sha" ]] ||
    fatal "msg=\"hash restaurado no coincide\" esperado=\"$expected_sha\" obtenido=\"$actual_sha\""

  actual_size="$(seaweedfs_restore_aws s3api head-object --bucket "$target_bucket" --key "$key" --query 'ContentLength' --output text)"
  [[ "$actual_size" == "$expected_size" ]] ||
    fatal "msg=\"tamaño restaurado no coincide\" esperado=\"$expected_size\" obtenido=\"$actual_size\""

  seaweedfs_restore_aws s3api head-object --bucket "$target_bucket" --key "$key" --output json > "$work_dir/restored-head.json"
  python3 - "$work_dir/restored-head.json" "$content_type" "$metadata_json" <<'PY'
import json, sys
with open(sys.argv[1], encoding='utf-8') as f:
    head = json.load(f)
if head.get('ContentType') != sys.argv[2] or head.get('Metadata', {}) != json.loads(sys.argv[3]):
    raise SystemExit('metadata restaurada no coincide')
PY
  restored=$((restored + 1))
  restored_bytes=$((restored_bytes + expected_size))
done

[[ "$restored" == "$expected_objects" ]] ||
  fatal "msg=\"cantidad de objetos procesados no coincide con el manifiesto\" esperado=\"$expected_objects\" obtenido=\"$restored\""

final_objects="$(bucket_object_count "$target_bucket")"
[[ "$final_objects" == "$restored" ]] ||
  fatal "msg=\"cantidad de objetos restaurados no coincide\" esperado=\"$restored\" obtenido=\"$final_objects\""

log_info msg="restore completado" ambiente="$ambiente" bucket="$target_bucket" database="$target_base" objects="$restored" bytes="$restored_bytes" source_backup="$backup_dir_input"
