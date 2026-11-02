#!/usr/bin/env bash
# Reconstrucción MANUAL no productiva: no se llama desde deploy ordinario.
source "$(dirname "$0")/_comun.sh"
ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"
[[ "${RESET_AUTHORIZED:-}" == "$ambiente" ]] || fatal 'msg="RESET_AUTHORIZED debe coincidir con ambiente; operación destructiva explícita"'
adquirir_lock_ambiente "$ambiente"
trap liberar_lock_ambiente EXIT
"$(dirname "$0")/teardown.sh" "$ambiente"
"$(dirname "$0")/spin-up.sh" "$ambiente"
# Los backups no forman parte del teardown/reset; permanecen en su volumen.
