#!/usr/bin/env bash
# Levanta (o actualiza) un ambiente completo: base + contenedores + migraciones + seed.
# Idempotente: re-ejecutar sobre un ambiente existente lo actualiza sin duplicar.
#
# Uso:
#   spin-up.sh <ambiente>          # ambiente: prod | staging | pr-<N>
#
# Variables requeridas (se inyectan en runtime / CI, nunca al repo):
#   DOMINIO                           dominio público real (reemplaza example.net)
#   REGISTRO TAG_FRONTEND TAG_BACKEND referencia de imágenes
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales ADMIN de Postgres (libpq)
#   APP_DB_USER APP_DB_PASSWORD       rol/password de la app para este ambiente
#   ASISTENTE_RO_PASSWORD             password del rol de lectura del asistente
#   ASISTENTE_RO_PII_PASSWORD         password del rol de lectura con datos personales
# Variables opcionales:
#   ASPNETCORE_ENVIRONMENT            default Production
#   DEVELOPMENT_AUTHENTICATION_ENABLED default false
#   COMANDO_MIGRACIONES               cómo el backend corre migraciones EF
#                                     (default: "dotnet ArsDocendi.Host.dll --migrate";
#                                      la app debe soportar este arg — trabajo adyacente)

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"

: "${DOMINIO:?msg=\"falta DOMINIO\"}"
: "${REGISTRO:?msg=\"falta REGISTRO\"}"
: "${TAG_FRONTEND:?msg=\"falta TAG_FRONTEND\"}"
: "${TAG_BACKEND:?msg=\"falta TAG_BACKEND\"}"
: "${APP_DB_USER:?msg=\"falta APP_DB_USER\"}"
: "${APP_DB_PASSWORD:?msg=\"falta APP_DB_PASSWORD\"}"
: "${ASISTENTE_RO_PASSWORD:?msg=\"falta ASISTENTE_RO_PASSWORD\"}"
: "${ASISTENTE_RO_PII_PASSWORD:?msg=\"falta ASISTENTE_RO_PII_PASSWORD\"}"

scripts_dir="$(cd "$(dirname "$0")" && pwd)"
compose_file="$(cd "$scripts_dir/../compose" && pwd)/compose.base.yml"

base="$(nombre_base "$ambiente")"
rol_ro="$(rol_asistente "$ambiente" basico)"
rol_ro_pii="$(rol_asistente "$ambiente" pii)"
host_publico="${ambiente}.${DOMINIO}"
url_base="Host=${PGHOST};Port=${PGPORT:-5432};Database=${base};Username=${APP_DB_USER};Password=${APP_DB_PASSWORD}"

log_info msg="spin-up iniciado" ambiente="$ambiente" host="$host_publico" base="$base"

# 1. Base aislada del ambiente + roles que deben existir antes que las tablas.
"$scripts_dir/provision-db.sh" "$ambiente"

# 1b. Test de humo: los roles del asistente existen y nacieron sin privilegios
#     de escritura, ANTES de que corra ninguna migración.
"$scripts_dir/verificar-roles-asistente.sh" "$ambiente"

# 2. Materializar el Compose project con un .env efímero (fuera del repo).
env_file="$(mktemp)"
trap 'rm -f "$env_file"' EXIT
cat >"$env_file" <<EOF
AMBIENTE=${ambiente}
HOST_PUBLICO=${host_publico}
REGISTRO=${REGISTRO}
TAG_FRONTEND=${TAG_FRONTEND}
TAG_BACKEND=${TAG_BACKEND}
ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}
DEVELOPMENT_AUTHENTICATION_ENABLED=${DEVELOPMENT_AUTHENTICATION_ENABLED:-false}
URL_BASE_DATOS=${url_base}
ASISTENTE_ROL_BASICO=${rol_ro}
ASISTENTE_ROL_PII=${rol_ro_pii}
ASISTENTE_RO_PASSWORD=${ASISTENTE_RO_PASSWORD}
ASISTENTE_RO_PII_PASSWORD=${ASISTENTE_RO_PII_PASSWORD}
EOF

docker compose -p "$ambiente" --env-file "$env_file" -f "$compose_file" up -d

# 3. Migraciones EF (la app debe soportar el comando; ver runbook).
log_info msg="corriendo migraciones" ambiente="$ambiente"
docker compose -p "$ambiente" --env-file "$env_file" -f "$compose_file" \
  run --rm backend ${COMANDO_MIGRACIONES:-dotnet ArsDocendi.Host.dll --migrate}

# 3b. Mismo test de humo, ahora con las tablas creadas: ninguna migración le
#     dio al asistente un privilegio de mutación.
"$scripts_dir/verificar-roles-asistente.sh" "$ambiente"

# 4. Seed SOLO en ambientes no-prod (datos sintéticos / anonimizados).
if [[ "$ambiente" != "prod" ]]; then
  "$scripts_dir/seed.sh" "$ambiente"
else
  log_info msg="ambiente prod: no se siembra seed sintético" ambiente="prod"
fi

log_info msg="spin-up OK" ambiente="$ambiente" host="$host_publico"
