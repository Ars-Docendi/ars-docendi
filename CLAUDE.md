# Ars Docendi — Gestión Docente

Sistema web institucional del Departamento de Ingeniería e Investigaciones Tecnológicas de la **UNLaM**. Digitaliza el flujo de designaciones docentes, reserva de aulas, autogestión del docente y seguimiento de tareas internas.

Desarrollado como **Trabajo Final Integrador de Ingeniería Informática**. **No es un proyecto académico común**: queda en producción para uso real del cliente (la universidad) después de la defensa.

> Para setup y comandos de desarrollo ver [README.md](README.md). Para gitflow + flujo de PRs ver [CONTRIBUTING.md](CONTRIBUTING.md). Para **onboarding y uso de las skills** (developer nuevo, primer mantenedor de contexto, o cheat sheet recurrente) ver [ONBOARDING.md](ONBOARDING.md).

## Tabla de navegación

| Tipo                  | Path                                         | Cuándo usar                                                        |
| --------------------- | -------------------------------------------- | ------------------------------------------------------------------ |
| **Producto**          | [docs/product/](docs/product/)               | Brief, vision, design principles, specs de features                |
| **Arquitectura**      | [docs/architecture/](docs/architecture/)     | Stack, layers, dependencias, API, datos, infra                     |
| **Planes**            | [docs/plans/](docs/plans/)                   | Planes activos (`active/`), completados (`completed/`), backlog    |
| **Calidad**           | [docs/quality/](docs/quality/)               | Golden principles, grading criteria, scorecard, tech debt          |
| **Workflows**         | [docs/workflows/](docs/workflows/)           | Playbooks operacionales (init-project, add-feature, fix-bug, etc.) |
| **Reglas de negocio** | [docs/business-rules/](docs/business-rules/) | BR-\* con citas reglamentarias + mapping a tests                   |
| **Referencias**       | [docs/references/](docs/references/)         | Docs externas cacheadas (llms.txt)                                 |
| **Skills**            | [.claude/skills/](.claude/skills/)           | Skills Claude Code project-scoped                                  |

## Stack tecnológico

| Capa          | Tecnología                                                                        |
| ------------- | --------------------------------------------------------------------------------- |
| Backend       | C# — .NET 10 (ASP.NET Core Web API)                                               |
| Frontend      | React 19 + TypeScript + Vite 8                                                    |
| Base de datos | PostgreSQL 18 (un schema por módulo)                                              |
| Autenticación | SSO Microsoft Azure AD (credenciales institucionales UNLaM)                       |
| Pre-commit    | husky + lint-staged (dotnet format + eslint + prettier)                           |
| CI            | GitHub Actions (path filtering por backend/frontend)                              |
| Deploy        | VMs ofrecidas por UNLaM (detalles TBD, ver `docs/architecture/infrastructure.md`) |

Detalle completo en [docs/architecture/stack.md](docs/architecture/stack.md).

## Arquitectura

Monolito modular en monorepo. El `ArsDocendi.Host` compone 4 módulos backend aislados; cada módulo expone su API pública vía un proyecto `*.Contracts`. Comunicación entre módulos **solo** a través de Contracts — los proyectos `Modules.X` internos NO se referencian entre sí.

Ver [docs/architecture/module-anatomy.md](docs/architecture/module-anatomy.md) y [docs/architecture/dependency-graph.md](docs/architecture/dependency-graph.md).

## Módulos del sistema

1. **Designaciones** — workflow de designaciones docentes + visualización de asignaciones (API Guaraní). Ver [docs/architecture/domains/designaciones.md](docs/architecture/domains/designaciones.md).
2. **Aulas** — pedidos y asignación de aulas/laboratorios para exámenes. Ver [docs/architecture/domains/aulas.md](docs/architecture/domains/aulas.md).
3. **Portal** — datos personales del docente, horas, áreas de experticia. Ver [docs/architecture/domains/portal.md](docs/architecture/domains/portal.md).
4. **Tareas** — tablero tipo Trello con semáforo de vencimiento. Ver [docs/architecture/domains/tareas.md](docs/architecture/domains/tareas.md).

