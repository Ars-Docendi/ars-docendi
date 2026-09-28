#!/usr/bin/env bash
# scripts/setup.sh — Bootstrap one-command para Ars Docendi
#
# Levanta TODO lo necesario para empezar a desarrollar:
#   - Postgres via docker-compose
#   - .NET deps + build
#   - Frontend deps
#   - .env si no existe
#
# Uso:
#   ./scripts/setup.sh             # modo auto (default)
#   ./scripts/setup.sh --no-build  # skip dotnet build (más rápido)
#
# Requisitos:
#   - .NET 10 SDK
#   - Node 24.15+
#   - pnpm 12.3.4 (o corepack habilitado)
#   - Docker + docker compose

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

# Colors (sólo si stdout es TTY)
if [ -t 1 ]; then
    BLUE='\033[0;34m'
    GREEN='\033[0;32m'
    YELLOW='\033[1;33m'
    RED='\033[0;31m'
    NC='\033[0m'
else
    BLUE='' GREEN='' YELLOW='' RED='' NC=''
fi

log() { echo -e "${BLUE}[setup]${NC} $1"; }
ok() { echo -e "${GREEN}[ok]${NC} $1"; }
warn() { echo -e "${YELLOW}[warn]${NC} $1"; }
err() { echo -e "${RED}[error]${NC} $1" >&2; }

SKIP_BUILD=false
for arg in "$@"; do
    case "$arg" in
        --no-build) SKIP_BUILD=true ;;
        --help|-h)
            sed -n '2,20p' "$0"
            exit 0
            ;;
    esac
done

# === Pre-requisitos ===
log "Validando pre-requisitos..."

command -v dotnet >/dev/null 2>&1 || { err "dotnet no instalado"; exit 1; }
command -v node >/dev/null 2>&1 || { err "node no instalado"; exit 1; }
command -v pnpm >/dev/null 2>&1 || { err "pnpm no instalado"; exit 1; }
command -v docker >/dev/null 2>&1 || { err "docker no instalado"; exit 1; }

DOTNET_VERSION=$(dotnet --version)
NODE_VERSION=$(node --version)
PNPM_VERSION=$(pnpm --version)

ok ".NET $DOTNET_VERSION | Node $NODE_VERSION | pnpm $PNPM_VERSION"

# === .env ===
if [ ! -f .env ]; then
    log "Creando .env desde .env.example..."
    if [ ! -f .env.example ]; then
        warn ".env.example no encontrado — saltando creación de .env"
    else
        cp .env.example .env
        ok ".env creado — REVISALO y ajustá los valores"
    fi
fi

# === Docker compose: Postgres ===
log "Levantando PostgreSQL (docker compose)..."
docker compose up -d postgres
sleep 2  # darle un momento al servicio
ok "PostgreSQL levantado"

# === Node deps (raíz + workspaces) ===
log "Instalando dependencias Node (husky/lint-staged/prettier + frontend)..."
pnpm install --no-frozen-lockfile
ok "Dependencias Node instaladas"

# === .NET deps ===
log "Restaurando dependencias .NET..."
dotnet restore backend/ArsDocendi.slnx
ok "Dependencias .NET restauradas"

if [ "$SKIP_BUILD" = false ]; then
    log "Building backend..."
    dotnet build backend/ArsDocendi.slnx --no-restore
    ok "Backend buildeado"
fi

# === Migrations ===
log "Aplicando migraciones..."
dotnet run --project backend/src/ArsDocendi.Host -- --migrate
ok "Migraciones aplicadas"

# === Seed de desarrollo local ===
# Dos datasets, siempre los dos: sintetico.sql (fixtures de prueba, sin datos reales,
# cubre los flujos de Designaciones/Aulas/Tareas) y sga.sql (carreras, materias y
# docentes reales del SGA, para poder probar con el volumen y los casos reales de la
# UNLaM). Ninguno de los dos corre nunca fuera de dev local — no hay equivalente de
# este paso para staging/prod: esos ambientes usan infra/scripts/seed.sh, que sólo
# acepta sintetico.sql (o el que se le indique por SEED_SQL) y nunca datos con PII real.
# Se ejecuta dentro del contenedor de Postgres (docker compose exec) para no depender
# de tener el cliente psql instalado en el host.
#
# Solo se siembra la PRIMERA vez (base vacía). Ambos archivos hacen upsert por id
# (ON CONFLICT ... DO UPDATE), así que re-sembrar sobre una base ya en uso le pisaría a
# cualquier edición hecha desde la app (por ejemplo, un docente editado a mano desde la
# pantalla de administración). Si un desarrollador quiere volver al dataset original,
# es su decisión explícita: `docker compose down -v && ./scripts/setup.sh`.
YA_SEMBRADO=$(docker compose exec -T postgres psql \
    -U "${POSTGRES_USER:-arsdocendi}" -d "${POSTGRES_DB:-arsdocendi}" -tAc \
    "SELECT to_regclass('public.seed_metadata') IS NOT NULL;" 2>/dev/null | tr -d '[:space:]')

if [ "$YA_SEMBRADO" = "t" ]; then
    warn "La base ya tiene datos de seed — no se vuelve a sembrar (evita pisar ediciones manuales)."
    warn "Para resetear al dataset original: docker compose down -v && ./scripts/setup.sh"
else
    log "Sembrando datos de desarrollo (sintético + SGA)..."
    for seed in infra/scripts/seed-data/sintetico.sql infra/scripts/seed-data/sga.sql; do
        if [ -f "$seed" ]; then
            docker compose exec -T postgres psql \
                -U "${POSTGRES_USER:-arsdocendi}" -d "${POSTGRES_DB:-arsdocendi}" \
                -v ON_ERROR_STOP=1 < "$seed" >/dev/null
            ok "Sembrado: $seed"
        else
            warn "No encontrado, se salteó: $seed"
        fi
    done
fi

echo
ok "Setup completo."
echo
echo "Próximos pasos:"
echo "  Backend:  dotnet run --project backend/src/ArsDocendi.Host"
echo "  Frontend: pnpm --filter frontend dev"
echo "  DB:       docker compose ps"
echo "  Format:   pnpm format"
echo "  Tests:    dotnet test backend/ArsDocendi.slnx"
echo
echo "URLs por defecto:"
echo "  Backend API: http://localhost:5000"
echo "  Swagger:     http://localhost:5000/swagger"
echo "  Frontend:    http://localhost:5173"
echo "  Postgres:    localhost:5432 (db: arsdocendi)"
