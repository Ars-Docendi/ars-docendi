#!/usr/bin/env bash
# scripts/eval-ollama.sh — Corre el evaluador del asistente contra el servicio de
# inferencia compartido (Ollama en la GPU de pc-prod), desde vm-dev.
#
# Qué hace, sin tocar ninguna base ni contenedor existente:
#   1. Copia la configuración Asistente__*/Inferencia__* de un backend ya
#      desplegado con el proveedor ollama (por defecto, el preview pr-40). Las
#      credenciales van a un archivo temporal 600 y nunca se imprimen.
#   2. Levanta un PostgreSQL descartable en una red Docker propia y aprovisiona
#      dos bases con infra/scripts/provision-db.sh: una con el fixture de siempre
#      (cuatro ejes) y otra con el suplemento de las compuestas.
#   3. Migra las dos con el Host de este checkout y les aplica el fixture.
#   4. Corre las pasadas pedidas y copia los reportes a una carpeta por pasada,
#      porque cada corrida del evaluador pisa la anterior.
#   5. Borra el PostgreSQL, la red y el archivo de credenciales al salir.
#
# Uso (desde la raíz del repo, en vm-dev, con el checkout en la rama a medir):
#   scripts/eval-ollama.sh                        # las tres pasadas
#   PASADAS="control plan" scripts/eval-ollama.sh # sólo las compuestas
#
# Variables opcionales:
#   CONTENEDOR_REFERENCIA  backend del que se copia la configuración (pr-40-backend-1)
#   PASADAS                subconjunto de "ejes control plan" (las tres)
#   SALIDA                 carpeta de reportes (backend/eval/reportes/ollama-<fecha>)
#   EVAL_INFERENCIA_USUARIO  usuario htpasswd propio del evaluador; pide su contraseña
#   ARGS_SDK               argumentos extra para el contenedor del SDK (p. ej. un proxy)
#
# Ocupa el único slot de la GPU, que comparte con prod: correrlo en una ventana
# acordada. Ver docs/architecture/modelo-local.md y docs/operations/ollama-compartido.md.

set -euo pipefail

raiz="$(cd "$(dirname "$0")/.." && pwd)"
cd "$raiz"

CONTENEDOR_REFERENCIA="${CONTENEDOR_REFERENCIA:-pr-40-backend-1}"
PASADAS="${PASADAS:-ejes control plan}"
SALIDA="${SALIDA:-backend/eval/reportes/ollama-$(date -u +%Y%m%dT%H%M%SZ)}"
IMAGEN_SDK="${IMAGEN_SDK:-mcr.microsoft.com/dotnet/sdk:10.0}"
IMAGEN_POSTGRES="${IMAGEN_POSTGRES:-postgres:18-alpine}"

log() { echo "ts=$(date -u +%Y-%m-%dT%H:%M:%SZ) $*" >&2; }
fatal() { log "nivel=ERROR $*"; exit 1; }

for pasada in $PASADAS; do
  [[ "$pasada" =~ ^(ejes|control|plan)$ ]] || fatal "msg=\"pasada desconocida: $pasada\" esperado=\"ejes control plan\""
done
command -v docker >/dev/null || fatal 'msg="falta docker"'

# ── 1. Configuración del backend de referencia ──────────────────────────────
docker inspect "$CONTENEDOR_REFERENCIA" >/dev/null 2>&1 ||
  fatal "msg=\"no existe el contenedor $CONTENEDOR_REFERENCIA\" ayuda=\"CONTENEDOR_REFERENCIA=<backend con ollama>\""

sufijo="$$"
trabajo="$(mktemp -d)"
chmod 700 "$trabajo"
red="eval-ollama-$sufijo"
pg="eval-ollama-pg-$sufijo"

limpiar() {
  docker rm -f "$pg" >/dev/null 2>&1 || true
  docker network rm "$red" >/dev/null 2>&1 || true
  rm -rf "$trabajo"
}
trap limpiar EXIT

# Sólo el modelo y el servicio: los roles, las contraseñas de lectura y los
# cassettes son del ambiente de referencia y no sirven contra la base descartable.
# PlanCompilado lo decide cada pasada.
docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "$CONTENEDOR_REFERENCIA" |
  grep -E '^(Asistente|Inferencia)__' |
  grep -vE '^Asistente__(RolSoloLectura|RolSoloLecturaPii|PasswordSoloLectura|PasswordSoloLecturaPii|DirectorioDeCassettes|RegrabarCassettes|PlanCompilado)=' \
  > "$trabajo/modelo.env"
