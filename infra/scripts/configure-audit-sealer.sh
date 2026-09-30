#!/usr/bin/env bash
# Provisiona el login de mínimo privilegio del job de sellado en prod.
# Ejecutar después de spin-up.sh prod (las tablas audit.seal_* deben existir).
# Variables: PGHOST PGPORT PGUSER PGPASSWORD (admin), AUDIT_SEAL_DB_PASSWORD.
set -euo pipefail
source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"
[[ "$ambiente" == "prod" ]] || fatal "msg=\"el custodio de auditoría sólo se provisiona en prod\""
: "${AUDIT_SEAL_DB_PASSWORD:?msg=\"falta AUDIT_SEAL_DB_PASSWORD\"}"
base="$(nombre_base "$ambiente")"
rol="audit_sealer_prod"

psql_en_docker -e AUDIT_SEAL_DB_PASSWORD "$IMAGEN_PSQL" \
  psql -v ON_ERROR_STOP=1 -tA -v rol="$rol" -v base="$base" <<'SQL'
\getenv clave_env AUDIT_SEAL_DB_PASSWORD
SELECT set_config('arsdocendi.audit_seal_role', :'rol', false) AS role_name,
       set_config('arsdocendi.audit_seal_password', :'clave_env', false) AS role_password
\gset
DO $$
DECLARE role_name text := current_setting('arsdocendi.audit_seal_role');
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
    EXECUTE format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT', role_name);
  END IF;
  EXECUTE format('ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT PASSWORD %L',
                 role_name, current_setting('arsdocendi.audit_seal_password'));
END
$$;
GRANT CONNECT ON DATABASE :"base" TO :"rol";
SQL

psql_en_docker -e PGDATABASE="$base" "$IMAGEN_PSQL" \
  psql -v ON_ERROR_STOP=1 -v rol="$rol" <<'SQL'
GRANT USAGE ON SCHEMA audit TO :"rol";
GRANT SELECT ON audit.change_log, audit.seal_cursor, audit.seal_baseline, audit.seal_batches TO :"rol";
GRANT USAGE ON SCHEMA identity TO :"rol";
GRANT SELECT ON identity.roles TO :"rol";
GRANT INSERT ON audit.seal_batches TO :"rol";
GRANT UPDATE (signature, signing_key_id, primary_witnessed_at, secondary_witnessed_at)
    ON audit.seal_batches TO :"rol";
GRANT UPDATE (status) ON audit.seal_baseline TO :"rol";
SQL

log_info msg="rol de sellado configurado con permisos mínimos" ambiente="$ambiente" rol="$rol" base="$base"
