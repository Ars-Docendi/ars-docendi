#!/usr/bin/env bash
# Seed único: bootstrap reconocido + marcador dataset transaccional, nunca reseed.
source "$(dirname "$0")/_comun.sh"
ambiente="${1:-}"
validar_ambiente "$ambiente"
[[ "$ambiente" != prod ]] || fatal 'msg="PROHIBIDO ejecutar fixtures en prod"'
[[ -z "${SEED_FROM_DB:-}" ]] || fatal 'msg="PROHIBIDO copiar bases como origen del seed"'
base="$(nombre_base "$ambiente")"
scripts_dir="$(cd "$(dirname "$0")" && pwd)"
seed_sql="${SEED_SQL:-$scripts_dir/seed-data/sintetico.sql}"
[[ "${seed_sql,,}" != *sga* ]] || fatal 'msg="SGA reservado al setup local privado"'
# El argumento SEED_BASE_CREATED no autoriza por sí solo: la procedencia se
# verifica en public.bootstrap_metadata, registrada por provision-db/v1.
adquirir_lock_ambiente "$ambiente"
trabajo="$(mktemp -d)"
trap 'rm -rf "$trabajo"; liberar_lock_ambiente' EXIT
{
  cat "$scripts_dir/seed-guard.sql"
  printf '\nCOMMIT;\nSELECT estado FROM public.bootstrap_metadata WHERE id;\n'
} | psql_en_docker -e "PGDATABASE=$base" "$IMAGEN_PSQL" \
  psql -X -qtA -v ON_ERROR_STOP=1 --set=ambiente="$ambiente" > "$trabajo/estado"
if [[ "$(tail -n 1 "$trabajo/estado")" == completado ]]; then
  log_info msg="seed ya completado; se conservan datos y objetos" ambiente="$ambiente"
  exit 0
fi
[[ -f "$seed_sql" ]] || fatal 'msg="dataset sintético inexistente"'
# Primero cargar bytes, luego confirmar dataset+metadata+marca en la misma TX.
# Una falla de upload nunca produce marcador completo. El backend no se publica.
SEED_STORAGE_SQL_ONLY=true "$scripts_dir/seed-storage.sh" "$ambiente" > "$trabajo/storage.sql"
python3 "$scripts_dir/seed-transaccion.py" "$seed_sql" "$trabajo/storage.sql" > "$trabajo/seed.sql"
psql_en_docker -e "PGDATABASE=$base" "$IMAGEN_PSQL" \
  psql -X -q -v ON_ERROR_STOP=1 --set=ambiente="$ambiente" < "$trabajo/seed.sql" >/dev/null
log_info msg="seed OK; inicializacion_completada" ambiente="$ambiente"
