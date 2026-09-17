#!/usr/bin/env bash
# Reaper: destruye ambientes pr-N huérfanos más viejos que N días (D9).
# Red de seguridad por si se pierde el webhook de cierre de PR.
#
# NUNCA toca prod ni staging: solo considera Compose projects con forma pr-<N>,
# y delega la destrucción a teardown.sh (que vuelve a validar el prefijo).
#
# Uso:
#   reap-pr-envs.sh
#
# Variables:
#   REAPER_MAX_DIAS                   antigüedad máxima en días (default 7)
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (para drop)

source "$(dirname "$0")/../scripts/_comun.sh"

max_dias="${REAPER_MAX_DIAS:-7}"
ahora="$(date +%s)"
scripts_dir="$(cd "$(dirname "$0")/../scripts" && pwd)"

pr_abierto() {
  local numero="$1" respuesta
  if [[ -z "${GITHUB_REPOSITORY:-}" ]]; then
    log_error msg="falta GITHUB_REPOSITORY; se preserva el ambiente" pr="$numero"
    return 2
  fi
  local cabeceras=(-H "Accept: application/vnd.github+json")
  if [[ -n "${GITHUB_TOKEN:-}" ]]; then
    cabeceras+=(-H "Authorization: Bearer ${GITHUB_TOKEN}")
  fi
  if ! respuesta="$(curl --fail --silent --show-error "${cabeceras[@]}" \
      "https://api.github.com/repos/${GITHUB_REPOSITORY}/pulls/${numero}")"; then
    log_error msg="no se pudo consultar el PR; se preserva el ambiente" pr="$numero"
    return 2
  fi
  grep -q '"state": "open"' <<< "$respuesta"
}

log_info msg="reaper iniciado" max_dias="$max_dias"

# Compose projects con forma pr-<N> (incluye los detenidos: --all).
projects="$(docker compose ls --all 2>/dev/null | awk 'NR>1 {print $1}' | grep -E '^pr-[0-9]+$' || true)"

if [[ -z "$projects" ]]; then
  log_info msg="no hay ambientes pr-N, nada que reapear"
  exit 0
fi

while IFS= read -r p; do
  [[ -n "$p" ]] || continue

  # Antigüedad = creación del contenedor más viejo del project.
  creado_raw="$(docker ps -a --filter "label=com.docker.compose.project=${p}" \
                  --format '{{.CreatedAt}}' | sort | head -1)"
  if [[ -z "$creado_raw" ]]; then
    log_warn msg="project sin contenedores, se ignora" project="$p"
    continue
  fi

  creado_epoch="$(date -d "$creado_raw" +%s 2>/dev/null || echo 0)"
  if [[ "$creado_epoch" -eq 0 ]]; then
    log_warn msg="no se pudo parsear la creación, se ignora" project="$p" creado="$creado_raw"
    continue
  fi

  edad_dias=$(( (ahora - creado_epoch) / 86400 ))

  if (( edad_dias > max_dias )); then
    numero="${p#pr-}"
    if pr_abierto "$numero"; then
      log_info msg="PR aún abierto, se preserva" project="$p" edad_dias="$edad_dias"
      continue
    else
      estado_pr=$?
      if (( estado_pr == 2 )); then
        continue
      fi
    fi
    log_warn msg="ambiente vencido, destruyendo" project="$p" edad_dias="$edad_dias" umbral="$max_dias"
    if "$scripts_dir/teardown.sh" "$p"; then
      log_info msg="ambiente reapeado" project="$p"
    else
      log_error msg="fallo al reapear ambiente" project="$p"
    fi
  else
    log_info msg="ambiente vigente, se preserva" project="$p" edad_dias="$edad_dias"
  fi
done <<< "$projects"

log_info msg="reaper finalizado"
