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
[[ "$APP_DB_USER" == "app_${ambiente//-/_}" ]] ||
  fatal "msg=\"APP_DB_USER no corresponde al ambiente\" ambiente=\"${ambiente}\""

base="$(nombre_base "$ambiente")"

log_info msg="aprovisionando ambiente" ambiente="$ambiente" base="$base" rol="$APP_DB_USER"

# Rol de la app (idempotente). Los valores viajan como variables de psql y
# PostgreSQL los cita con format(); nunca se interpolan dentro de SQL.
psql_admin \
  --set=app_db_user="$APP_DB_USER" \
  --set=app_db_password="$APP_DB_PASSWORD" \
  -c "SELECT set_config('arsdocendi.app_db_user', :'app_db_user', false),
             set_config('arsdocendi.app_db_password', :'app_db_password', false);
      DO \$\$
      DECLARE app_user text := current_setting('arsdocendi.app_db_user');
      BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = app_user) THEN
          EXECUTE format('CREATE ROLE %I LOGIN', app_user);
        END IF;
        EXECUTE format('ALTER ROLE %I PASSWORD %L',
          app_user, current_setting('arsdocendi.app_db_password'));
      END
      \$\$;"

# Base de datos (CREATE DATABASE no admite IF NOT EXISTS: chequeamos antes).
if existe_base "$base"; then
  log_info msg="base ya existe, no se recrea" base="$base"
else
  psql_admin --set=app_db_user="$APP_DB_USER" \
    -c "CREATE DATABASE \"${base}\" OWNER :\"app_db_user\";"
  log_info msg="base creada" base="$base"
fi

# Privilegios (idempotente).
psql_admin --set=app_db_user="$APP_DB_USER" \
  -c "GRANT ALL PRIVILEGES ON DATABASE \"${base}\" TO :\"app_db_user\";"

log_info msg="aprovisionamiento OK" ambiente="$ambiente" base="$base"
