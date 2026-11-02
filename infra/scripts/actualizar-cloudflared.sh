#!/usr/bin/env bash
# Actualiza el contenedor cloudflared del host si hay imagen nueva. Pensado para cron.
#
#   infra/scripts/actualizar-cloudflared.sh
#
# Recrea el contenedor con los mismos argumentos de infra/README.md §4 (túnel
# local). Valida el ingress con la imagen nueva antes del corte y, si el
# contenedor nuevo no queda corriendo, restaura el anterior.
source "$(dirname "$0")/_comun.sh"
IMAGEN="${CLOUDFLARED_IMAGEN:-cloudflare/cloudflared:latest}"
NOMBRE=cloudflared
CONF=/etc/cloudflared

docker pull -q "$IMAGEN" >/dev/null
nueva="$(docker image inspect -f '{{.Id}}' "$IMAGEN")"
actual="$(docker inspect -f '{{.Image}}' "$NOMBRE")" || fatal 'msg="no existe el contenedor cloudflared"'
if [[ "$nueva" == "$actual" ]]; then
  log_info 'msg="cloudflared ya está actualizado"'
  exit 0
fi

docker run --rm -v "$CONF/config.yml:$CONF/config.yml:ro" \
  "$IMAGEN" tunnel --config "$CONF/config.yml" ingress validate >/dev/null \
  || fatal 'msg="la imagen nueva rechaza el ingress; se conserva la actual"'

docker rm -f "$NOMBRE-anterior" >/dev/null 2>&1 || true
docker stop "$NOMBRE" >/dev/null
docker rename "$NOMBRE" "$NOMBRE-anterior"
docker run -d --name "$NOMBRE" --network traefik --restart unless-stopped \
  -v "$CONF/config.yml:$CONF/config.yml:ro" \
  -v "$CONF/credentials.json:$CONF/credentials.json:ro" \
  "$IMAGEN" tunnel --config "$CONF/config.yml" run >/dev/null || true

sleep 15
if [[ "$(docker inspect -f '{{.State.Running}}' "$NOMBRE" 2>/dev/null)" != true ]]; then
  docker rm -f "$NOMBRE" >/dev/null 2>&1 || true
  docker rename "$NOMBRE-anterior" "$NOMBRE"
  docker start "$NOMBRE" >/dev/null
  fatal 'msg="cloudflared nuevo no arrancó; se restauró el anterior"'
fi
docker rm "$NOMBRE-anterior" >/dev/null
docker image rm "$actual" >/dev/null 2>&1 || true
log_info "msg=\"cloudflared actualizado\" imagen=${nueva:7:12}"
