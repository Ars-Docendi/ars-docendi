# Onboarding y uso del harness

Este documento explica cómo usar el workflow y set de skills del proyecto. **Tres audiencias** con necesidades distintas — leé la sección que te corresponda.

| Si vos sos...                                                                                              | Saltá a                                                                                       |
| ---------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Alguien nuevo en el equipo que acaba de clonar el repo y nunca lo levantó                                  | [Sección 1 — Developer nuevo](#1-developer-nuevo-en-el-proyecto)                              |
| El primer encargado de llenar / completar el contenido inicial de `docs/` (brief, arquitectura, BRs, etc.) | [Sección 2 — Primer mantenedor de contexto inicial](#2-primer-mantenedor-de-contexto-inicial) |
| Alguien que ya conoce el sistema y solo necesita refrescar comandos del día a día                          | [Sección 3 — Cheat sheet de uso recurrente](#3-uso-recurrente--cheat-sheet)                   |

> **Importante**: este harness es **project-scoped**. Todas las skills viven en `.claude/skills/` del repo y se versionan. No requieren ningún framework global (engram, SDD, etc.). Si vos personalmente usás herramientas globales adicionales, son opcionales — el proyecto no depende de ellas.

---

## 1. Developer nuevo en el proyecto

Bienvenido. Esta sección te lleva de "acabo de clonar" a "puedo abrir mi primer PR" en orden.

### 1.1 Pre-requisitos en tu máquina

| Herramienta      | Versión mínima        | Cómo verificar           |
| ---------------- | --------------------- | ------------------------ |
| .NET SDK         | 10.0.x                | `dotnet --version`       |
| Node             | 20.19+                | `node --version`         |
| pnpm             | 9.x (o usar corepack) | `pnpm --version`         |
| Docker + Compose | Reciente              | `docker compose version` |
| `gh` CLI         | Reciente              | `gh --version`           |

Si te falta algo, instalalo antes de seguir. En Linux/macOS, [Mise](https://mise.jdx.dev/) o `asdf` simplifican el manejo de versiones múltiples.

### 1.2 Levantar el proyecto la primera vez

```bash
git clone <repo-url>
cd ars-docendi
./scripts/setup.sh
```

Esto:

1. Crea `.env` desde `.env.example` (revisalo y ajustá lo que aplique).
2. Levanta PostgreSQL en docker compose.
3. Corre `pnpm install` en la raíz (instala husky/lint-staged/prettier + deps del workspace `frontend`).
4. Corre `dotnet restore` y `dotnet build` del backend.
5. (Cuando existan migrations) las aplica.

Al terminar te lista las URLs de los servicios. Esperalas:

| URL                             | Para qué                       |
| ------------------------------- | ------------------------------ |
| `http://localhost:5000`         | Backend API                    |
| `http://localhost:5000/swagger` | Swagger UI                     |
| `http://localhost:5173`         | Frontend (cuando lo arranques) |
| `localhost:5432`                | Postgres                       |

### 1.3 Arrancar dev servers

En dos terminales separadas:

```bash
# Terminal 1: backend
dotnet run --project backend/src/ArsDocendi.Host

# Terminal 2: frontend
pnpm --filter frontend dev
```

### 1.4 Qué leer (en este orden)

1. **[CLAUDE.md](CLAUDE.md)** — contexto del proyecto, módulos, roles, invariantes (12 reglas no negociables), skills disponibles. **Es la única lectura realmente obligatoria.**
2. **[CONTRIBUTING.md](CONTRIBUTING.md)** — gitflow (develop/main), pre-commit, code review, convención de commits.
3. **[docs/architecture/stack.md](docs/architecture/stack.md)** — qué tecnologías y por qué.
4. **[docs/architecture/module-anatomy.md](docs/architecture/module-anatomy.md)** — layout de un módulo .NET, layer rules, cross-module via Contracts.
5. **[docs/architecture/dependency-graph.md](docs/architecture/dependency-graph.md)** — diagrama Mermaid + edge registry actual.
6. **[docs/quality/golden-principles.md](docs/quality/golden-principles.md)** — anti-patterns + reglas de calidad. Lo flagga `/pr-review` y `/evaluate`.
7. **[docs/workflows/README.md](docs/workflows/README.md)** — índice de playbooks. Cada skill referencia uno.

Después podés mirar [`docs/architecture/domains/`](docs/architecture/domains/) para entender cada módulo en profundidad.

### 1.5 Cómo trabajan las skills

Las skills viven en `.claude/skills/<nombre>/SKILL.md` y se invocan en Claude Code con `/<nombre> [args]`. Hay tres tipos:

- **Interactivas** (las invocás vos): `/opsx:propose`, `/add-feature`, `/fix-bug`, `/pr-review`, etc.
- **Path-scoped** (auto-activan según el archivo que toques): `dotnet-modules-guide` se activa al editar `backend/src/Modules.*`, `react-features-guide` al editar `frontend/src/`.
- **Ops** (las corre alguien con permisos de prod): `/check-deploy`, `/debug-production`, `/infra-logs-monitor`.

**No necesitás memorizar todas**. Empezás con dos: `/opsx:propose` para algo nuevo, `/fix-bug` para algo roto. El resto se va aprendiendo.

### 1.6 Tu primer cambio: bug fix simple (caminata guiada)

Supongamos que encontraste un bug: el endpoint `/api/portal/docentes` devuelve 500 cuando no hay docentes. Debería devolver 200 con array vacío.

```bash
# 1. Sincronizar y crear branch
git checkout develop
git pull
git checkout -b feature/portal-empty-docentes
```

En Claude Code:

```
/fix-bug endpoint /api/portal/docentes devuelve 500 con DB vacía, debería ser 200 con []
```

La skill va a:

1. Localizar el módulo (Portal).
2. Hacer escalation check (es un solo módulo, sin cambio de Contracts → continúa).
3. Escribir un test que falla (`PortalController_GetDocentes_RetornaListaVaciaCuandoNoHayDocentes`).
4. Aplicar el fix mínimo en el service.
5. Confirmar que todos los tests pasan.
6. Si el bug podría recurrir, sugerir agregar regla a `golden-principles.md`.
7. Abrir el PR a `develop`.

Vos revisás cada paso y commiteás cuando estés conforme. **No es automático ciego — vos manejás**.

### 1.7 Tu primera feature: ejemplo end-to-end

Para una feature nueva el flujo tiene **dos fases** con aprobación humana en el medio: primero se planifica el change en OpenSpec, después se implementa.

```
/opsx:explore (opcional) → /opsx:propose → [equipo aprueba] → /add-feature → PR + CI → merge → /opsx:archive
        idea                   el plan        gate humano       implementación                     cierre
```

Caminata concreta. Secretaría Académica pide: **exportar el listado de designaciones a Excel**.

**1. (Opcional) Pensar el problema — `/opsx:explore`**

```
/opsx:explore exportar listado de designaciones a Excel para Secretaría
```

Modo read-only para investigar antes de comprometerte. No escribe código.

**2. Crear el plan — `/opsx:propose`**

```
/opsx:propose exportar listado de designaciones a Excel para Secretaría Académica
```

Genera `openspec/changes/exportar-designaciones-excel/` con:

- `proposal.md` — qué + por qué + criterios de aceptación
- `design.md` — decisiones de diseño y arquitectura
- `specs/` — requisitos SHALL/MUST + scenarios
- `tasks.md` — tareas de implementación ordenadas

Gracias a `openspec/config.yaml`, los artefactos ya respetan tus invariantes (módulos afectados, Contracts cross-module, BR-\* si hay normativa).

**3. Aprobación del equipo**

```
openspec view exportar-designaciones-excel
```

El equipo revisa y ajusta. Los cambios acordados se encodean en los artefactos, no en "LGTM por chat". Acá el change queda apply-ready.

**4. Implementar — `/add-feature`**

```
/add-feature exportar-designaciones-excel
```

Acá viven los gates del proyecto, en orden:

- **Architecture check** — dependencias permitidas en `dependency-graph.md`; si hace falta un módulo nuevo → `/create-module` primero.
- **Hard gate** — confirma `openspec status` con `applyRequires` (las tasks) en `done`. Si no está listo, frena.
- **Execute** — delega en `/opsx:apply`: implementa contract-first (`Modules.X.Contracts` antes que internals) y tilda `- [ ]` → `- [x]` en `tasks.md`.
- **Security pass** — auth por rol, sin leak de `Internal/`, DAG respetado.
- **QA + docs** — `dotnet test`, build/lint frontend, `openspec validate --strict`, `/evaluate`; actualizar `api-contracts.md` / `data-model.md` / `domains/*` si cambiaron boundaries.

**5. PR + CI**

El PR se abre (ver [open-pr.md](docs/workflows/open-pr.md)). CI corre build/test/format **+ `openspec validate --all --strict`**. Opcional: `/pr-review <PR>`.

**6. Merge → archivar — `/opsx:archive`**

```
/opsx:archive exportar-designaciones-excel
```

Mueve el change a `openspec/changes/archive/` y mergea sus delta specs a `openspec/specs/` (que ahora refleja el comportamiento vigente).

**Quién es dueño de qué**

| Responsabilidad                                      | Dueño                              |
| ---------------------------------------------------- | ---------------------------------- |
| Artefactos de planning (proposal/design/specs/tasks) | OpenSpec (`/opsx:*`)               |
| Loop de ejecución de tasks + checkboxes              | OpenSpec (`/opsx:apply`)           |
| Gates de arquitectura, security, `/evaluate`, PR, BR | Proyecto (`/add-feature` + skills) |
| Fuente de verdad de specs                            | `openspec/specs/`                  |

Vos revisás cada paso y commiteás cuando estés conforme. **No es automático ciego — vos manejás.**

### 1.8 Pre-commit: qué hace y qué hacer si falla

Al `git commit`, husky dispara `lint-staged` que corre:

- `dotnet format` sobre archivos `.cs` modificados
- `eslint --fix` + `prettier --write` sobre `.ts/.tsx` del frontend
- `prettier --write` sobre `.md/.json/.yml`

**Si falla**: el commit se aborta. Mirá el error, arreglá lo que indica (o re-stageá los archivos que prettier modificó), y volvé a commitear.

```bash
# Si prettier modificó archivos durante el pre-commit:
git add <archivos>
git commit -m "<mensaje>"
```

**Nunca** usar `--no-verify` salvo acuerdo explícito del equipo. Si el pre-commit está mal configurado, arreglar la config, no bypassearla.

### 1.9 Reglas críticas para no romper nada

Las **12 invariantes** del [CLAUDE.md](CLAUDE.md#invariantes-no-negociables) son no negociables. Las más fáciles de romper sin darse cuenta:

- **Cross-module: solo vía Contracts**. Si tu código en `Modules.Designaciones` necesita algo de `Modules.Portal`, importás desde `Modules.Portal.Contracts`, NO desde `Modules.Portal` directamente. Nunca `Internal/` ajeno.
- **Nueva feature ⇒ change OpenSpec aprobado y apply-ready ANTES de código**. El `/add-feature` lo verifica con un hard gate (`openspec status` — `applyRequires` en `done`); si el change no está listo, no implementa.
- **Cambios en API/schema/dependencias ⇒ actualizar docs en el MISMO PR**. `api-contracts.md`, `data-model.md`, `dependency-graph.md`, `domains/<x>.md` según corresponda.
- **Bug fixes ⇒ red-green obligatorio**. Test que falla primero, después fix mínimo.
- **Compliance reglamentario**: si tu cambio implementa una regla que viene de normativa institucional (estatuto, régimen, disposición departamental), registrala como `BR-<modulo>-NNN` en `docs/business-rules/<modulo>.md` con la cita de la fuente.

---

## 2. Primer mantenedor de contexto inicial

Esta sección es para vos si te toca **completar el contenido inicial** de `docs/` (la primera carga real de info del proyecto, después del bootstrap mecánico del harness).

### 2.1 Estado actual de `docs/`

| Carpeta                            | Estado                                                                    | Acción                                                                                                 |
| ---------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `product/brief.md`                 | ✅ Completo (TFI UNLaM, módulos, scope)                                   | Revisar y ajustar si querés precisarlo                                                                 |
| `product/vision.md`                | ✅ Completo (goals, non-goals, métricas)                                  | Revisar; las métricas pueden necesitar baseline real                                                   |
| `product/design-principles.md`     | ✅ Completo (principios, anti-patterns)                                   | Ajustar cuando se decida herramienta UX                                                                |
| `product/designs/`                 | 📝 Placeholder (herramienta TBD)                                          | Completar cuando el equipo decida (Figma / Pencil / otra)                                              |
| `architecture/stack.md`            | ✅ Completo (.NET 10 + React 19 + Postgres)                               | Mantener al día si cambian versiones o se agrega tooling                                               |
| `architecture/module-anatomy.md`   | ✅ Completo (.NET module layout)                                          | Estable, ajustar si evoluciona la convención                                                           |
| `architecture/dependency-graph.md` | ✅ Completo (Mermaid + edge registry)                                     | Actualizar SIEMPRE que se agregue/quite un edge cross-module                                           |
| `architecture/api-contracts.md`    | ⚠️ Parcial (estructura + ping endpoints, sin endpoints reales)            | Completar a medida que se definan endpoints                                                            |
| `architecture/data-model.md`       | ⚠️ Parcial (estructura + Portal/Docentes, resto vacío)                    | Completar a medida que se definan entidades                                                            |
| `architecture/infrastructure.md`   | ⚠️ Skeleton (TBD en VMs/deploy/secrets/backup)                            | Completar cuando UNLaM provisione VMs                                                                  |
| `architecture/domains/<modulo>.md` | ⚠️ Esqueleto (proposito + roles + dependencias; entidades y BR-\* vacías) | Llenar a medida que cada módulo evolucione                                                             |
| `plans/backlog.md`                 | 📝 Vacío                                                                  | Ideas y features pendientes de proponer; las features activas van en `openspec/changes/`               |
| `quality/golden-principles.md`     | ✅ Completo                                                               | Sumar reglas a medida que aparezcan anti-patterns nuevos                                               |
| `quality/grading-criteria.md`      | ✅ Completo (rúbrica para `/evaluate`)                                    | Estable                                                                                                |
| `quality/scorecard.md`             | 📝 Vacío                                                                  | Se llena con `/evaluate`                                                                               |
| `quality/tech-debt.md`             | 📝 Vacío                                                                  | Se llena cuando se detecte deuda                                                                       |
| `workflows/`                       | ✅ 13 playbooks completos                                                 | Estable; ajustar si cambia un proceso                                                                  |
| `business-rules/`                  | 📝 Solo `_template.md`                                                    | **Crear un .md por módulo a medida que se identifiquen BR-\***. Crítico para compliance reglamentario. |
| `references/`                      | 📝 Solo README                                                            | Agregar `<lib>-llms.txt` cuando una lib se use intensivamente                                          |

### 2.2 Qué completar y cómo

#### 2.2.1 Changes de feature (OpenSpec)

**No los llenes a priori**. Se crean **uno por feature** cuando arranca esa feature, usando `/opsx:propose <descripción>`. El comando genera `openspec/changes/<id>/` con proposal, design, specs y tasks. Una vez aprobado por el equipo, `/add-feature <id>` lo implementa.

#### 2.2.2 Business rules — TAREA CRÍTICA INICIAL

Esta es la **tarea más importante** del primer mantenedor: identificar las **reglas reglamentarias** del Departamento de Ingeniería UNLaM y registrarlas en `docs/business-rules/<modulo>.md`.

**Proceso**:

1. Pedir al cliente (Secretaría Académica / Departamento) los documentos normativos relevantes:
   - Estatuto universitario UNLaM
   - Régimen de docencia
   - Normativas departamentales sobre designaciones, mesas de examen, reservas
   - Cualquier disposición que afecte el flujo del sistema
2. Por cada módulo, crear `docs/business-rules/<modulo>.md` desde `_template.md`.
3. Por cada regla que el sistema deba implementar:
   - Asignar ID `BR-<modulo>-NNN` (numeración local, empieza en 001).
   - Llenar `Statement`, `Rationale`, `Provenance: from_regulation`, `Fuente normativa` (cita exacta), `Ejemplos` (positivos y negativos), `Roles afectados`.
4. **No inventar reglas**. Si una regla no está en la normativa pero parece obvia, marcarla como `Provenance: inferred_from_code` o `confirmed_user` y validarla con el cliente.

Ejemplo:

```markdown
### BR-designaciones-001 Solo el Coordinador de la carrera puede aprobar designaciones de esa carrera

- **Statement:** El Coordinador de Carrera solo puede aprobar/rechazar designaciones de docentes asignados a carreras bajo su coordinación. Designaciones de otras carreras quedan fuera de su scope.
- **Rationale:** Cumple separación de responsabilidades entre coordinadores de distintas carreras del Departamento.
- **Provenance:** `from_regulation`
- **Fuente normativa:** Régimen de Designaciones Docentes UNLaM, Art. 14
- **Ejemplos:**
  - Positivo: Coordinador de Ingeniería Informática aprueba designación de docente de Programación I.
  - Negativo: Coordinador de Ingeniería Informática NO puede aprobar designación de docente de Cálculo (otra carrera).
- **Roles afectados:** Coordinador de Carrera, Jefe de Cátedra (solicitante)
```

**Cada BR debe tener un test** que la verifique cuando la regla se implemente. El mapping va en la sección "Test mapping" del mismo archivo.

#### 2.2.3 API contracts

`docs/architecture/api-contracts.md` tiene la estructura armada con headers, autenticación, error shape, formato de tablas por módulo. **Faltan las filas de endpoints reales** (solo está el `/ping` de cada módulo como ejemplo).

Cada vez que se agregue un endpoint, completar la fila correspondiente. Lo ideal es que ocurra en el MISMO PR que introduce el endpoint (es invariante #6 del CLAUDE.md).

#### 2.2.4 Data model

`docs/architecture/data-model.md` tiene la estructura por módulo + ya nombró `Portal.Docentes` con la nota de PII. **Faltan las entidades reales de cada módulo** a medida que se diseñen.

Cuando se crea una migration EF Core, agregar la fila correspondiente. Marcar siempre la columna **PII** explícitamente (Sí/No + qué campos).

#### 2.2.5 Infrastructure

`docs/architecture/infrastructure.md` está como **skeleton con secciones TBD**. Completar a medida que UNLaM entregue las VMs:

- **Topología VMs**: tipo, OS, recursos, dominios.
- **Deployment process**: el proceso decidido (GitHub Actions → SSH, manual, Ansible).
- **Secrets management**: en qué env vars + dónde se guardan + quién tiene acceso.
- **Backup strategy**: frecuencia, destino, retención, encriptación.
- **Reverse proxy**: ajustar `infra/nginx/ars-docendi.conf` con dominio real.
- **Hardening checklist**: ir tildando a medida que se aplica.
- **Monitoring**: si se decide montar Prometheus/Grafana o algo más simple.

Cuando se concrete el deploy, crear los runbooks operacionales:

- `docs/workflows/deploy.md`
- `docs/workflows/troubleshooting.md`
- `docs/workflows/restore-from-backup.md`

#### 2.2.6 Domain docs

`docs/architecture/domains/<modulo>.md` están armados con propósito, roles, bounded context y dependencias proyectadas. **Las entidades, API pública concreta y BR-\* siguen vacías** porque se materializan a medida que se diseña cada módulo.

A medida que `/create-module` o `/modify-module` se invoquen, actualizá los domain docs en el MISMO PR.

#### 2.2.7 Design specs

Hasta que el equipo elija herramienta UX, los design specs son archivos `.md` en `docs/product/designs/` siguiendo `_design-spec-template.md`. Describen layout, estados, decisiones. Cuando se elija herramienta, agregar el MCP correspondiente a `.mcp.json` y portar la skill `visual-test` del template original.

### 2.3 Si querés re-bootstrap (rehacer el contenido inicial)

El harness ya hizo el bootstrap mecánico con info derivada del CLAUDE.md original. Pero si querés **rehacer desde cero** con sesiones interactivas de aclaración con el equipo, podés correr:

```
/init-project
```

Esto pregunta sobre producto, problema, plataformas, scope, métricas y dirección de diseño, y reescribe los 3 archivos de `docs/product/`.

```
/architecture-proposal
```

Esto pregunta sobre runtime topology, stack, dominios, API pública, persistencia, infrastructure, y reescribe los 7 archivos de `docs/architecture/` + un domain doc por bounded context.

**Cuidado**: ambas skills sobreescriben los archivos. Hacelo en una branch separada (`chore/re-init-project`) y diffeá contra `develop` antes de mergear, no sea que se pierdan precisiones que ya tenías.

### 2.4 Quality docs (golden-principles, tech-debt)

A medida que el proyecto avance:

- **`golden-principles.md`**: cada vez que una clase de bug se podría haber prevenido con una regla, agregala. La skill `/fix-bug` step 8 lo recuerda.
- **`tech-debt.md`**: cuando dejás deuda intencional (fix parcial, atajo justificado, follow-up), agregala con severidad + owner. La skill `/evaluate` la lee para detectar deudas no documentadas.

---

## 3. Uso recurrente — cheat sheet

Para el day-to-day, una vez que ya conocés el sistema.

### 3.1 Comandos shell más usados

```bash
# Setup (solo primera vez o reset)
./scripts/setup.sh

# Dev backend
dotnet run --project backend/src/ArsDocendi.Host

# Dev frontend
pnpm --filter frontend dev

# Tests backend
dotnet test backend/ArsDocendi.slnx
dotnet test --filter "FullyQualifiedName~NombreDelTest"   # uno solo

# Build backend
dotnet build backend/ArsDocendi.slnx

# Build / lint frontend
pnpm --filter frontend build
pnpm --filter frontend lint

# Format
pnpm format          # arregla todo
pnpm format:check    # solo verifica

# Regenerar índice de business-rules
pnpm generate-indexes

# Ver changes activos de OpenSpec
openspec list

# Ver detalle de un change
openspec view <id>

# Docker compose
docker compose up -d         # levanta servicios en background
docker compose ps            # estado
docker compose logs -f api   # tail logs
docker compose down          # baja todo
```

### 3.2 Skills y comandos más usados (por frecuencia)

| Skill / Comando  | Cuándo                                | Ejemplo                                                                       |
| ---------------- | ------------------------------------- | ----------------------------------------------------------------------------- |
| `/opsx:propose`  | Empezar una feature nueva             | `/opsx:propose listado de aulas disponibles por fecha`                        |
| `/add-feature`   | Implementar change aprobado           | `/add-feature listado-aulas-disponibles`                                      |
| `/opsx:apply`    | Ejecutar tasks de un change (directo) | `/opsx:apply listado-aulas-disponibles`                                       |
| `/opsx:archive`  | Post-merge: archivar change           | `/opsx:archive listado-aulas-disponibles`                                     |
| `/fix-bug`       | Algo roto / regresión                 | `/fix-bug formato fecha en designación aparece DD/MM en API pero MM/DD en UI` |
| `/pr-review`     | Review estructurado de PR             | `/pr-review 42`                                                               |
| `/ci-fix`        | CI fallido en tu PR                   | `/ci-fix 42`                                                                  |
| `/add-tests`     | Cubrir gaps de tests                  | `/add-tests designaciones --lane business`                                    |
| `/create-module` | Módulo .NET nuevo                     | `/create-module Examenes`                                                     |
| `/modify-module` | Cambio en módulo existente            | `/modify-module Designaciones`                                                |
| `/evaluate`      | Autocrítica de feature completada     | `/evaluate exportar-designaciones-excel`                                      |

### 3.3 Flujos comunes paso a paso

#### Nueva feature

```
1. git checkout develop && git pull
2. git checkout -b feature/<kebab-slug>
3. /opsx:propose <descripción>     → genera openspec/changes/<id>/ (proposal+design+specs+tasks)
4. [equipo aprueba change]         → ajustes si hace falta; change queda apply-ready
5. /add-feature <id>               → gates del proyecto + ejecución vía /opsx:apply + abre PR
6. /pr-review <PR-number>          → opcional: review estructurado
7. [merge a develop]               → /opsx:archive <id> para archivar el change
```

#### Bug fix simple

```
1. git checkout develop && git pull
2. git checkout -b feature/fix-<descripcion-corta>
3. /fix-bug <descripción del bug>  → red-green-refactor
4. [revisar diff]                  → confirmar que no hay drive-by changes
5. git push + abrir PR (la skill suele hacerlo)
6. [merge a develop]
```

#### Bug que toca múltiples módulos / Contracts

```
1. /fix-bug <descripción>          → en step 2 detecta escalación, te avisa
2. La skill indica crear un change con /opsx:propose (spec de bug escalado)
3. Continuás: red-green sobre las Contracts impactadas
4. Documentar consumidores afectados en el PR body
```

#### Cambio cross-module (sin bug)

```
1. /modify-module <NombreModulo>   → analiza impacto Contracts
2. Si breaking → te indica escalar a /opsx:propose
3. Si aditivo → implementás, actualizás dependency-graph.md
```

#### Antes de release / mensual

```
/evaluate <feature-reciente>       → score + scorecard
/security-audit                    → deps vulnerables, secrets, auth
/architecture-drift-check          → docs vs código real
/test-gap-monitor                  → tests faltantes (BR-*, endpoints)
```

#### Post-deploy (cuando haya prod)

```
/check-deploy prod                 → health endpoints + latencia
/infra-logs-monitor prod           → drift logs vs infrastructure.md
```

#### Algo se rompe en prod

```
/debug-production <síntoma>        → correlación logs + recent changes
                                   → si causa clara: /fix-bug
                                   → si no: documentar y pedir más data
```

### 3.4 Path-scoped guides (auto-inyectan, no las invocás)

Cuando Claude Code toca archivos en estos paths, las guides correspondientes se activan automáticamente y le recuerdan las convenciones:

| Path                             | Guide                                                                     |
| -------------------------------- | ------------------------------------------------------------------------- |
| `backend/src/Modules.*/`         | `dotnet-modules-guide` (layers, Contracts cross-module, ping endpoint)    |
| `backend/src/ArsDocendi.Host/`   | `dotnet-modules-guide`                                                    |
| `backend/src/ArsDocendi.Shared/` | `dotnet-modules-guide`                                                    |
| `frontend/src/`                  | `react-features-guide` (features aisladas, React Query, axios compartido) |

No tenés que hacer nada — solo confirma que las convenciones se aplican automáticamente.

### 3.5 Atajos útiles

```bash
# Ver solo el diff staged
git diff --staged

# Listar changes activos de OpenSpec
openspec list

# Listar BR-* de un módulo
grep -n "^### BR-" docs/business-rules/designaciones.md

# Mirar qué hace el pre-commit sin commitear
pnpm exec lint-staged --debug

# Forzar regenerar el lock
rm pnpm-lock.yaml && pnpm install

# Backend rebuild rápido (sin restore)
dotnet build backend/ArsDocendi.slnx --no-restore

# Frontend dev sin Vite cache
rm -rf frontend/node_modules/.vite && pnpm --filter frontend dev
```

### 3.6 Convenciones a tener siempre presentes

- **Pre-commit falla → arreglar, NO bypass con `--no-verify`**.
- **Change OpenSpec aprobado y apply-ready ANTES de código** para features (hard gate en `/add-feature`).
- **Cross-module solo via Contracts** (invariante #1).
- **Cambios de schema/API → actualizar docs en el MISMO PR** (invariante #6).
- **Red-green obligatorio en bug fixes** (invariante #9).
- **BR-\* obligatorio para reglas reglamentarias** (invariante #11).
- **Sin AI attribution en commits**: el proyecto explícitamente NO usa `Co-Authored-By: Claude` ni similares.

---

## Cuando algo se rompe

| Problema                                   | Acción                                                                                                                                      |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Pre-commit aborta el commit                | Leer el error, arreglar formato/lint, `git add` archivos modificados, re-commit                                                             |
| `pnpm install` falla con peer dep warnings | Verificar Node ≥ 20.19 y pnpm ≥ 9. Si persiste, `rm -rf node_modules pnpm-lock.yaml && pnpm install`                                        |
| `dotnet restore` falla                     | Verificar `dotnet --version` ≥ 10.0.x. Borrar caches: `dotnet nuget locals all --clear`                                                     |
| Docker compose: Postgres no levanta        | `docker compose down -v` (cuidado: borra datos) y reintentar                                                                                |
| Tests fallan en CI pero no localmente      | Probable env de CI distinto. Revisar `.github/workflows/ci.yml` y reproducir las versiones exactas                                          |
| CI rojo en tu PR                           | `/ci-fix <PR-number>`                                                                                                                       |
| Olvidaste archivar el change post-merge    | Archivar manualmente: `/opsx:archive <id>` — mergea las delta specs a `openspec/specs/`                                                     |
| Skill `/<nombre>` no aparece en la lista   | Confirmá que existe `.claude/skills/<nombre>/SKILL.md` con frontmatter `name:` correcto. Reiniciar Claude Code si fue creada en esta sesión |

---

## Si te perdés

- **Tabla de navegación general**: [CLAUDE.md](CLAUDE.md#tabla-de-navegación)
- **Workflows detallados** (qué hace cada skill): [docs/workflows/README.md](docs/workflows/README.md)
- **Reglas no negociables**: [CLAUDE.md → Invariantes](CLAUDE.md#invariantes-no-negociables)
- **Calidad y anti-patterns**: [docs/quality/golden-principles.md](docs/quality/golden-principles.md)
- **Cómo abrir un PR canónico**: [docs/workflows/open-pr.md](docs/workflows/open-pr.md)

Si después de todo esto sigue sin estar claro, **preguntale al equipo**. Si la confusión se repite, probablemente falta documentación — agregala en el PR donde la viviste.
