#!/usr/bin/env bash
# Verifica al candidato del host, no una respuesta cacheada del ingress público.
source "$(dirname "$0")/_comun.sh"
ambiente="${1:-}"
validar_ambiente "$ambiente"
: "${REGISTRO:?}" "${TAG_BACKEND:?}" "${TAG_FRONTEND:?}" "${APP_DB_USER:?}" "${APP_DB_PASSWORD:?}"
for servicio in frontend backend; do
  id="$(docker ps -q --filter "label=com.docker.compose.project=$ambiente" --filter "label=com.docker.compose.service=$servicio")"
  [[ -n "$id" && "$id" != *$'\n'* ]] || fatal 'msg="smoke: falta contenedor único"'
  imagen="$(docker inspect --format '{{.Config.Image}}' "$id")"
  variable="TAG_${servicio^^}"
  [[ "$imagen" == "$REGISTRO/arsdocendi-$servicio:${!variable}" ]] || fatal 'msg="smoke: imagen no corresponde al SHA candidato"'
  [[ "$servicio" != backend ]] || backend_id="$id"
done
intentos="${SMOKE_ATTEMPTS:-30}"
[[ "$intentos" =~ ^[1-9][0-9]*$ ]] || fatal 'msg="SMOKE_ATTEMPTS inválido"'
for modulo in designaciones aulas portal tareas; do
  comprobado=false
  for ((i=0; i<intentos; i++)); do
    if respuesta="$(docker run --rm --network "container:$backend_id" --entrypoint wget "$IMAGEN_PSQL" \
      -q -T 5 -O - "http://127.0.0.1:8080/api/$modulo/ping")" && \
      printf '%s' "$respuesta" | python3 -c 'import json,sys; x=json.load(sys.stdin); assert x["status"] == "ok" and x["module"].lower() == sys.argv[1]' "$modulo" 2>/dev/null; then
      comprobado=true
      break
    fi
    (( i + 1 >= intentos )) || sleep 2
  done
  [[ "$comprobado" == true ]] || fatal 'msg="smoke: ping de módulo fallido"'
done
base="$(nombre_base "$ambiente")"
# Misma identidad de app, read-only: no basta con que admin alcance PostgreSQL.
identidad="$(psql_en_docker -e "PGDATABASE=$base" -e "PGUSER=$APP_DB_USER" -e "PGPASSWORD=$APP_DB_PASSWORD" \
  "$IMAGEN_PSQL" psql -X -qtA -v ON_ERROR_STOP=1 \
  -c "BEGIN READ ONLY; SELECT current_database() || ':' || current_user || ':ok'; COMMIT;")"
[[ "$identidad" == "$base:$APP_DB_USER:ok" ]] || fatal 'msg="smoke: identidad o conectividad DB incorrecta"'
log_info msg="smoke OK; SHA, pings e identidad DB verificados" ambiente="$ambiente"
