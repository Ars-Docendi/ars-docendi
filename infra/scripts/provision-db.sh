#!/usr/bin/env bash
# Aprovisiona la base AISLADA de un ambiente (D7: una base por ambiente).
# Idempotente: si la base/rol ya existen, no falla.
#
# Uso:
#   provision-db.sh <ambiente>
#
# Variables requeridas:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales ADMIN de Postgres (libpq)
#   APP_DB_USER                       rol de la app para este ambiente (p. ej. app_pr_123)
#   APP_DB_PASSWORD                   password del rol de la app (inyectado en runtime)

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"
: "${APP_DB_USER:?msg=\"falta APP_DB_USER\"}"
: "${APP_DB_PASSWORD:?msg=\"falta APP_DB_PASSWORD\"}"

base="$(nombre_base "$ambiente")"

log_info msg="aprovisionando ambiente" ambiente="$ambiente" base="$base" rol="$APP_DB_USER"

# Rol de la app (idempotente).
psql_admin -c "DO \$\$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '${APP_DB_USER}') THEN
    CREATE ROLE \"${APP_DB_USER}\" LOGIN PASSWORD '${APP_DB_PASSWORD}';
  ELSE
    ALTER ROLE \"${APP_DB_USER}\" LOGIN PASSWORD '${APP_DB_PASSWORD}';
  END IF;
END
\$\$;"

# Base de datos (CREATE DATABASE no admite IF NOT EXISTS: chequeamos antes).
if existe_base "$base"; then
  log_info msg="base ya existe, no se recrea" base="$base"
else
  psql_admin -c "CREATE DATABASE \"${base}\" OWNER \"${APP_DB_USER}\";"
  log_info msg="base creada" base="$base"
fi

# Privilegios (idempotente).
psql_admin -c "GRANT ALL PRIVILEGES ON DATABASE \"${base}\" TO \"${APP_DB_USER}\";"

log_info msg="aprovisionamiento OK" ambiente="$ambiente" base="$base"
