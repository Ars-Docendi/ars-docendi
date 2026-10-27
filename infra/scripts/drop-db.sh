#!/usr/bin/env bash
# Elimina la base y los roles de un ambiente DESCARTABLE (staging / pr-N). Idempotente.
#
# Guardas de seguridad:
#   - Valida que el ambiente sea staging o pr-N (NUNCA prod).
#   - Si el nombre de base no empieza por el prefijo esperado, aborta.
#   - Si un nombre de rol coincide con el de prod, aborta.
#
# Uso:
#   drop-db.sh <ambiente>
#
# Variables:
#   PGHOST PGPORT PGUSER PGPASSWORD   credenciales admin de Postgres (libpq)

source "$(dirname "$0")/_comun.sh"

ambiente="${1:-}"
exigir_ambiente_destruible "$ambiente"   # aborta si es prod o inválido

base="$(nombre_base "$ambiente")"
rol_ro="$(rol_asistente "$ambiente" basico)"
rol_ro_pii="$(rol_asistente "$ambiente" pii)"

# Cinturón y tiradores: el nombre de base debe ser de un ambiente descartable.
base_prod="$(nombre_base prod)"
if [[ "$base" == "$base_prod" ]]; then
  fatal "msg=\"se intentó dropear la base de prod\" base=\"${base}\""
fi
if [[ ! "$base" =~ ^arsdocendi_(staging|pr_[0-9]+)$ ]]; then
  fatal "msg=\"nombre de base no descartable\" base=\"${base}\""
fi

# Misma guarda para los roles: son objetos de CLUSTER, así que un error de
# nombre acá no borra "la base equivocada" sino el rol de OTRO ambiente.
for rol in "$rol_ro" "$rol_ro_pii"; do
  if [[ "$rol" == "$(rol_asistente prod basico)" || "$rol" == "$(rol_asistente prod pii)" ]]; then
    fatal "msg=\"se intentó dropear un rol de prod\" rol=\"${rol}\""
  fi
  if [[ ! "$rol" =~ ^asistente_ro(_pii)?_(staging|pr_[0-9]+)$ ]]; then
    fatal "msg=\"nombre de rol no descartable\" rol=\"${rol}\""
  fi
done

if existe_base "$base"; then
  log_warn msg="dropeando base" ambiente="$ambiente" base="$base"

  # DROP OWNED BY dentro de la base, ANTES del DROP DATABASE: limpia objetos y
  # privilegios del rol en este catálogo. Después el DROP ROLE no encuentra
  # dependencias y no falla.
  for rol in "$rol_ro" "$rol_ro_pii"; do
    if existe_rol "$rol"; then
      psql_base "$base" -c "DROP OWNED BY \"${rol}\";" >/dev/null
    fi
  done

  # Cortar conexiones activas antes del DROP para que no falle por 'in use'.
  psql_admin -c "SELECT pg_terminate_backend(pid)
                 FROM pg_stat_activity
                 WHERE datname = '${base}' AND pid <> pg_backend_pid();" >/dev/null

  psql_admin -c "DROP DATABASE IF EXISTS \"${base}\";"
  log_info msg="base eliminada" ambiente="$ambiente" base="$base"
else
  log_info msg="base ya no existe, nada que dropear (idempotente)" base="$base"
fi

# Los roles del asistente sobreviven al DROP DATABASE (son de cluster): hay que
# darlos de baja explícitamente. Se hace SIEMPRE, incluso si la base ya no
# estaba, para que una baja a medias converja al reintentar.
for rol in "$rol_ro" "$rol_ro_pii"; do
  if existe_rol "$rol"; then
    psql_admin -c "DROP OWNED BY \"${rol}\";" >/dev/null
    psql_admin -c "DROP ROLE IF EXISTS \"${rol}\";"
    log_info msg="rol del asistente eliminado" rol="$rol"
  else
    log_info msg="rol del asistente ya no existe (idempotente)" rol="$rol"
  fi
done

log_info msg="baja OK" ambiente="$ambiente" base="$base"
