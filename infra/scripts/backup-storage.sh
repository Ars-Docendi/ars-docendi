#!/usr/bin/env bash
# Respalda metadata PostgreSQL y objetos SeaweedFS de un ambiente.
#
# Uso:
#   backup-storage.sh <ambiente> <directorio-backup>
#
# Variables requeridas:
#   SEAWEEDFS_ROOT_ACCESS_KEY SEAWEEDFS_ROOT_SECRET_KEY
#   PGHOST PGUSER PGPASSWORD
# Variables opcionales:
#   PGPORT PGDATABASE RED_DATOS SEAWEEDFS_BUCKET_PREFIX
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
backup_dir_input="${2:-}"
[[ $# -eq 2 ]] || fatal "msg=\"uso: backup-storage.sh <ambiente> <directorio-backup>\""
validar_ambiente "$ambiente"
: "${SEAWEEDFS_ROOT_ACCESS_KEY:?msg=\"falta SEAWEEDFS_ROOT_ACCESS_KEY\"}"
: "${SEAWEEDFS_ROOT_SECRET_KEY:?msg=\"falta SEAWEEDFS_ROOT_SECRET_KEY\"}"
: "${PGHOST:?msg=\"falta PGHOST\"}"
: "${PGUSER:?msg=\"falta PGUSER\"}"
: "${PGPASSWORD:?msg=\"falta PGPASSWORD\"}"

backup_dir="$(mkdir -p "$backup_dir_input" && cd "$backup_dir_input" && pwd)"
mkdir -p "$backup_dir/objects"
chmod 700 "$backup_dir" "$backup_dir/objects"

network="${RED_DATOS:-arsdocendi-datos}"
bucket="${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${ambiente}"
storage_host="$(seaweedfs_host_for "$ambiente")"
base="$(nombre_base "$ambiente")"
pgdatabase="${PGDATABASE:-$base}"
export PGPORT="${PGPORT:-5432}"

work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT
list_json="$work_dir/list.json"
entries_ndjson="$work_dir/entries.ndjson"
: > "$entries_ndjson"

seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
  s3api head-bucket --bucket "$bucket" >/dev/null

# El dump se ejecuta dentro de la red Docker y se transmite por stdout; así no
# depende de que el daemon Docker comparta el filesystem del runner.
psql_en_docker -e PGDATABASE="$pgdatabase" "$IMAGEN_PSQL" \
  pg_dump --format=custom --no-owner "$pgdatabase" >"$backup_dir/postgres.dump"

postgres_sha256="$(sha256sum "$backup_dir/postgres.dump" | cut -d' ' -f1)"

seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
  s3api list-objects-v2 --bucket "$bucket" --output json > "$list_json"

mapfile -d '' keys < <(python3 - "$list_json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
for item in payload.get("Contents", []):
    sys.stdout.buffer.write(item["Key"].encode("utf-8") + b"\0")
PY
)

index=0
for key in "${keys[@]}"; do
  index=$((index + 1))
  relative_path="objects/$(printf '%08d.bin' "$index")"
  object_path="$backup_dir/$relative_path"
  head_json="$work_dir/head-$index.json"

  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3api head-object --bucket "$bucket" --key "$key" --output json > "$head_json"
  seaweedfs_aws "$network" "$SEAWEEDFS_ROOT_ACCESS_KEY" "$SEAWEEDFS_ROOT_SECRET_KEY" "$storage_host" \
    s3 cp "s3://$bucket/$key" - --only-show-errors >"$object_path"

  object_sha256="$(sha256sum "$object_path" | cut -d' ' -f1)"
  python3 - "$head_json" "$key" "$relative_path" "$object_sha256" >> "$entries_ndjson" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    head = json.load(stream)

entry = {
    "key": sys.argv[2],
    "file": sys.argv[3],
    "sha256": sys.argv[4],
    "size": int(head.get("ContentLength", 0)),
    "etag": head.get("ETag"),
    "content_type": head.get("ContentType"),
    "metadata": head.get("Metadata", {}),
}
print(json.dumps(entry, ensure_ascii=False, separators=(",", ":")))
PY
done

created_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
python3 - "$entries_ndjson" "$backup_dir/manifest.json" "$ambiente" "$bucket" "$pgdatabase" "$postgres_sha256" "$created_at" <<'PY'
import json
import sys
from pathlib import Path

entries = []
with open(sys.argv[1], encoding="utf-8") as stream:
    for line in stream:
        if line.strip():
            entries.append(json.loads(line))

manifest = {
    "format": "arsdocendi-storage-backup/v1",
    "environment": sys.argv[3],
    "bucket": sys.argv[4],
    "created_at_utc": sys.argv[7],
    "database": {
        "name": sys.argv[5],
        "file": "postgres.dump",
        "sha256": sys.argv[6],
    },
    "objects": entries,
    "object_count": len(entries),
    "total_bytes": sum(item["size"] for item in entries),
}
Path(sys.argv[2]).write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
    encoding="utf-8",
)
PY

(
  cd "$backup_dir"
  find . -type f ! -name checksums.sha256 -print0 \
    | sort -z \
    | xargs -0 sha256sum
) > "$backup_dir/checksums.sha256"
chmod 600 "$backup_dir"/manifest.json "$backup_dir"/postgres.dump "$backup_dir"/checksums.sha256
chmod 600 "$backup_dir"/objects/* 2>/dev/null || true

object_count="$(python3 - "$backup_dir/manifest.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as stream:
    print(json.load(stream)["object_count"])
PY
)"
total_bytes="$(python3 - "$backup_dir/manifest.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as stream:
    print(json.load(stream)["total_bytes"])
PY
)"

log_info msg="backup completado" ambiente="$ambiente" bucket="$bucket" database="$pgdatabase" objects="$object_count" bytes="$total_bytes" backup_dir="$backup_dir" postgres_sha256="$postgres_sha256"
