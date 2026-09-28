#!/usr/bin/env bash
# Destruye un ambiente DESCARTABLE por completo: contenedores + volúmenes + base.
# Idempotente: no falla si el ambiente ya no existe.
#
# Uso:
#   teardown.sh <ambiente>          # ambiente: staging | pr-<N>  (NUNCA prod)
#
# Variables:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (libpq)
#   SEAWEEDFS_ROOT_ACCESS_KEY SEAWEEDFS_ROOT_SECRET_KEY credenciales administrativas del servicio privado

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"   # aborta si es prod o inválido

scripts_dir="$(cd "$(dirname "$0")" && pwd)"
compose_file="$(cd "$scripts_dir/../compose" && pwd)/compose.base.yml"

log_warn msg="teardown iniciado" ambiente="$ambiente"

# 1. Contenedores + volúmenes de la aplicación del ambiente.
#    El storage híbrido se conserva; purge-storage.sh elimina sólo el bucket y
#    la identidad del ambiente descartable.
docker compose -p "$ambiente" \
  -f "$compose_file" \
  --env-file <(printf 'AMBIENTE=%s\nHOST_PUBLICO=x\nREGISTRO=x\nTAG_FRONTEND=x\nTAG_BACKEND=x\nURL_BASE_DATOS=x\n' "$ambiente") \
  down -v --remove-orphans || log_warn msg="compose down no encontró el project (ok, idempotente)" ambiente="$ambiente"

# 2. Bucket e identidad del ambiente en SeaweedFS. Los proyectos/volúmenes
#    compartidos no se destruyen.
"$scripts_dir/purge-storage.sh" "$ambiente"

# 3. Base del ambiente (drop-db es idempotente y valida que no sea prod).
"$scripts_dir/drop-db.sh" "$ambiente"

# 4. Rol exclusivo del ambiente. El nombre se deriva del ambiente igual que en CI,
#    por lo que teardown no acepta un rol arbitrario como objetivo destructivo.
rol="app_${ambiente//-/_}"
psql_admin --set=app_db_user="$rol" <<'SQL'
DROP ROLE IF EXISTS :"app_db_user";
SQL

log_info msg="teardown OK" ambiente="$ambiente"
