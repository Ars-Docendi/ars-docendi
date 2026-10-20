#!/usr/bin/env bash
# Volumen privado del host: streams por stdin/stdout, nunca binds del runner.
source "$(dirname "$0")/_comun.sh"
ambiente="${1:-}"
validar_ambiente "$ambiente"
exigir_configuracion_backups
accion="${2:-}"
id="${3:-}"
volumen="${BACKUP_VOLUME_PREFIX}-${ambiente}"
if [[ -n "$id" ]]; then
  [[ "$id" =~ ^[a-zA-Z0-9][a-zA-Z0-9_-]*$ ]] || fatal 'msg="snapshot inválido"'
fi
vol() {
  docker run --rm -i --network none -v "$volumen:/backups" \
    --entrypoint /bin/sh "$IMAGEN_PSQL" -eu -c "$1" backup-volume "${@:2}"
}
case "$accion" in
  init)
    [[ -n "$id" ]] || fatal 'msg="falta snapshot"'
    docker volume create --label "arsdocendi.backups=$ambiente" "$volumen" >/dev/null
    vol 'umask 077; mkdir -p /backups/snapshots /backups/deploys /backups/receipts; mkdir "/backups/snapshots/$1"; mkdir "/backups/snapshots/$1/objects"; printf incomplete > "/backups/snapshots/$1/status"' "$id"
    ;;
  write)
    ruta="${4:-}"
    [[ "$ruta" =~ ^(postgres.dump|manifest.json|checksums.sha256|objects/[0-9]{8}.bin)$ ]] || fatal 'msg="archivo de snapshot inválido"'
    vol 'd="/backups/snapshots/$1"; test "$(cat "$d/status")" = incomplete; test ! -e "$d/$2"; umask 077; cat > "$d/$2"' "$id" "$ruta"
    ;;
  hash)
    ruta="${4:-}"
    [[ "$ruta" =~ ^(postgres.dump|objects/[0-9]{8}.bin)$ ]] || fatal 'msg="archivo inválido"'
    vol 'sha256sum "/backups/snapshots/$1/$2"' "$id" "$ruta" | cut -d' ' -f1
    ;;
  size)
    ruta="${4:-}"
    [[ "$ruta" =~ ^objects/[0-9]{8}.bin$ ]] || fatal 'msg="archivo inválido"'
    vol 'stat -c %s "/backups/snapshots/$1/$2"' "$id" "$ruta"
    ;;
  complete)
    vol 'd="/backups/snapshots/$1"; test "$(cat "$d/status")" = incomplete; cd "$d"; test -s postgres.dump; test -s manifest.json; sha256sum manifest.json postgres.dump > checksums.sha256; for f in objects/*.bin; do test ! -e "$f" || sha256sum "$f" >> checksums.sha256; done; sha256sum -c checksums.sha256 >&2; printf complete > status.tmp; mv status.tmp status; chmod -R a-w "$d"' "$id"
    ;;
  verify)
    vol 'cd "/backups/snapshots/$1"; test "$(cat status)" = complete; sha256sum -c checksums.sha256 >&2' "$id"
    ;;
  export)
    vol 'cd "/backups/snapshots/$1"; test "$(cat status)" = complete; sha256sum -c checksums.sha256 >&2; tar -cf - .' "$id"
    ;;
  read)
    ruta="${4:-}"
    [[ "$ruta" =~ ^(postgres.dump|manifest.json|checksums.sha256|status|objects/[0-9]{8}.bin)$ ]] || fatal 'msg="archivo inválido"'
    vol 'd="/backups/snapshots/$1"; test "$(cat "$d/status")" = complete; cat "$d/$2"' "$id" "$ruta"
    ;;
  success)
    vol 'test "$(cat "/backups/snapshots/$1/status")" = complete; umask 077; test ! -e "/backups/deploys/$1.success"; date +%s > "/backups/deploys/$1.success"' "$id"
    ;;
  receipt)
    vol 'umask 077; mkdir -p /backups/receipts; d="/backups/receipts/$(date -u +%Y%m%dT%H%M%S)-$$"; cat > "$d.tmp"; mv "$d.tmp" "$d.json"'
    ;;
  preview)
    vol 'umask 077; mkdir -p /backups/previews; test ! -e "/backups/previews/$1.tar"; cat > "/backups/previews/$1.tmp"; mv "/backups/previews/$1.tmp" "/backups/previews/$1.tar"' "$id"
    ;;
  retain)
    vol 'test ${#1} -le 10 || exit 0; now=$(date +%s); max=$(($1 * 86400)); for f in /backups/deploys/*.success; do test -f "$f" || continue; id=${f##*/}; id=${id%.success}; d="/backups/snapshots/$id"; test "$(cat "$d/status")" = complete || continue; stamp=$(cat "$f"); case "$stamp" in *[!0-9]*|"") exit 1;; esac; if test $((now - stamp)) -gt "$max"; then chmod -R u+w "$d"; rm -rf "$d"; rm "$f"; fi; done' "$BACKUP_RETENTION_DAYS"
    ;;
  list)
    vol 'for d in /backups/snapshots/*; do test -d "$d" || continue; id=${d##*/}; printf "%s %s " "$id" "$(cat "$d/status")"; if test -f "/backups/deploys/$id.success"; then echo deploy-success; else echo retained-manual; fi; done'
    ;;
  *) fatal 'msg="acción de backup-volume inválida"' ;;
esac
