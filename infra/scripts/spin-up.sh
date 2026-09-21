#!/usr/bin/env bash
# Levanta un ambiente completo: reset descartable + base + migraciones + seed + servicios.
# staging y pr-N se reconstruyen desde cero; prod conserva su base y nunca recibe seed.
#
# Uso:
#   spin-up.sh <ambiente>          # ambiente: prod | staging | pr-<N>
#
# Variables requeridas (se inyectan en runtime / CI, nunca al repo):
#   DOMINIO                           dominio público real (reemplaza example.net)
#   REGISTRO TAG_FRONTEND TAG_BACKEND referencia de imágenes
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales ADMIN de Postgres (libpq)
#   APP_DB_USER APP_DB_PASSWORD       rol/password de la app para este ambiente
#   SEAWEEDFS_ROOT_ACCESS_KEY SEAWEEDFS_ROOT_SECRET_KEY credenciales administrativas del servicio privado
#   En prod y staging, las credenciales de aplicación se derivan de la raíz.
#   En pr-N se inyectan SEAWEEDFS_APP_ACCESS_KEY_PR_<N> /
#   SEAWEEDFS_APP_SECRET_KEY_PR_<N>.
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
: "${SEAWEEDFS_ROOT_ACCESS_KEY:?msg=\"falta SEAWEEDFS_ROOT_ACCESS_KEY\"}"
: "${SEAWEEDFS_ROOT_SECRET_KEY:?msg=\"falta SEAWEEDFS_ROOT_SECRET_KEY\"}"
variable_ambiente="${ambiente^^}"
variable_ambiente="${variable_ambiente//-/_}"
access_variable="SEAWEEDFS_APP_ACCESS_KEY_${variable_ambiente}"
secret_variable="SEAWEEDFS_APP_SECRET_KEY_${variable_ambiente}"
seaweedfs_app_access_key="${!access_variable:-${SEAWEEDFS_APP_ACCESS_KEY:-}}"
seaweedfs_app_secret_key="${!secret_variable:-${SEAWEEDFS_APP_SECRET_KEY:-}}"
if [[ -z "$seaweedfs_app_access_key" || -z "$seaweedfs_app_secret_key" ]]; then
  if [[ "$ambiente" == "prod" || "$ambiente" == "staging" ]]; then
    seaweedfs_app_access_key="$(seaweedfs_app_access_for "$ambiente")"
    seaweedfs_app_secret_key="$(seaweedfs_app_secret_for "$ambiente" "$SEAWEEDFS_ROOT_SECRET_KEY")"
  else
    fatal "msg=\"faltan credenciales SeaweedFS de aplicación\" variable=\"$access_variable/$secret_variable\""
  fi
fi
export SEAWEEDFS_APP_ACCESS_KEY="$seaweedfs_app_access_key"
export SEAWEEDFS_APP_SECRET_KEY="$seaweedfs_app_secret_key"
seaweedfs_host="$(seaweedfs_host_for "$ambiente")"
clamav_host="$(clamav_host_for "$ambiente")"

scripts_dir="$(cd "$(dirname "$0")" && pwd)"
compose_file="$(cd "$scripts_dir/../compose" && pwd)/compose.base.yml"

base="$(nombre_base "$ambiente")"
host_publico="${ambiente}.${DOMINIO}"

# Npgsql admite valores entre comillas dobles; una comilla interna se duplica.
# URL_BASE_DATOS se exporta al proceso de Compose para no serializar la clave en
# el archivo .env temporal, donde `$`, comillas y saltos tienen otra semántica.
valor_npgsql() {
  local valor="${1//\"/\"\"}"
  printf '"%s"' "$valor"
}
export URL_BASE_DATOS="Host=$(valor_npgsql "$PGHOST");Port=$(valor_npgsql "${PGPORT:-5432}");Database=$(valor_npgsql "$base");Username=$(valor_npgsql "$APP_DB_USER");Password=$(valor_npgsql "$APP_DB_PASSWORD")"

log_info msg="spin-up iniciado" ambiente="$ambiente" host="$host_publico" base="$base"

# Serializa reconstrucciones del mismo ambiente en el host. La CI también tiene
# concurrency por ambiente, pero este lock cubre reintentos/manuales simultáneos.
# ponytail: lock local por ambiente; si se distribuye el host, moverlo a un lock manager.
lock_file="${TMPDIR:-/tmp}/arsdocendi-spin-up-${ambiente//-/_}.lock"
exec 9>"$lock_file"
flock 9

# Materializar el Compose project con un .env efímero (fuera del repo).
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
ALMACENAMIENTO_ENDPOINT=${ALMACENAMIENTO_ENDPOINT:-${seaweedfs_host}:8333}
ALMACENAMIENTO_BUCKET=${SEAWEEDFS_BUCKET_PREFIX:-arsdocendi}-${ambiente}
ALMACENAMIENTO_ACCESS_KEY=${seaweedfs_app_access_key}
ALMACENAMIENTO_SECRET_KEY=${seaweedfs_app_secret_key}
ALMACENAMIENTO_RECHAZAR_SI_ANTIVIRUS_NO_DISPONIBLE=${ALMACENAMIENTO_RECHAZAR_SI_ANTIVIRUS_NO_DISPONIBLE:-true}
ALMACENAMIENTO_CLAMAV_HOST=${ALMACENAMIENTO_CLAMAV_HOST:-$clamav_host}
EOF

# 1. Los ambientes descartables parten de cero. Se detienen antes de dropear la
# base para no dejar contenedores publicados apuntando a una base reconstruida.
if [[ "$ambiente" != "prod" ]]; then
  docker compose -p "$ambiente" --env-file "$env_file" -f "$compose_file" \
    down -v --remove-orphans
  "$scripts_dir/drop-db.sh" "$ambiente"
  "$scripts_dir/purge-storage.sh" "$ambiente"
fi

# El servicio común queda arriba antes de migraciones y seed.
"$scripts_dir/provision-storage.sh" "$ambiente"

# 2. Base aislada del ambiente.
"$scripts_dir/provision-db.sh" "$ambiente"

# 3. Migraciones EF antes de publicar el backend. Una falla detiene seed/up por
# set -euo pipefail.
log_info msg="corriendo migraciones" ambiente="$ambiente"
docker compose -p "$ambiente" --env-file "$env_file" -f "$compose_file" \
  run --rm backend ${COMANDO_MIGRACIONES:-dotnet ArsDocendi.Host.dll --migrate}

# 4. Seed SOLO en ambientes no-prod (datos sintéticos / anonimizados).
if [[ "$ambiente" != "prod" ]]; then
  "$scripts_dir/seed.sh" "$ambiente"
else
  log_info msg="ambiente prod: no se siembra seed sintético" ambiente="prod"
fi

# 5. Publicar servicios únicamente después de completar migración y seed.
docker compose -p "$ambiente" --env-file "$env_file" -f "$compose_file" up -d

log_info msg="spin-up OK" ambiente="$ambiente" host="$host_publico"
