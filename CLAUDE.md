# Ars Docendi — Gestión Docente

Sistema web institucional del Departamento de Ingeniería e Investigaciones Tecnológicas de la **UNLaM**. Digitaliza el flujo de designaciones docentes, reserva de aulas, autogestión del docente y seguimiento de tareas internas.

Desarrollado como **Trabajo Final Integrador de Ingeniería Informática**. **No es un proyecto académico común**: queda en producción para uso real del cliente (la universidad) después de la defensa.

> Para setup y comandos de desarrollo ver [README.md](README.md). Para gitflow + flujo de PRs ver [CONTRIBUTING.md](CONTRIBUTING.md). Para **onboarding y uso de las skills** (developer nuevo, primer mantenedor de contexto, o cheat sheet recurrente) ver [ONBOARDING.md](ONBOARDING.md).

## Tabla de navegación

| Tipo                  | Path                                         | Cuándo usar                                                                          |
| --------------------- | -------------------------------------------- | ------------------------------------------------------------------------------------ |
| **Producto**          | [docs/product/](docs/product/)               | Brief, vision, design principles, designs UX                                         |
| **Arquitectura**      | [docs/architecture/](docs/architecture/)     | Stack, layers, dependencias, API, datos, infra                                       |
| **Planning**          | [openspec/](openspec/)                       | Specs vigentes (`openspec/specs/`), changes activos/archivados (`openspec/changes/`) |
| **Calidad**           | [docs/quality/](docs/quality/)               | Golden principles, grading criteria, scorecard, tech debt                            |
| **Workflows**         | [docs/workflows/](docs/workflows/)           | Playbooks operacionales (init-project, add-feature, fix-bug, etc.)                   |
| **Reglas de negocio** | [docs/business-rules/](docs/business-rules/) | BR-\* con citas reglamentarias + mapping a tests                                     |
| **Referencias**       | [docs/references/](docs/references/)         | Docs externas cacheadas (llms.txt)                                                   |
| **Skills**            | [.claude/skills/](.claude/skills/)           | Skills Claude Code project-scoped                                                    |

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
├── openspec/                        # Planning: specs vigentes + changes activos/archivados
│   ├── config.yaml                  # Contexto del proyecto + invariantes inyectadas
│   ├── specs/                       # Specs consolidadas y vigentes
│   └── changes/                     # Changes activos (<id>/) y archivados (archive/)
├── scripts/                         # Bootstrap + automatización
│   ├── setup.sh
│   └── generate-indexes.ts          # Regenera _index.md solo de docs/business-rules/
├── .claude/skills/                  # Skills Claude Code (project-scoped)
├── .github/workflows/
│   └── ci.yml                       # Path-filtered build + test + format + openspec validate
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
5. **Nueva feature → change OpenSpec aprobado**. SIEMPRE crear el change con `/opsx:propose` (genera proposal + design + specs + tasks) y dejarlo apply-ready ANTES de tocar código. El hard gate de `/add-feature` lo verifica con `openspec status` (`applyRequires` en `done`).
6. **Cambios de schema/API → docs en el MISMO PR**. Actualizar `dependency-graph.md`, `api-contracts.md`, `data-model.md`, `domains/<x>.md` en el mismo PR que el código.
7. **No fake UI**. Sin botones/rutas/forms que aparenten estar hechos pero no funcionan. Sin lorem ipsum / TODO visible al usuario.
8. **Referencias cacheadas para libs externas**. Preferir `docs/references/<lib>-llms.txt` cuando exista; agregarlo si la lib se usa intensivamente.
9. **Bug fixes: red-green mandatorio**. Test que falla primero, después fix mínimo (ver la skill `/fix-bug`).
10. **Change lifecycle**. Los changes viven en `openspec/changes/<id>/` mientras están activos; post-merge se archivan con `/opsx:archive` → `openspec/changes/archive/` (mergea las delta specs a `openspec/specs/`).
11. **Compliance reglamentario**. Toda regla de negocio que provenga de normativa institucional debe registrarse como `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con **cita de la normativa** + mapping a test.
12. **Cambios de UX → design spec**. Cambios que afecten flujos del cliente requieren actualizar `docs/product/designs/<feature>-design-spec.md`. (Cuando se defina herramienta UX, también el artefacto en la herramienta.)
13. **Idioma del código: español**. Todo el código escrito por el equipo —identificadores (clases, métodos, variables, funciones, hooks, tipos), comentarios y docstrings— se escribe en español, para garantizar la mantenibilidad a futuro por un equipo hispanohablante. Única excepción: los símbolos provistos por el framework/lenguaje (que NO escribimos nosotros) se usan tal como los define la librería —keywords de C#/TS, APIs de React como `useEffect`/`useState`, atributos y tipos base de ASP.NET—. Detalle y gotchas en [Convenciones de código](#convenciones-de-código).

## Skills disponibles (project-scoped)

Las skills del proyecto viven en [`.claude/skills/`](.claude/skills/) y se versionan con el repo. Cualquiera que clone el repo las tiene disponibles sin frameworks globales. El planning usa los comandos `/opsx:*` (glue OpenSpec, también versionado en `.claude/commands/opsx/`).

### Bootstrap (corren una vez)

| Skill                    | Cuándo                                                    |
| ------------------------ | --------------------------------------------------------- |
| `/init-project`          | Llenar `docs/product/{brief,vision,design-principles}.md` |
| `/architecture-proposal` | Llenar `docs/architecture/*` post `/init-project`         |

### Planning (OpenSpec)

Los changes se gestionan con los comandos `/opsx:*` (glue versionado en `.claude/commands/opsx/`):

| Comando         | Cuándo                                                                   |
| --------------- | ------------------------------------------------------------------------ |
| `/opsx:explore` | Explorar una idea antes de proponerla formalmente                        |
| `/opsx:propose` | Crear change: genera proposal + design + specs + tasks                   |
| `/opsx:apply`   | Implementar las tasks de un change apply-ready                           |
| `/opsx:archive` | Post-merge: archivar change y mergear delta specs a `openspec/specs/`    |
| `/opsx:sync`    | Sincronizar las delta specs de un change a las main specs (sin archivar) |

La skill `/add-feature` se conserva como orquestador principal de features: aplica los gates del proyecto (architecture check, security pass, `/evaluate`, apertura de PR) y delega la ejecución de tasks a `/opsx:apply`. Su precondición es un change OpenSpec apply-ready.

| Skill          | Cuándo                                                                  |
| -------------- | ----------------------------------------------------------------------- |
| `/add-feature` | Implementar feature de punta a punta, gated en change OpenSpec aprobado |

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
| `/architecture-drift-check` | Read-only: docs vs código real                      |

### Guides path-scoped (auto-activan)

| Skill                  | Trigger                                                                            |
| ---------------------- | ---------------------------------------------------------------------------------- |
| `dotnet-modules-guide` | Auto al tocar `backend/src/Modules.*/` o `ArsDocendi.Host/` o `ArsDocendi.Shared/` |
| `react-features-guide` | Auto al tocar `frontend/src/`                                                      |

### Política del glue OpenSpec (D7)

El glue generado por OpenSpec (`.claude/commands/opsx/` + `.claude/skills/openspec-*`) **se versiona en el repo** (clone-and-go). Disciplina obligatoria:

- Al bumpear el devDep `@fission-ai/openspec`, correr `openspec update` y commitear el glue regenerado en el **mismo PR**.
- **Nadie corre `openspec init` global** — el init ya está hecho y el glue vive en el repo.
- El CI corre `openspec validate --strict` sobre specs y changes. (Opcional pendiente: un guard que falle si `openspec update` produce diff, para detectar glue desincronizado de la CLI.)

## Workflows clave

El detalle paso a paso de cada workflow vive en su skill (`.claude/skills/<x>/SKILL.md`) — **una sola fuente**. En `docs/workflows/` solo queda [open-pr.md](docs/workflows/open-pr.md) (referencia canónica de `gh pr create`, linkeada desde varias skills) + un [índice](docs/workflows/README.md). Para nuevos developers, leer en este orden:

1. [docs/workflows/README.md](docs/workflows/README.md) — índice "¿qué skill uso?"
2. Skill `/add-feature` — flujo principal de features
3. Skill `/fix-bug` — red-green
4. Skill `/pr-review` — review
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
5. Mirar changes activos: `openspec list`. Para business-rules, regenerar índice: `pnpm generate-indexes`.

## Herramientas personales del developer (opcional)

Las siguientes herramientas son **opcionales** y NO requeridas para trabajar en el proyecto. Cualquier developer puede usarlas para SU propio flujo sin afectar a otros:

- **Engram** (plugin Claude Code) para memoria persistente entre sesiones.
- **SDD orchestrator** (skills globales) si se prefiere ese flujo de planning.

El proyecto **no depende** de ninguna de ellas. Los artefactos canónicos viven en archivos versionados (`docs/`, `.claude/skills/`).
