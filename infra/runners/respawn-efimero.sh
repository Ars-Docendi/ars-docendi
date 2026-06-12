#!/usr/bin/env bash
# Registra y corre UN runner self-hosted efímero, automáticamente.
#
# Flujo (one-shot): pide un registration-token FRESCO a la API de GitHub →
# registra el runner con --ephemeral → corre 1 job → sale. systemd
# (Restart=always) lo vuelve a lanzar, y en el próximo arranque se pide otro
# token y se registra de nuevo. Así NUNCA hay que regenerar runners a mano.
#
# Pensado para el unit template arsdocendi-runner-efimero@.service: cada instancia
# (@1, @2, ...) usa su propio RUNNER_DIR, lo que da concurrencia = nº de instancias.
#
# Variables (desde EnvironmentFile /etc/ars-docendi/runner.env, fuera del repo):
#   GH_OWNER        org/usuario dueño del repo (p. ej. Ars-Docendi)
#   GH_REPO         nombre del repo
#   GH_PAT          PAT fine-grained con permiso "Administration: Read and write"
#                   sobre el repo (o token de instalación de una GitHub App).
#   RUNNER_DIR      directorio del paquete del runner para ESTA instancia
#   RUNNER_LABELS   (opcional) default "arsdocendi,efimero"
#
# Requiere: curl, jq, y el paquete del runner ya extraído en RUNNER_DIR.

set -euo pipefail

: "${GH_OWNER:?falta GH_OWNER}"
: "${GH_REPO:?falta GH_REPO}"
: "${GH_PAT:?falta GH_PAT}"
: "${RUNNER_DIR:?falta RUNNER_DIR}"
labels="${RUNNER_LABELS:-arsdocendi,efimero}"

api="https://api.github.com/repos/${GH_OWNER}/${GH_REPO}/actions/runners/registration-token"

# Pide un registration-token efímero (caduca ~1h) a la API de GitHub.
obtener_runner_token() {
  curl -fsS -X POST \
    -H "Authorization: Bearer ${GH_PAT}" \
    -H "Accept: application/vnd.github+json" \
    -H "X-GitHub-Api-Version: 2022-11-28" \
    "$api" | jq -r '.token'
}

cd "$RUNNER_DIR"
nombre="arsdocendi-efimero-$(hostname)-$$"

# El runner efímero se autodesregistra al terminar el job; por las dudas limpiamos
# si salimos por error antes de correr (token nuevo porque el de registro ya caducó).
limpiar() { ./config.sh remove --token "$(obtener_runner_token)" >/dev/null 2>&1 || true; }
trap limpiar EXIT

# 1. Registrar con token fresco. --replace evita choques si quedó un registro viejo.
./config.sh --url "https://github.com/${GH_OWNER}/${GH_REPO}" \
            --token "$(obtener_runner_token)" \
            --name "$nombre" \
            --labels "$labels" \
            --ephemeral --unattended --replace

# 2. Correr exactamente 1 job y salir. systemd reinicia el unit → nuevo ciclo.
./run.sh
