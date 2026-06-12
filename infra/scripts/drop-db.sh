#!/usr/bin/env bash
# Elimina la base de un ambiente DESCARTABLE (staging / pr-N). Idempotente.
#
# Guardas de seguridad:
#   - Valida que el ambiente sea staging o pr-N (NUNCA prod).
#   - Si el nombre de base no empieza por el prefijo esperado, aborta.
#
# Uso:
#   drop-db.sh <ambiente>
#
# Variables:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (libpq)

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"   # aborta si es prod o inválido

base="$(nombre_base "$ambiente")"

# Cinturón y tiradores: el nombre de base debe ser de un ambiente descartable.
base_prod="$(nombre_base prod)"
if [[ "$base" == "$base_prod" ]]; then
  fatal "msg=\"se intentó dropear la base de prod\" base=\"${base}\""
fi
if [[ ! "$base" =~ ^arsdocendi_(staging|pr_[0-9]+)$ ]]; then
  fatal "msg=\"nombre de base no descartable\" base=\"${base}\""
fi

if ! existe_base "$base"; then
  log_info msg="base ya no existe, nada que dropear (idempotente)" base="$base"
  exit 0
fi

log_warn msg="dropeando base" ambiente="$ambiente" base="$base"

# Cortar conexiones activas antes del DROP para que no falle por 'in use'.
psql_admin -c "SELECT pg_terminate_backend(pid)
               FROM pg_stat_activity
               WHERE datname = '${base}' AND pid <> pg_backend_pid();" >/dev/null

psql_admin -c "DROP DATABASE IF EXISTS \"${base}\";"

log_info msg="base eliminada" ambiente="$ambiente" base="$base"
