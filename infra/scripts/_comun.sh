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
# ¿Existe el rol?  existe_rol <nombre_rol> -> 0 si existe
# Los roles son objetos de CLUSTER: viven fuera de cualquier base y sobreviven
# a un DROP DATABASE. Por eso el alta y la baja se manejan explícitamente.
existe_rol() {
  local rol="$1"
  local res
  res="$(psql_admin -c "SELECT 1 FROM pg_roles WHERE rolname = '${rol}';")"
  [[ "$res" == "1" ]]
}

# psql como admin contra la base de UN ambiente (para DDL que debe ejecutarse
# dentro de esa base, p. ej. DROP OWNED BY).  psql_base <base> [args-psql...]
psql_base() {
  local base="$1"; shift
  psql_en_docker -e "PGDATABASE=${base}" "$IMAGEN_PSQL" \
    psql -v ON_ERROR_STOP=1 -tA "$@"
}

# Escapa un valor para interpolarlo como literal SQL: duplica las comillas
# simples y devuelve el literal ya entrecomillado.
#
# Por qué NO alcanza con "'${valor}'": una contraseña con una comilla simple
# corta la sentencia y el resto se interpreta como SQL. Con standard_conforming_strings
# (default desde PostgreSQL 9.1) duplicar la comilla es el único escape necesario.
#
# Gotcha: este literal NO puede ir dentro de un bloque $$ ... $$. En un string
# dollar-quoted un '$$' en el valor cerraría la cita antes de tiempo. Por eso las
# sentencias de rol de acá son CREATE/ALTER planos, no un DO.
sql_literal() {
  local valor="$1"
  printf "'%s'" "${valor//\'/\'\'}"
}

# Sufijo determinístico del ambiente para nombres de objeto: '-' -> '_'.
# pr-123 -> pr_123 ; staging -> staging ; prod -> prod
sufijo_ambiente() {
  local ambiente="$1"
  [[ -n "$ambiente" ]] || fatal "msg=\"ambiente vacío\""
  echo "$ambiente" | tr '-' '_'
}

# Nombre de un rol de solo lectura del asistente para un ambiente.
#   rol_asistente <ambiente> <basico|pii>
#   prod   -> asistente_ro_prod   / asistente_ro_pii_prod
#   pr-123 -> asistente_ro_pr_123 / asistente_ro_pii_pr_123
#
# Un par de roles POR AMBIENTE y no uno global: los roles son objetos de cluster
# y la instancia es una sola con una base por ambiente. Un rol único sería el
# mismo principal —y la misma contraseña— para producción y para cada ambiente
# efímero de PR, que corre código arbitrario de un pull request sobre la misma
# red de datos. (change asistente-fundaciones, decisión D9.)
rol_asistente() {
  local ambiente="$1"
  local variante="${2:-}"
  local sufijo
  sufijo="$(sufijo_ambiente "$ambiente")"
  case "$variante" in
    basico) echo "asistente_ro_${sufijo}" ;;
    pii)    echo "asistente_ro_pii_${sufijo}" ;;
    *)      fatal "msg=\"variante de rol inválida\" variante=\"${variante}\" esperado=\"basico|pii\"" ;;
  esac
}

# Atributos con los que se crean AMBOS roles del asistente. Explícitos aunque
# varios coincidan con el default: son la frontera del módulo y se leen acá.
#   NOBYPASSRLS  el asistente queda sujeto a las policies de RLS — es el
#                mecanismo de contención de alcance por actor, no una decoración.
#   NOINHERIT    si mañana alguien le da membresía en otro rol, no hereda sus
#                privilegios de forma implícita.
ATRIBUTOS_ROL_ASISTENTE="NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT"

# Alta o actualización idempotente de un rol con LOGIN.
#   asegurar_rol_login <rol> <password> [atributos]
# Nunca loguea la contraseña.
asegurar_rol_login() {
  local rol="$1"
  local password="$2"
  local atributos="${3:-}"
  local literal
  literal="$(sql_literal "$password")"

  if existe_rol "$rol"; then
    psql_admin -c "ALTER ROLE \"${rol}\" WITH LOGIN PASSWORD ${literal} ${atributos};"
    log_info msg="rol actualizado" rol="$rol"
  else
    psql_admin -c "CREATE ROLE \"${rol}\" WITH LOGIN PASSWORD ${literal} ${atributos};"
    log_info msg="rol creado" rol="$rol"
  fi
}
