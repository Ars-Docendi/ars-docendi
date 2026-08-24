#!/usr/bin/env bash
# Aprovisiona la base AISLADA de un ambiente (D7: una base por ambiente) y los
# roles que deben existir ANTES que cualquier tabla.
# Idempotente: si la base/roles ya existen, no falla.
#
# Uso:
#   provision-db.sh <ambiente>
#
# Variables requeridas:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales ADMIN de Postgres (libpq)
#   APP_DB_USER                       rol de la app para este ambiente (p. ej. app_pr_123)
#   APP_DB_PASSWORD                   password del rol de la app (inyectado en runtime)
#   ASISTENTE_RO_PASSWORD             password del rol de lectura del asistente
#   ASISTENTE_RO_PII_PASSWORD         password del rol de lectura con datos personales
#
# ALCANCE: acá va SOLO lo que debe existir antes que las tablas — CREATE ROLE,
# GRANT CONNECT y search_path. Los GRANT USAGE / GRANT SELECT del asistente van
# en una migración del módulo, NO acá: spin-up.sh corre este script en el paso 1
# sobre una base VACÍA, así que un `GRANT ... ON ALL TABLES` escrito acá otorgaría
# exactamente nada y no fallaría — el asistente arrancaría y PostgreSQL devolvería
# `permission denied` en cada consulta. (change asistente-fundaciones, decisión D8.)

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"
: "${APP_DB_USER:?msg=\"falta APP_DB_USER\"}"
: "${APP_DB_PASSWORD:?msg=\"falta APP_DB_PASSWORD\"}"
: "${ASISTENTE_RO_PASSWORD:?msg=\"falta ASISTENTE_RO_PASSWORD\"}"
: "${ASISTENTE_RO_PII_PASSWORD:?msg=\"falta ASISTENTE_RO_PII_PASSWORD\"}"

base="$(nombre_base "$ambiente")"
rol_ro="$(rol_asistente "$ambiente" basico)"
rol_ro_pii="$(rol_asistente "$ambiente" pii)"

log_info msg="aprovisionando ambiente" ambiente="$ambiente" base="$base" \
  rol="$APP_DB_USER" rol_asistente="$rol_ro" rol_asistente_pii="$rol_ro_pii"

# --- Roles (objetos de cluster: van antes que la base) ---

# Rol de la app: dueño de la base, con privilegios plenos sobre ella.
asegurar_rol_login "$APP_DB_USER" "$APP_DB_PASSWORD"

# Roles de solo lectura del asistente. Sin privilegios sobre ningún objeto
# todavía: acá solo nacen. Ver ATRIBUTOS_ROL_ASISTENTE en _comun.sh.
asegurar_rol_login "$rol_ro"     "$ASISTENTE_RO_PASSWORD"     "$ATRIBUTOS_ROL_ASISTENTE"
asegurar_rol_login "$rol_ro_pii" "$ASISTENTE_RO_PII_PASSWORD" "$ATRIBUTOS_ROL_ASISTENTE"

# --- Base de datos (CREATE DATABASE no admite IF NOT EXISTS: chequeamos antes) ---
if existe_base "$base"; then
  log_info msg="base ya existe, no se recrea" base="$base"
else
  psql_admin -c "CREATE DATABASE \"${base}\" OWNER \"${APP_DB_USER}\";"
  log_info msg="base creada" base="$base"
fi

# --- Privilegios a nivel BASE (idempotente) ---

# PUBLIC trae CONNECT y TEMPORARY sobre toda base nueva. Sin este REVOKE:
#   1. el `GRANT CONNECT` de abajo es decorativo — cualquier rol del cluster,
#      incluido el de OTRO ambiente, ya podía conectarse a esta base;
#   2. el asistente podría crear tablas temporales, y pg_temp se busca ANTES
#      que el search_path para resolver relaciones: una tabla temporal puede
#      tapar a una real y cambiar lo que una consulta lee.
# El rol de la app no se ve afectado: recibe sus privilegios explícitamente abajo.
psql_admin -c "REVOKE ALL ON DATABASE \"${base}\" FROM PUBLIC;"
psql_admin -c "GRANT ALL PRIVILEGES ON DATABASE \"${base}\" TO \"${APP_DB_USER}\";"

# Al asistente, CONNECT y nada más: ni CREATE ni TEMPORARY.
psql_admin -c "GRANT CONNECT ON DATABASE \"${base}\" TO \"${rol_ro}\", \"${rol_ro_pii}\";"

# search_path vacío: todo nombre de objeto debe ir calificado con su schema.
# Es la contrapartida del REVOKE de arriba — deja sin efecto cualquier intento de
# resolver un nombre por ambiente en vez de por schema, y hace que la SQL generada
# sea explícita sobre qué tabla toca. Consecuencia para quien escriba consultas:
# también las funciones de extensiones se llaman calificadas (`public.unaccent(...)`).
psql_admin -c "ALTER ROLE \"${rol_ro}\"     IN DATABASE \"${base}\" SET search_path = '';"
psql_admin -c "ALTER ROLE \"${rol_ro_pii}\" IN DATABASE \"${base}\" SET search_path = '';"

log_info msg="aprovisionamiento OK" ambiente="$ambiente" base="$base"