El chatbot se integrará dentro de alguno de los módulos anteriores (pendiente definir).

## Roles

| Rol                             | Alcance                                         |
| ------------------------------- | ----------------------------------------------- |
| Jefe de Cátedra                 | Genera el proyecto docente de su cátedra        |
| Coordinador de Carrera          | Aprueba/rechaza novedades de su carrera         |
| Secretaría Académica del Depto. | Ve todo el departamento, parametriza el sistema |
| Decanato                        | Aprobación final                                |
| Administrativos                 | Gestionan reservas de aulas y configurables     |
| Docente                         | Portal propio: datos, horas, áreas              |

## Estructura del repositorio

```
/
├── backend/                         # .NET 10, monolito modular
│   ├── ArsDocendi.slnx
│   ├── global.json
│   └── src/
│       ├── ArsDocendi.Host/         # Composition root
│       ├── ArsDocendi.Shared/       # Utilidades puras transversales
│       ├── Modules.Designaciones/   + .Contracts/
│       ├── Modules.Aulas/           + .Contracts/
│       ├── Modules.Portal/          + .Contracts/
│       └── Modules.Tareas/          + .Contracts/
├── frontend/                        # Vite + React 19 + TS
│   └── src/
│       ├── app/                     # router + composición
│       ├── shared/                  # api, auth, ui primitivos
│       └── features/                # uno por módulo backend
├── docs/                            # System of record (ver Tabla de navegación)
├── infra/                           # Skeleton para deploy (TBD)
├── scripts/                         # Bootstrap + automatización
│   ├── setup.sh
│   ├── generate-indexes.ts
│   └── close-plan-on-merge.ts
├── .claude/skills/                  # Skills Claude Code (project-scoped)
├── .github/workflows/
│   ├── ci.yml                       # Path-filtered build + test + format
│   └── close-plan-on-merge.yml      # Auto-cierre de planes post-merge
├── .husky/pre-commit                # Pre-commit unificado (dotnet + prettier + eslint)
├── lint-staged.config.mjs
├── package.json                     # Hospeda husky/lint-staged/prettier
├── pnpm-workspace.yaml              # Declara frontend como workspace
├── docker-compose.yml
└── .env.example
```

## Invariantes (no negociables)

Reglas que se cumplen en todo PR. El reviewer (humano o `/pr-review`) las verifica.

