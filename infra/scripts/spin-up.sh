#!/usr/bin/env bash
# Actualiza un ambiente persistente: preflight/preview, backup, migraciones y smoke.
# Nunca reset/drop/purge en deploy ordinario; prod nunca recibe seed.
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
#   BACKUP_VOLUME_PREFIX              default arsdocendi-backups
#   BACKUP_RETENTION_DAYS             default 7, entero positivo

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
validar_ambiente "$ambiente"

: "${DOMINIO:?msg=\"falta DOMINIO\"}"
: "${REGISTRO:?msg=\"falta REGISTRO\"}"
: "${TAG_FRONTEND:?msg=\"falta TAG_FRONTEND\"}"
: "${TAG_BACKEND:?msg=\"falta TAG_BACKEND\"}"
[[ "$TAG_BACKEND" =~ ^sha-[a-f0-9]{40}$ && "$TAG_FRONTEND" == "$TAG_BACKEND" ]] || fatal 'msg="se requieren imágenes de la misma release por SHA completo"'
export RELEASE_SHA="${TAG_BACKEND#sha-}"
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
host_publico="$(hostname_publico "$ambiente" "$DOMINIO")"

# Npgsql admite valores entre comillas dobles; una comilla interna se duplica.
# URL_BASE_DATOS se exporta al proceso de Compose para no serializar la clave en
# el archivo .env temporal, donde `$`, comillas y saltos tienen otra semántica.
valor_npgsql() {
  local valor="${1//\"/\"\"}"
  printf '"%s"' "$valor"
}
export URL_BASE_DATOS="Host=$(valor_npgsql "$PGHOST");Port=$(valor_npgsql "${PGPORT:-5432}");Database=$(valor_npgsql "$base");Username=$(valor_npgsql "$APP_DB_USER");Password=$(valor_npgsql "$APP_DB_PASSWORD")"

log_info msg="spin-up iniciado" ambiente="$ambiente" host="$host_publico" base="$base"

# Serializa deploy, backup y teardown del mismo ambiente en el daemon. La CI tiene
# concurrency por ambiente, pero este lock cubre reintentos/manuales simultáneos.
# Cada ambiente tiene un único host de deploy; el lock es local a ese destino.
adquirir_lock_ambiente "$ambiente"
trap liberar_lock_ambiente EXIT

# Materializar el Compose project con un .env efímero (fuera del repo).
env_file="$(mktemp)"
trap 'rm -f "$env_file"; liberar_lock_ambiente' EXIT
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

