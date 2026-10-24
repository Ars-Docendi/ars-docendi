# Ars Docendi — Gestión Docente

Sistema web para el Departamento de Ingeniería de la UNLaM. Para las reglas de trabajo y el contexto funcional ver [AGENTS.md](AGENTS.md).

> **¿Primera vez en el proyecto?** Leé [ONBOARDING.md](ONBOARDING.md) — explica cómo trabajar con las skills, el flujo de features, y la cheat sheet del día a día.

## Arquitectura

Monolito modular en monorepo:

- **backend/**: ASP.NET Core 10 Web API. Un Host compone 4 módulos de negocio (Designaciones, Aulas, Portal, Tareas) y la infraestructura transversal (`Shared`, `Migraciones`, `Storage`, `Inferencia`). Comunicación entre módulos sólo vía proyectos `*.Contracts`.
- **frontend/**: Vite + React 19 + TypeScript. Una app única con `src/features/` aisladas entre sí.
- **Base de datos**: PostgreSQL único, un schema por contexto (`identity`, `audit`, `storage`, `designaciones`, `aulas`, `portal`, `tareas`).

Detalle en [docs/architecture/](docs/architecture/).

## Requisitos

- .NET 10 SDK
- Node 24.15+ (LTS)
- pnpm 12.3.4+ (o `corepack enable` para usar la versión declarada en `package.json`)
- Docker + Docker Compose (para Postgres local)
- `gh` CLI (para flujos de PRs)

## Levantar el entorno

### Opción rápida (recomendada)

```bash
./scripts/setup.sh
```

Este script: crea `.env` desde `.env.example`, levanta Postgres en docker, instala deps Node (raíz + frontend), restaura/buildea backend, aplica migraciones y siembra datos de desarrollo. Al terminar te lista las URLs.

El seed solo corre la primera vez (base vacía) — si ya sembraste, correrlo de nuevo no pisa lo que edites desde la app. Para volver al dataset original: `docker compose down -v && ./scripts/setup.sh`.

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

#### 3. Migraciones y seed

```bash
cd backend
dotnet restore
dotnet build
dotnet run --project src/ArsDocendi.Host -- --estado-migraciones # consulta segura
dotnet run --project src/ArsDocendi.Host -- --migrate   # aplica migraciones y termina
cd ..
# Primera inicialización local: ejecuta ambos datasets en una transacción,
# registra la marca completa y no vuelve a sembrar sobre ediciones existentes.
bash infra/scripts/seed-local.sh
```

Los dos datasets van siempre juntos: `sintetico.sql` (fixtures de prueba) y `sga.sql`
(carreras, materias y docentes reales del SGA — tiene PII real, por eso **sólo corre en
dev local**; nunca en staging/prod). Staging/PR usan `infra/scripts/seed.sh` sólo al
inicializar y preservan datos/adjuntos entre despliegues. El reset es una operación
explícita distinta del deploy; producción nunca recibe fixtures.

El baseline consolidado requiere bases nuevas. Si existe historial alpha anterior,
el runner aborta sin modificarlo: no existe conversión ni reset automático. Ver
`database/README.md` y `docs/operations/migrations-persistence.md` para scaffolding,
preview SQL, integridad, backups y recuperación.

#### 4. Backend

```bash
dotnet run --project backend/src/ArsDocendi.Host
```

- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- Ping por módulo: `http://localhost:5000/api/{designaciones|aulas|portal|tareas}/ping`

#### 5. Frontend

```bash
pnpm --filter frontend dev
```

- App: `http://localhost:5173`
- Vite proxea `/api` a `http://localhost:5000`: el frontend y la API quedan en el mismo origen, como en los despliegues. No hace falta `VITE_API_URL`.

#### 6. Ingreso con Microsoft (opcional)

En local se entra con el selector de identidades sembradas, que hay que habilitar en el backend una sola vez:

```bash
dotnet user-secrets set "DevelopmentAuthentication:Enabled" "true" --project backend/src/ArsDocendi.Host
```

Para probar el ingreso real con la registración de desarrollo ("Ars Docendi (desarrollo)", redirect `http://localhost/api/auth/signin-oidc`), cargá la configuración en tus user-secrets; el secret nunca va al repo:

```bash
dotnet user-secrets set "AutenticacionMicrosoft:ClientId" "32d10bbf-3c8a-4526-ba10-3d7a0ceee9d1" --project backend/src/ArsDocendi.Host
```

```bash
dotnet user-secrets set "AutenticacionMicrosoft:ClientSecret" "<secret>" --project backend/src/ArsDocendi.Host
```

```bash
dotnet user-secrets set "AutenticacionMicrosoft:Habilitada" "true" --project backend/src/ArsDocendi.Host
```

Y en `frontend/.env.local` (no se versiona) agregá `VITE_MICROSOFT_LOGIN_ENABLED=true`. El botón principal pasa a ser el ingreso con Microsoft y el selector queda como acceso secundario. Sólo entran cuentas cuyo mail coincida con el UPN de un usuario activo con rol: dalo de alta antes desde la pantalla de usuarios. Probalo en Chrome, Edge o Firefox; algunos Safari no envían las cookies `Secure` del retorno sobre `http://localhost`.

## Comandos útiles

```bash
pnpm format               # formatea todo el repo (prettier)
pnpm format:check         # verifica formato sin modificar
dotnet test backend/ArsDocendi.slnx
pnpm --filter frontend lint
pnpm --filter frontend build
```

Más comandos en [ONBOARDING.md → Cheat sheet](ONBOARDING.md#3-cheat-sheet).

`frontend/src/test/contratos/` guarda respuestas reales de la API que el frontend usa en sus tests. Si un cambio intencional altera la forma de una respuesta, el test de backend falla; regeneralo con `ACTUALIZAR_CONTRATOS=1 dotnet test backend/ArsDocendi.slnx` y adaptá el mapper del frontend.

## Estructura

```
/
├── backend/                  # .NET 10, monolito modular
│   ├── ArsDocendi.slnx
│   └── src/
│       ├── ArsDocendi.Host/
│       ├── ArsDocendi.Shared/        # identity, audit y utilidades
│       ├── ArsDocendi.Migraciones/   # runner de SQL versionado
│       ├── ArsDocendi.Storage/       + .Contracts/ (adjuntos S3)
│       ├── ArsDocendi.Inferencia/    # cliente Ollama
│       ├── Modules.Designaciones/  + .Contracts/
│       ├── Modules.Aulas/          + .Contracts/
│       ├── Modules.Portal/         + .Contracts/
│       └── Modules.Tareas/         + .Contracts/
│   └── tests/ArsDocendi.IntegrationTests/
├── database/                 # SQL versionado por contexto
├── frontend/                 # Vite + React 19 + TS
│   └── src/{app,shared,features}/
├── openspec/                 # Planning: specs vigentes + changes activos (fuente de verdad)
├── docs/                     # System of record (product, architecture, quality, workflows, business-rules, references)
├── infra/                    # Compose + Traefik + Cloudflare Tunnel (producción en Debian; pruebas en Proxmox)
├── scripts/                  # setup local
├── .claude/, .agents/        # skills de Claude Code y Codex
├── .github/workflows/        # CI, CodeQL y deploys (staging, prod, pr-N)
├── .husky/pre-commit
├── lint-staged.config.mjs
├── global.json               # fija el SDK y el runner de tests .NET
├── package.json              # hospeda husky/lint-staged/prettier
├── pnpm-workspace.yaml       # declara frontend como workspace
├── docker-compose.yml
└── .env.example
```

## Convenciones

Las reglas de módulos, frontend y tests están en [AGENTS.md](AGENTS.md#reglas-de-implementación) y [docs/quality/golden-principles.md](docs/quality/golden-principles.md).

## Próximos pasos

- ¿Empezás a trabajar? → [ONBOARDING.md](ONBOARDING.md)
- ¿Vas a abrir un PR? → [CONTRIBUTING.md](CONTRIBUTING.md)
- ¿Contexto del proyecto y reglas no negociables? → [AGENTS.md](AGENTS.md)
- ¿Vas a preparar los dos hosts? → [Provisioning](infra/README.md),
  [runners y secretos en GitHub](docs/operations/github-pr-deploy.md) y
  [túneles y DNS Cloudflare](infra/cloudflared/README.md). La
  [topología objetivo](docs/architecture/infrastructure.md) separa producción
  exclusivamente en Debian (`https://<DOMINIO>`) de staging y `pr-N` en
  Proxmox. No incluye LB, deploy dual ni failover; el operador configura
  Cloudflare/runners y acepta el corte con decisión explícita sobre los datos
  productivos anteriores.