1. **Cross-module: solo via Contracts**. `Modules.X` importa solo desde `Modules.<Otro>.Contracts`. **PROHIBIDO** referenciar `Modules.<Otro>` (proyecto interno) directamente o importar desde `Internal/` ajeno.
2. **Grafo de dependencias DAG**. Sin ciclos. Chequear contra [docs/architecture/dependency-graph.md](docs/architecture/dependency-graph.md) antes de agregar edges nuevos.
3. **Ping endpoint obligatorio**. Cada módulo expone `GET /api/<modulo>/ping` con `[AllowAnonymous]` como smoke test.
4. **Contracts y Shared puros**. `Modules.<X>.Contracts`: solo DTOs/interfaces/tokens, sin lógica. `ArsDocendi.Shared`: solo utilidades puras, sin I/O ni estado mutable.
5. **Nueva feature → spec + plan**. SIEMPRE crear `docs/product/specs/<slug>.md` + `docs/plans/active/<slug>.md` antes de tocar código. El hard gate de `/add-feature` lo verifica.
6. **Cambios de schema/API → docs en el MISMO PR**. Actualizar `dependency-graph.md`, `api-contracts.md`, `data-model.md`, `domains/<x>.md` en el mismo PR que el código.
7. **No fake UI**. Sin botones/rutas/forms que aparenten estar hechos pero no funcionan. Sin lorem ipsum / TODO visible al usuario.
8. **Referencias cacheadas para libs externas**. Preferir `docs/references/<lib>-llms.txt` cuando exista; agregarlo si la lib se usa intensivamente.
9. **Bug fixes: red-green mandatorio**. Test que falla primero, después fix mínimo (ver [docs/workflows/fix-bug.md](docs/workflows/fix-bug.md)).
10. **Plan lifecycle**. Plans en `docs/plans/active/` hasta merge; después en `completed/` (automatizado por workflow o manual con `/complete-plan`).
11. **Compliance reglamentario**. Toda regla de negocio que provenga de normativa institucional debe registrarse como `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con **cita de la normativa** + mapping a test.
12. **Cambios de UX → design spec**. Cambios que afecten flujos del cliente requieren actualizar `docs/product/designs/<feature>-design-spec.md`. (Cuando se defina herramienta UX, también el artefacto en la herramienta.)
13. **Idioma del código: español**. Todo el código escrito por el equipo —identificadores (clases, métodos, variables, funciones, hooks, tipos), comentarios y docstrings— se escribe en español, para garantizar la mantenibilidad a futuro por un equipo hispanohablante. Única excepción: los símbolos provistos por el framework/lenguaje (que NO escribimos nosotros) se usan tal como los define la librería —keywords de C#/TS, APIs de React como `useEffect`/`useState`, atributos y tipos base de ASP.NET—. Detalle y gotchas en [Convenciones de código](#convenciones-de-código).

## Skills disponibles (project-scoped)

Las 20 skills viven en [`.claude/skills/`](.claude/skills/) y se versionan con el proyecto. Cualquiera que clone el repo las tiene disponibles sin frameworks globales.

### Bootstrap (corren una vez)

| Skill                    | Cuándo                                                    |
| ------------------------ | --------------------------------------------------------- |
| `/init-project`          | Llenar `docs/product/{brief,vision,design-principles}.md` |
| `/architecture-proposal` | Llenar `docs/architecture/*` post `/init-project`         |

### Planning

| Skill            | Cuándo                                                     |
| ---------------- | ---------------------------------------------------------- |
| `/plan-feature`  | Expandir brief → spec + plan (sin código)                  |
| `/add-feature`   | Implementar feature de punta a punta, gated en spec + plan |
| `/complete-plan` | Post-merge: mover plan a `completed/`                      |

### Implementación

| Skill            | Cuándo                                                     |
| ---------------- | ---------------------------------------------------------- |
| `/create-module` | Scaffold nuevo módulo .NET (Modules.X + Contracts)         |
| `/modify-module` | Cambiar módulo existente con análisis de impacto Contracts |
| `/fix-bug`       | Bug fix red-green con check de escalación                  |
| `/add-tests`     | Agregar tests (lane business BR-\* o technical smoke)      |
| `/ci-fix`        | Arreglar CI fallido en PR existente                        |

### Review / Quality

| Skill                       | Cuándo                                              |
| --------------------------- | --------------------------------------------------- |
| `/pr-review`                | Review estructurado con inline comments + summary   |
| `/evaluate`                 | Evaluación read-only contra spec + grading-criteria |
| `/security-audit`           | Read-only security pass del proyecto                |
| `/test-gap-monitor`         | Read-only: identifica tests faltantes               |
| `/architecture-drift-check` | Read-only: docs vs código real                      |

### Operaciones (cuando haya deploy)

| Skill                 | Cuándo                                        |
| --------------------- | --------------------------------------------- |
| `/check-deploy`       | Health check post-deploy                      |
| `/debug-production`   | Investigar issue reportado en producción      |
| `/infra-logs-monitor` | Read-only: logs reales vs `infrastructure.md` |

### Guides path-scoped (auto-activan)

| Skill                  | Trigger                                                                            |
| ---------------------- | ---------------------------------------------------------------------------------- |
| `dotnet-modules-guide` | Auto al tocar `backend/src/Modules.*/` o `ArsDocendi.Host/` o `ArsDocendi.Shared/` |
| `react-features-guide` | Auto al tocar `frontend/src/`                                                      |

## Workflows clave

Cada skill apunta a un playbook en [`docs/workflows/`](docs/workflows/) con el detalle paso a paso. Para nuevos developers, leer en este orden:

1. [docs/workflows/README.md](docs/workflows/README.md) — índice
2. [docs/workflows/add-feature.md](docs/workflows/add-feature.md) — flujo principal
3. [docs/workflows/fix-bug.md](docs/workflows/fix-bug.md) — red-green
4. [docs/workflows/pr-review.md](docs/workflows/pr-review.md) — review
5. [docs/workflows/open-pr.md](docs/workflows/open-pr.md) — referencia canónica

## Convenciones de código

Detalle completo en [docs/quality/golden-principles.md](docs/quality/golden-principles.md). Resumen:

- **Backend**: Controller → Service → Repository. Sin saltar capas. Sin imports cross-module excepto vía Contracts.
- **Frontend**: features aisladas, lo común a `shared/`. React Query para data. Un solo axios instance.
- **Naming**: PascalCase (componentes, clases .NET), camelCase (funciones, variables, hooks), kebab-case (filenames, branches).
- **Idioma del código: español** (invariante #13). Identificadores, comentarios y docstrings en español. Los strings de UI, labels y reglas de negocio (`BR-*`) ya van en español.
  - **Excepción — símbolos del framework/lenguaje**: lo que provee la librería se usa como ella lo define. Keywords (`public`, `async`, `await`), APIs de React (`useEffect`, `useState`, `useMemo`), atributos y tipos de ASP.NET (`[ApiController]`, `[AllowAnonymous]`, `ControllerBase`, `IActionResult`), helpers de librerías (`useQuery`, `useMutation` de React Query). Traducirlos rompe el build.
  - **Gotcha .NET — sufijo `Controller`**: ASP.NET descubre los controllers y resuelve el token `[controller]` de las rutas por el sufijo `Controller` del nombre de clase. No traducir el sufijo: `DesignacionesController` ✅ / `ControladorDesignaciones` ❌ (rompe el routing por convención). El resto del nombre va en español de dominio.
  - **Código existente en inglés**: el código ya mergeado (app-shell, auth) está en inglés. Migrar de forma gradual y registrar la deuda en [docs/quality/tech-debt.md](docs/quality/tech-debt.md).
- **Archivos chicos**: ~300 líneas como cap soft.
- **Structured logging** (Serilog) en backend. Nunca `Console.WriteLine` ni `console.log` en código productivo.

## Gitflow

- `develop` (default) — todos los PRs van acá.
- `main` — release stable; merges desde develop o hotfix/\*.
- `feature/<kebab>` — features nuevas → PR a develop.
- `hotfix/<kebab>` — parches urgentes → PRs a main Y develop.

Detalle completo en [CONTRIBUTING.md](CONTRIBUTING.md).

## Para nuevos developers

→ **Empezá por [ONBOARDING.md](ONBOARDING.md)**: explica cómo trabajar con las skills, el flujo de features, y la cheat sheet del día a día. Tiene tres secciones según tu perfil (developer nuevo, primer mantenedor de contexto, uso recurrente).

Resumen rápido para volver acá si te perdés:

1. Leer este archivo + [README.md](README.md).
2. Correr `./scripts/setup.sh` (levanta DB + deps + build).
3. Leer [docs/architecture/stack.md](docs/architecture/stack.md) + [module-anatomy.md](docs/architecture/module-anatomy.md).
4. Leer [docs/quality/golden-principles.md](docs/quality/golden-principles.md).
5. Mirar planes activos en [docs/plans/active/](docs/plans/active/) o regenerar índice: `pnpm generate-indexes`.

## Herramientas personales del developer (opcional)

Las siguientes herramientas son **opcionales** y NO requeridas para trabajar en el proyecto. Cualquier developer puede usarlas para SU propio flujo sin afectar a otros:

- **Engram** (plugin Claude Code) para memoria persistente entre sesiones.
- **SDD orchestrator** (skills globales) si se prefiere ese flujo de planning.

El proyecto **no depende** de ninguna de ellas. Los artefactos canónicos viven en archivos versionados (`docs/`, `.claude/skills/`).