chmod 600 "$trabajo/modelo.env"

grep -qx 'Asistente__Proveedor=ollama' "$trabajo/modelo.env" ||
  fatal "msg=\"$CONTENEDOR_REFERENCIA no corre con el proveedor ollama\" ayuda=\"desplegarlo con la label asistente-ollama\""
grep -q '^Inferencia__Url=.' "$trabajo/modelo.env" ||
  fatal "msg=\"$CONTENEDOR_REFERENCIA no tiene Inferencia__Url\""

if [[ -n "${EVAL_INFERENCIA_USUARIO:-}" ]]; then
  read -rsp "Contraseña de $EVAL_INFERENCIA_USUARIO: " contrasena_eval; echo >&2
  grep -vE '^Inferencia__(Usuario|Contrasena)=' "$trabajo/modelo.env" > "$trabajo/modelo.tmp"
  printf 'Inferencia__Usuario=%s\nInferencia__Contrasena=%s\n' \
    "$EVAL_INFERENCIA_USUARIO" "$contrasena_eval" >> "$trabajo/modelo.tmp"
  mv "$trabajo/modelo.tmp" "$trabajo/modelo.env"
  chmod 600 "$trabajo/modelo.env"
  unset contrasena_eval
fi

usuario_inferencia="$(grep -m1 '^Inferencia__Usuario=' "$trabajo/modelo.env" | cut -d= -f2-)"
modelo="$(grep -m1 '^Asistente__Modelo=' "$trabajo/modelo.env" | cut -d= -f2-)"
log "msg=\"configuración copiada\" referencia=$CONTENEDOR_REFERENCIA modelo=${modelo:-?} usuario=${usuario_inferencia:-?}"

# ── 2. PostgreSQL descartable ───────────────────────────────────────────────
secreto() { head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 24; }
pg_admin_pw="$(secreto)"

docker network create "$red" >/dev/null
docker run -d --name "$pg" --network "$red" \
  -e POSTGRES_PASSWORD="$pg_admin_pw" "$IMAGEN_POSTGRES" >/dev/null
# Por TCP: durante la inicialización la imagen levanta un servidor temporal sólo
# en el socket local, y responder por ahí no significa que ya terminó.
listo=false
for _ in $(seq 1 60); do
  if docker exec "$pg" pg_isready -h 127.0.0.1 -U postgres -q 2>/dev/null; then listo=true; break; fi
  sleep 1
done
$listo || fatal 'msg="PostgreSQL no arrancó"'

# Compilar una vez el Host y el evaluador. NuGet en un volumen para no bajarlo
# en cada corrida; bin/obj quedan en el checkout con el usuario de quien corre.
docker volume create eval-ollama-nuget >/dev/null
# shellcheck disable=SC2086 # ARGS_SDK se parte en palabras a propósito
sdk() {
  docker run --rm -i --network "$red" \
    --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp \
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 -e NUGET_PACKAGES=/nuget \
    -v eval-ollama-nuget:/nuget -v "$raiz":/repo -w /repo ${ARGS_SDK:-} "$@"
}
docker run --rm -v eval-ollama-nuget:/nuget "$IMAGEN_SDK" chown "$(id -u):$(id -g)" /nuget
log 'msg="compilando Host y evaluador"'
for proyecto in backend/src/ArsDocendi.Host backend/eval/ArsDocendi.Evaluacion; do
  sdk "$IMAGEN_SDK" dotnet build "$proyecto" -c Release -v q -nologo > "$trabajo/build.log" 2>&1 || {
    grep -E 'error' "$trabajo/build.log" | head -20 >&2
    fatal "msg=\"no compiló $proyecto\""
  }
done

