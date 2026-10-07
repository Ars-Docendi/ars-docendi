#!/usr/bin/env bash
# Exclusivo desarrollo local. Nunca invocado por workflows; SGA no se inspecciona
# por el agente ni se publica. El usuario provee el archivo por canal privado.
set -euo pipefail
scripts_dir="$(cd "$(dirname "$0")" && pwd)"
local_psql() {
  docker compose exec -T postgres psql -X -qtA -v ON_ERROR_STOP=1 \
    -U "${POSTGRES_USER:-arsdocendi}" -d "${POSTGRES_DB:-arsdocendi}" --set=ambiente=local "$@"
}
if [[ "${1:-}" == --authorize-empty ]]; then
  # Registrar procedencia ANTES de migrar, sólo en una base realmente vacía.
  local_psql <<'SQL' >/dev/null
SELECT to_regclass('public.bootstrap_metadata') IS NOT NULL AS reconocida \gset
\if :reconocida
\else
DO $$ BEGIN
  IF EXISTS (SELECT 1 FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')) THEN
    RAISE EXCEPTION 'base local poblada sin autorización; no se sobrescribe';
  END IF;
END $$;
CREATE TABLE public.bootstrap_metadata (
  id boolean PRIMARY KEY DEFAULT true CHECK (id), ambiente text NOT NULL,
  origen text NOT NULL, estado text NOT NULL, huella_inicial text,
  creado_en timestamptz NOT NULL DEFAULT now()
);
INSERT INTO public.bootstrap_metadata (ambiente,origen,estado) VALUES ('local','provision-db/v1','autorizado');
\endif
SQL
  exit 0
fi
trabajo="$(mktemp -d)"
trap 'rm -rf "$trabajo"' EXIT
{ cat "$scripts_dir/seed-guard.sql"; printf '\nCOMMIT;\nSELECT estado FROM public.bootstrap_metadata WHERE id;\n'; } \
  | local_psql > "$trabajo/estado"
[[ "$(tail -n 1 "$trabajo/estado")" != completado ]] || { printf 'Seed local completo; se conservan ediciones.\n'; exit 0; }
sga="${SEED_SGA_FILE:-$scripts_dir/seed-data/sga.sql}"
[[ -f "$sga" ]] || { printf 'Falta dataset SGA privado: ambos datasets son necesarios; no se marca éxito.\n' >&2; exit 1; }
# La combinación se hace sólo al ejecutar setup local, sin logs de contenido.
SEED_LOCAL_COMBINADO=true python3 "$scripts_dir/seed-transaccion.py" \
  "$scripts_dir/seed-data/sintetico.sql" "$sga" > "$trabajo/seed.sql"
if ! local_psql < "$trabajo/seed.sql" > "$trabajo/salida" 2> "$trabajo/error"; then
  printf 'Seed local falló; transacción revertida. No se publican detalles con PII.\n' >&2
  exit 1
fi
printf 'Seed local sintético + SGA confirmado atómicamente.\n'
