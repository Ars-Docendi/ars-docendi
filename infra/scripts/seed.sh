#!/usr/bin/env bash
# Siembra la base de un ambiente con datos NO productivos (sintéticos o snapshot
# anonimizado). ABORTA si se le pide copiar datos de prod a un ambiente no-prod.
#
# Uso:
#   seed.sh <ambiente>
#
# Variables:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (libpq)
#   SEED_SQL                          (opcional) ruta a un .sql de fixtures sintéticas
#                                     default: infra/scripts/seed-data/sintetico.sql
#   SEED_FROM_DB                      (opcional) base ORIGEN si se siembra por copia.
#                                     Si apunta a la base de prod y el destino NO es
#                                     prod, el script ABORTA (regla dura, BR de seguridad).

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"

base="$(nombre_base "$ambiente")"
base_prod="$(nombre_base prod)"

# --- Regla dura: nunca datos productivos en ambientes no-prod ---
if [[ -n "${SEED_FROM_DB:-}" ]]; then
  if [[ "$SEED_FROM_DB" == "$base_prod" && "$ambiente" != "prod" ]]; then
    fatal "msg=\"PROHIBIDO sembrar un ambiente no-prod con datos de prod\" destino=\"${base}\" origen=\"${SEED_FROM_DB}\""
  fi
fi

seed_sql="${SEED_SQL:-$(dirname "$0")/seed-data/sintetico.sql}"

log_info msg="sembrando ambiente" ambiente="$ambiente" base="$base" fixtures="$seed_sql"

if [[ ! -f "$seed_sql" ]]; then
  fatal "msg=\"no existe el archivo de fixtures\" ruta=\"${seed_sql}\""
fi

# psql corre en un contenedor (ver _comun.sh): el .sql se monta read-only adentro.
seed_sql_abs="$(cd "$(dirname "$seed_sql")" && pwd)/$(basename "$seed_sql")"
psql_en_docker -e "PGDATABASE=$base" \
  -v "${seed_sql_abs}:/seed.sql:ro" \
  "$IMAGEN_PSQL" \
  psql -v ON_ERROR_STOP=1 -f /seed.sql

log_info msg="seed OK" ambiente="$ambiente" base="$base"
