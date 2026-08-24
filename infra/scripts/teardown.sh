#!/usr/bin/env bash
# Destruye un ambiente DESCARTABLE por completo: contenedores + volúmenes + base.
# Idempotente: no falla si el ambiente ya no existe.
#
# Uso:
#   teardown.sh <ambiente>          # ambiente: staging | pr-<N>  (NUNCA prod)
#
# Variables:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (libpq)

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"   # aborta si es prod o inválido

scripts_dir="$(cd "$(dirname "$0")" && pwd)"
compose_file="$(cd "$scripts_dir/../compose" && pwd)/compose.base.yml"

log_warn msg="teardown iniciado" ambiente="$ambiente"

# 1. Contenedores + volúmenes del ambiente (idempotente: down no falla si no hay nada).
#    --env-file no es necesario para `down`, pero compose pide las vars del archivo;
#    se pasan placeholders inertes para que `down` resuelva sin errores.
docker compose -p "$ambiente" \
  -f "$compose_file" \
  --env-file <(printf 'AMBIENTE=%s\nHOST_PUBLICO=x\nREGISTRO=x\nTAG_FRONTEND=x\nTAG_BACKEND=x\nURL_BASE_DATOS=x\nASISTENTE_ROL_BASICO=x\nASISTENTE_ROL_PII=x\nASISTENTE_RO_PASSWORD=x\nASISTENTE_RO_PII_PASSWORD=x\n' "$ambiente") \
  down -v --remove-orphans || log_warn msg="compose down no encontró el project (ok, idempotente)" ambiente="$ambiente"

# 2. Base del ambiente (drop-db es idempotente y valida que no sea prod).
"$scripts_dir/drop-db.sh" "$ambiente"

log_info msg="teardown OK" ambiente="$ambiente"
