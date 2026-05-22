# Ars Docendi — Gestión Docente

Sistema web para el Departamento de Ingeniería de la UNLaM. Para el contexto funcional (módulos, roles, reglas de negocio) ver [CLAUDE.md](CLAUDE.md).

## Arquitectura

Monolito modular en monorepo:

- **backend/**: ASP.NET Core 10 Web API. Un Host compone 4 módulos aislados (Designaciones, Aulas, Portal, Tareas). Cada módulo tiene su par `Modules.X` (interno) + `Modules.X.Contracts` (público). Comunicación entre módulos sólo vía contratos.
- **frontend/**: Vite + React 19 + TypeScript. Una app única con `src/features/` aislados por módulo.
- **Base de datos**: PostgreSQL único, un schema por módulo (`designaciones`, `aulas`, `portal`, `tareas`).

## Requisitos

- .NET 10 SDK
- Node 22 LTS o superior, pnpm 10+ (vía Corepack)
- Docker Desktop (para Postgres local)

## Levantar el entorno

### 1. Postgres

```pwsh
copy .env.example .env       # ajustar credenciales si hace falta
docker compose up -d
docker compose ps            # postgres debe quedar healthy
```

### 2. Backend

```pwsh
cd backend
dotnet restore
dotnet build
dotnet run --project src/ArsDocendi.Host
```

- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- Ping por módulo: `http://localhost:5000/api/{designaciones|aulas|portal|tareas}/ping`

### 3. Frontend

```pwsh
corepack enable              # solo la primera vez por máquina
cd frontend
copy .env.example .env       # opcional, default apunta a localhost:5000
pnpm install
pnpm dev
```

- App: `http://localhost:5173`

## Estructura

```
/
├── backend/                # .NET 10, monolito modular
│   ├── ArsDocendi.slnx
│   ├── global.json
│   └── src/
│       ├── ArsDocendi.Host/
│       ├── ArsDocendi.Shared/
│       ├── Modules.Designaciones/  + .Contracts/
│       ├── Modules.Aulas/          + .Contracts/
│       ├── Modules.Portal/         + .Contracts/
│       └── Modules.Tareas/         + .Contracts/
├── frontend/               # Vite + React 19 + TS
│   └── src/
│       ├── app/            # router
│       ├── shared/         # api client, auth, ui primitivos
│       └── features/       # uno por módulo del backend
├── docker-compose.yml
├── .env.example
└── .github/workflows/ci.yml
```

## Convenciones de módulos

- `Modules.X` solo referencia `Shared` y `Modules.*.Contracts`. **Prohibido** referenciar `Modules.X` internos entre sí.
- Cada módulo expone `GET /api/{x}/ping` como smoke test.
- En el frontend, las features no se importan entre sí; lo común sube a `src/shared/`.
