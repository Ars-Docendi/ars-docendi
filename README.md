# Ars Docendi — Gestión Docente

Sistema web para el Departamento de Ingeniería de la UNLaM. Para el contexto funcional (módulos, roles, reglas de negocio) ver [CLAUDE.md](CLAUDE.md).

> **¿Primera vez en el proyecto?** Leé [ONBOARDING.md](ONBOARDING.md) — explica cómo trabajar con las skills, el flujo de features, y la cheat sheet del día a día.

## Arquitectura

Monolito modular en monorepo:

- **backend/**: ASP.NET Core 10 Web API. Un Host compone 4 módulos aislados (Designaciones, Aulas, Portal, Tareas). Cada módulo tiene su par `Modules.X` (interno) + `Modules.X.Contracts` (público). Comunicación entre módulos sólo vía contratos.
- **frontend/**: Vite + React 19 + TypeScript. Una app única con `src/features/` aislados por módulo.
- **Base de datos**: PostgreSQL único, un schema por módulo (`designaciones`, `aulas`, `portal`, `tareas`).

## Requisitos

- .NET 10 SDK
- Node 20.19+ (LTS)
- pnpm 9+ (o `corepack enable` para usar la versión declarada en `package.json`)
- Docker + Docker Compose (para Postgres local)
- `gh` CLI (para flujos de PRs)

## Levantar el entorno

### Opción rápida (recomendada)

```bash
./scripts/setup.sh
```

Este script: crea `.env` desde `.env.example`, levanta Postgres en docker, instala deps Node (raíz + frontend), y restaura/buildea backend. Al terminar te lista las URLs.

### Manual (paso a paso)

#### 1. Postgres

```bash
cp .env.example .env         # ajustar credenciales si hace falta
docker compose up -d
docker compose ps            # postgres debe quedar healthy
```

#### 2. Node deps (raíz + frontend workspace)

```bash
pnpm install                 # instala husky/lint-staged/prettier + deps del frontend
```

Husky se activa automáticamente con `pnpm install` y configura el pre-commit (dotnet format + eslint + prettier).

#### 3. Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/ArsDocendi.Host
```

- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- Ping por módulo: `http://localhost:5000/api/{designaciones|aulas|portal|tareas}/ping`

#### 4. Frontend

```bash
pnpm --filter frontend dev
```

- App: `http://localhost:5173`

## Comandos útiles

```bash
pnpm format               # formatea todo el repo (prettier)
pnpm format:check         # verifica formato sin modificar
pnpm generate-indexes     # regenera _index.md de docs/business-rules/
dotnet test backend/ArsDocendi.slnx
pnpm --filter frontend lint
pnpm --filter frontend build
```

Más comandos en [ONBOARDING.md → Cheat sheet](ONBOARDING.md#3-uso-recurrente--cheat-sheet).

## Estructura

```
/
├── backend/                  # .NET 10, monolito modular
│   ├── ArsDocendi.slnx
│   ├── global.json
│   └── src/
│       ├── ArsDocendi.Host/
│       ├── ArsDocendi.Shared/
│       ├── Modules.Designaciones/  + .Contracts/
│       ├── Modules.Aulas/          + .Contracts/
│       ├── Modules.Portal/         + .Contracts/
│       └── Modules.Tareas/         + .Contracts/
├── frontend/                 # Vite + React 19 + TS
│   └── src/{app,shared,features}/
├── openspec/                 # Planning: specs vigentes + changes activos/archivados (fuente de verdad)
├── docs/                     # System of record (product, architecture, quality, workflows, business-rules, references)
├── infra/                    # Skeleton para deploy (nginx + systemd samples)
├── scripts/                  # setup.sh + generate-indexes.ts (solo business-rules)
├── .claude/skills/           # Skills Claude Code project-scoped
├── .github/workflows/        # ci.yml (path filtering + openspec validate)
├── .husky/pre-commit
├── lint-staged.config.mjs
├── package.json              # hospeda husky/lint-staged/prettier
├── pnpm-workspace.yaml       # declara frontend como workspace
├── docker-compose.yml
└── .env.example
```

## Convenciones de módulos

- `Modules.X` solo referencia `Shared` y `Modules.*.Contracts`. **Prohibido** referenciar `Modules.X` internos entre sí.
- Cada módulo expone `GET /api/{x}/ping` como smoke test.
- En el frontend, las features no se importan entre sí; lo común sube a `src/shared/`.

Detalle completo en [CLAUDE.md → Invariantes](CLAUDE.md#invariantes-no-negociables) y [docs/quality/golden-principles.md](docs/quality/golden-principles.md).

## Próximos pasos

- ¿Empezás a trabajar? → [ONBOARDING.md](ONBOARDING.md)
- ¿Vas a abrir un PR? → [CONTRIBUTING.md](CONTRIBUTING.md)
- ¿Contexto del proyecto y reglas no negociables? → [CLAUDE.md](CLAUDE.md)