# ── 3. Una base por fixture ─────────────────────────────────────────────────
# Ambientes pr-9001/pr-9002 SÓLO dentro del PostgreSQL descartable: los nombres
# de base y de roles salen de provision-db.sh y no chocan con nada de vm-dev.
preparar_base() {
  local ambiente="$1" fixture_args="$2"
  local n="${ambiente#pr-}"
  local app_pw ro_pw ro_pii_pw
  app_pw="$(secreto)"; ro_pw="$(secreto)"; ro_pii_pw="$(secreto)"

  RED_DATOS="$red" PGHOST="$pg" PGPORT=5432 PGUSER=postgres PGPASSWORD="$pg_admin_pw" \
  APP_DB_USER="app_pr_$n" APP_DB_PASSWORD="$app_pw" \
  ASISTENTE_RO_PASSWORD="$ro_pw" ASISTENTE_RO_PII_PASSWORD="$ro_pii_pw" \
    infra/scripts/provision-db.sh "$ambiente" 2>/dev/null

  # Mismas claves que compose.base.yml, apuntando a la base descartable.
  cat > "$trabajo/base-$ambiente.env" <<EOF
ConnectionStrings__ArsDocendi=Host=$pg;Port=5432;Database=arsdocendi_pr_$n;Username=app_pr_$n;Password=$app_pw
Asistente__RolSoloLectura=asistente_ro_pr_$n
Asistente__RolSoloLecturaPii=asistente_ro_pii_pr_$n
Asistente__PasswordSoloLectura=$ro_pw
Asistente__PasswordSoloLecturaPii=$ro_pii_pw
EOF
  chmod 600 "$trabajo/base-$ambiente.env"

  log "msg=\"migrando\" base=arsdocendi_pr_$n"
  # Desde la carpeta de salida, como en la imagen: el Host toma ahí su appsettings.
  sdk --env-file "$trabajo/modelo.env" --env-file "$trabajo/base-$ambiente.env" \
    -e ASPNETCORE_ENVIRONMENT=Production -w /repo/backend/src/ArsDocendi.Host/bin/Release/net10.0 \
    "$IMAGEN_SDK" dotnet ArsDocendi.Host.dll --migrate >/dev/null

  log "msg=\"aplicando fixture\" base=arsdocendi_pr_$n args=\"$fixture_args\""
  # shellcheck disable=SC2086 # fixture_args es una lista de flags fija
  sdk "$IMAGEN_SDK" dotnet backend/eval/ArsDocendi.Evaluacion/bin/Release/net10.0/ArsDocendi.Evaluacion.dll \
    --fixture $fixture_args |
    docker run --rm -i --network "$red" -e PGPASSWORD="$app_pw" "$IMAGEN_POSTGRES" \
      psql -q -v ON_ERROR_STOP=1 -h "$pg" -U "app_pr_$n" -d "arsdocendi_pr_$n" >/dev/null
}

necesita_ejes=false; necesita_compuestas=false
for pasada in $PASADAS; do
  case "$pasada" in
    ejes) necesita_ejes=true ;;
    control|plan) necesita_compuestas=true ;;
  esac
done
$necesita_ejes && preparar_base pr-9001 ""
$necesita_compuestas && preparar_base pr-9002 "--compuestas"

# ── 4. Pasadas ──────────────────────────────────────────────────────────────
mkdir -p "$SALIDA"
resumen="$SALIDA/resumen.txt"
: > "$resumen"

correr() {
  local nombre="$1" ambiente="$2" plan="$3"; shift 3
  local inicio fin codigo=0
  log "msg=\"pasada\" nombre=$nombre plan_compilado=$plan"
  rm -f backend/eval/reportes/*.md backend/eval/reportes/*.jsonl
  inicio=$(date +%s)
  sdk --env-file "$trabajo/modelo.env" --env-file "$trabajo/base-$ambiente.env" \
    -e Asistente__PlanCompilado="$plan" "$IMAGEN_SDK" \
    dotnet backend/eval/ArsDocendi.Evaluacion/bin/Release/net10.0/ArsDocendi.Evaluacion.dll "$@" \
    2>&1 | tee "$SALIDA/$nombre.log" || codigo=${PIPESTATUS[0]}
  fin=$(date +%s)
  mkdir -p "$SALIDA/$nombre"
  cp backend/eval/reportes/*.md backend/eval/reportes/*.jsonl "$SALIDA/$nombre/" 2>/dev/null || true
  # 0 = sin regresión, 4 = el gate encontró regresión contra la línea de base:
  # las dos son corridas válidas. 3 = el preflight rechazó el proveedor.
  echo "$nombre: código $codigo, $((fin - inicio)) s" | tee -a "$resumen" >&2
  [[ "$codigo" != 3 ]] ||
    fatal "msg=\"el preflight rechazó el proveedor; ver $SALIDA/$nombre.log\" ayuda=\"servicio, credenciales o modelo\""
}

for pasada in $PASADAS; do
  case "$pasada" in
    ejes)    correr ejes    pr-9001 false ;;
    control) correr control pr-9002 false --compuestas ;;
    plan)    correr plan    pr-9002 true  --compuestas ;;
  esac
done

{
  echo "modelo: ${modelo:-?}"
  echo "referencia: $CONTENEDOR_REFERENCIA"
  echo "commit: $(git rev-parse --short HEAD 2>/dev/null || echo ?)"
} >> "$resumen"
log "msg=\"listo\" reportes=$SALIDA"
