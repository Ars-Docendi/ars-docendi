#!/usr/bin/env bash
# Helpers compartidos por los scripts de infra. Se sourcea, no se ejecuta.
#
#   source "$(dirname "$0")/_comun.sh"
#
# Convenciones:
#   - Credenciales admin de Postgres por libpq: PGHOST, PGPORT, PGUSER, PGPASSWORD.
#   - El ambiente se identifica por su nombre determinístico: prod | staging | pr-<N>.

set -euo pipefail

# --- Logging estructurado (clave=valor, parseable) ---
_log() {
  local nivel="$1"; shift
  local ts; ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "ts=${ts} nivel=${nivel} $*" >&2
}
log_info()  { _log INFO  "$@"; }
log_warn()  { _log WARN  "$@"; }
log_error() { _log ERROR "$@"; }
fatal()     { log_error "$@"; exit 1; }

# Nombre de la base aislada del ambiente: arsdocendi_<ambiente con '-' -> '_'>.
# pr-123 -> arsdocendi_pr_123 ; staging -> arsdocendi_staging ; prod -> arsdocendi_prod
nombre_base() {
  local ambiente="$1"
  [[ -n "$ambiente" ]] || fatal "msg=\"ambiente vacío\""
  echo "arsdocendi_$(echo "$ambiente" | tr '-' '_')"
}

# Valida que el nombre de ambiente tenga forma esperada (prod|staging|pr-<N>).
validar_ambiente() {
  local ambiente="$1"
  if [[ ! "$ambiente" =~ ^(prod|staging|pr-[0-9]+)$ ]]; then
    fatal "msg=\"ambiente inválido\" ambiente=\"${ambiente}\" esperado=\"prod|staging|pr-<N>\""
  fi
}

# Solo permite operaciones DESTRUCTIVAS sobre ambientes descartables.
# prod NUNCA es destruible por estos scripts; solo staging y pr-N.
exigir_ambiente_destruible() {
  local ambiente="$1"
  validar_ambiente "$ambiente"
  if [[ "$ambiente" == "prod" ]]; then
    fatal "msg=\"operación destructiva PROHIBIDA sobre prod\" ambiente=\"prod\""
  fi
  if [[ ! "$ambiente" =~ ^(staging|pr-[0-9]+)$ ]]; then
    fatal "msg=\"ambiente no destruible\" ambiente=\"${ambiente}\""
  fi
}

# Red Docker interna donde vive Postgres (no expuesto al host: ver infra/README §2)
# e imagen que trae el cliente psql (pin a la versión del server). Override por env.
RED_DATOS="${RED_DATOS:-arsdocendi-datos}"
IMAGEN_PSQL="${IMAGEN_PSQL:-postgres:18-alpine}"

# Corre psql en un contenedor efímero adjunto a la red de datos. El host del runner
# NO trae cliente psql ni alcanza a 'arsdocendi-postgres' (5432 sin publicar), así
# que toda invocación a psql pasa por acá. Reenvía credenciales libpq por -e.
# Los args extra de `docker run` (p. ej. -e PGDATABASE=... o -v para montar un .sql)
# van ANTES de la imagen:
#   psql_en_docker [args-docker...] "$IMAGEN_PSQL" psql [args-psql...]
psql_en_docker() {
  docker run --rm --network "$RED_DATOS" \
    -e PGHOST -e PGPORT -e PGUSER -e PGPASSWORD \
    "$@"
}

# psql como admin contra la base 'postgres' (para CREATE/DROP DATABASE).
psql_admin() {
  psql_en_docker -e PGDATABASE=postgres "$IMAGEN_PSQL" \
    psql -v ON_ERROR_STOP=1 -tA "$@"
}

# ¿Existe la base?  existe_base <nombre_base> -> 0 si existe
existe_base() {
  local base="$1"
  local res
  res="$(psql_admin -c "SELECT 1 FROM pg_database WHERE datname = '${base}';")"
  [[ "$res" == "1" ]]
}