compose() { docker compose -p "$ambiente" --env-file "$env_file" -f "$compose_file" "$@"; }
# Validar el ciclo de vida bajo el mismo lock que teardown, también en reintentos.
if [[ "$ambiente" == pr-* && -n "${GITHUB_API_URL:-}" ]]; then
  : "${GH_TOKEN:?falta token para revalidar PR}"
  pr_estado="$(curl -fsS -H "Authorization: Bearer $GH_TOKEN" \
    "$GITHUB_API_URL/repos/$GITHUB_REPOSITORY/pulls/${ambiente#pr-}")"
  [[ "$(printf '%s' "$pr_estado" | python3 -c 'import json,sys; print(json.load(sys.stdin)["state"])')" == open ]] ||
    fatal 'msg="PR cerrado: publicación omitida"'
fi
exigir_configuracion_backups
base_nueva=false
if ! existe_base "$base"; then base_nueva=true; fi
"$scripts_dir/provision-db.sh" "$ambiente"
# Consultas y preview de la misma imagen candidata, sin listener ni binds.
trabajo="$(mktemp -d)"
backup_id=""
critica=false
finalizado=false
limpiar() {
  codigo=$?
  if [[ "$finalizado" != true && "$critica" == true ]]; then
    compose stop backend >&2 || true
    log_error msg="deploy abortado; backend detenido; recuperación manual" ambiente="$ambiente" backup="$backup_id"
  fi
  rm -f "$env_file"
  rm -rf "$trabajo"
  liberar_lock_ambiente
  exit "$codigo"
}
trap limpiar EXIT
compose run --rm -T --no-deps backend dotnet ArsDocendi.Host.dll --estado-migraciones > "$trabajo/estado.json"
pendientes="$(python3 "$scripts_dir/estado-migraciones.py" "$trabajo/estado.json" "$base")"
compose run --rm -T --no-deps backend dotnet ArsDocendi.Host.dll --script-migraciones - > "$trabajo/preview.tar"
# El tar es del CLI candidato; no permitir traversal, links ni dispositivos.
python3 - "$trabajo" <<'PY'
import hashlib, json, os, pathlib, sys, tarfile
p = pathlib.Path(sys.argv[1])
with tarfile.open(p / 'preview.tar') as t:
    for m in t.getmembers():
        if m.name.startswith('/') or '..' in pathlib.PurePosixPath(m.name).parts or not (m.isfile() or m.isdir()):
            raise SystemExit('preview inseguro')
    t.extractall(p / 'preview', filter='data')
if not (p / 'preview/manifiesto.json').is_file():
    raise SystemExit('preview sin manifiesto')
manifest = json.loads((p / 'preview/manifiesto.json').read_text())
observado = json.loads((p / 'estado.json').read_text())
if manifest.get('formato') != 'arsdocendi-migraciones/v1' or manifest.get('sha') != os.environ['RELEASE_SHA'] or manifest.get('contextos') != observado['contextos']:
    raise SystemExit('preview no corresponde a release/estado observado')
for script in manifest.get('scripts', []):
    nombre = script['archivo']
    if pathlib.PurePosixPath(nombre).name != nombre or not nombre.endswith('.sql'):
        raise SystemExit('ruta SQL insegura')
    if hashlib.sha256((p / 'preview' / nombre).read_bytes()).hexdigest() != script['sha256']:
        raise SystemExit('hash SQL de preview incorrecto')
PY
release_preview="${TAG_BACKEND#sha-}-$(openssl rand -hex 8)"
tar -C "$trabajo" -cf - estado.json preview | "$scripts_dir/backup-volume.sh" "$ambiente" preview "$release_preview"
# Credenciales PR rotan por ejecución: parar antes de reconfigurar, nunca purgar.
if (( pendientes > 0 )) || [[ "$ambiente" == pr-* ]]; then
  critica=true
  compose stop backend
fi
if (( pendientes > 0 )) && [[ "$base_nueva" != true ]]; then
  backup_id="$("$scripts_dir/backup-storage.sh" "$ambiente")"
elif [[ "$base_nueva" == true ]]; then
  log_info msg="base nueva; no existe estado previo para backup" ambiente="$ambiente"
fi
"$scripts_dir/provision-storage.sh" "$ambiente"
compose run --rm -T --no-deps backend dotnet ArsDocendi.Host.dll --estado-migraciones > "$trabajo/revalidado.json"
python3 "$scripts_dir/estado-migraciones.py" "$trabajo/estado.json" "$base" "$trabajo/revalidado.json" >/dev/null
if (( pendientes > 0 )); then
  critica=true
  compose run --rm -T --no-deps backend dotnet ArsDocendi.Host.dll --migrate
fi
if [[ "$ambiente" != prod ]]; then
  SEED_BASE_CREATED="$base_nueva" "$scripts_dir/seed.sh" "$ambiente"
fi
critica=true
compose up -d
"$scripts_dir/smoke.sh" "$ambiente"
compose run --rm -T --no-deps backend dotnet ArsDocendi.Host.dll --estado-migraciones > "$trabajo/final.json"
[[ "$(python3 "$scripts_dir/estado-migraciones.py" "$trabajo/final.json" "$base")" == 0 ]] || fatal 'msg="persisten migraciones pendientes"'
# Recibo privado incluye SHA, historia y vínculo al backup; sólo después del smoke.
python3 - "$trabajo/final.json" "$ambiente" "$TAG_BACKEND" "$backup_id" <<'PY' | "$scripts_dir/backup-volume.sh" "$ambiente" receipt
import json, sys
with open(sys.argv[1]) as f:
    print(json.dumps({'ambiente': sys.argv[2], 'release': sys.argv[3], 'backup': sys.argv[4] or None, 'estado': json.load(f)}))
PY
if [[ -n "$backup_id" ]]; then "$scripts_dir/backup-volume.sh" "$ambiente" success "$backup_id"; fi
"$scripts_dir/backup-volume.sh" "$ambiente" retain
finalizado=true
log_info msg="spin-up OK" ambiente="$ambiente" host="$host_publico" release="$TAG_BACKEND" backup="$backup_id"
