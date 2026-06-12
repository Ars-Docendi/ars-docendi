---
name: add-feature
description: Implementar una feature de punta a punta sobre un change OpenSpec aprobado. Usar cuando el usuario pide una nueva capability, una mejora, o un comportamiento visible para el usuario, o cuando se reanuda implementación. Aplica los gates de arquitectura/calidad del proyecto y delega la ejecución de tasks a /opsx:apply.
argument-hint: [<change-id>]
---

# Add feature

Flujo completo de implementación de una nueva capability: planning (OpenSpec) → gate → execute → security → QA → close.

## Cuándo usar

Estás implementando una **nueva capability** (UI o API) que debe trackearse como trabajo de producto + ingeniería. Para bugs usar `/fix-bug`. Para nuevo módulo, primero `/create-module`.

## Modelo: OpenSpec planifica, `/add-feature` aplica la disciplina del proyecto

El planning vive en OpenSpec (`openspec/changes/<id>/`). `/add-feature` **no planifica**: orquesta la disciplina de implementación del proyecto (architecture check, security pass, `/evaluate`, PR) sobre un change OpenSpec ya aprobado, y **delega la ejecución de tasks a `/opsx:apply`**.

| Fase               | Comando         | Escribe                                                      | Termina con         |
| ------------------ | --------------- | ------------------------------------------------------------ | ------------------- |
| **Planning**       | `/opsx:propose` | `openspec/changes/<id>/` (proposal + design + specs + tasks) | Pidiendo aprobación |
| **Implementación** | `/add-feature`  | Código + PR (ejecución vía `/opsx:apply`)                    | PR abierto          |

`$ARGUMENTS` es el id (kebab-case) del change en `openspec/changes/$ARGUMENTS/`. Si no se provee, derivar del request o listar con `openspec list`.

## Pre-requisitos

- Leer `docs/product/vision.md`.
- Revisar los changes existentes: `openspec list`.
- Leer `docs/architecture/dependency-graph.md`.

## Steps (sin reordenar)

### 1. Contexto

- Identificar surfaces afectadas: `backend/src/Modules.<X>/`, `backend/src/Modules.<X>.Contracts/`, `frontend/src/features/<x>/`.
- Identificar roles afectados y BR-\* aplicables.

### 2. Planning con OpenSpec (si todavía no existe el change)

- Si no hay un change para esta feature, correr `/opsx:propose "<descripción>"` → genera `openspec/changes/<id>/` (proposal, design, specs, tasks).
- `openspec/config.yaml` inyecta el contexto e invariantes del proyecto en cada artefacto generado.
- Para pensar antes de proponer: `/opsx:explore`.

### 3. Architecture check

- Confirmar dependencias permitidas en `docs/architecture/dependency-graph.md`.
- Si requiere un **nuevo módulo .NET**, correr `/create-module` primero.
- Si extiende API pública de un módulo existente, seguir `/modify-module` para análisis de impacto.

### 4. Human review gate (aprobación del plan)

- **Parar** y presentar el change (`openspec show <id>` / `openspec view`) al equipo.
- No tratar "LGTM en chat" como durable; los cambios acordados se encodean en los artefactos del change.
- **Fin de fase planning.**

### 5. Implementation hard gate (OBLIGATORIO antes de tocar código)

1. Confirmar que el change está apply-ready: `openspec status --change "<id>"` con todos los artefactos de `applyRequires` (típicamente `tasks`) en estado `done`.

Si no está listo: **parar inmediatamente**. Volver al step 2 o completar el change con `/opsx:propose`.

### 6. Execute (vía `/opsx:apply`)

1. Delegar la ejecución de las tasks a **`/opsx:apply <id>`** (OpenSpec es dueño del loop de tasks + checkboxes en `tasks.md`). No reimplementar el loop a mano.
2. Durante la implementación, respetar:
   - **Contract-first**: definir/extender `Modules.<X>.Contracts/` antes que internals.
   - Guides path-scoped: `dotnet-modules-guide`, `react-features-guide`.
   - `docs/quality/golden-principles.md` siempre.

### 7. Security pass

Read-only / minimal change antes de merge:

- Autorización por rol matchea la spec; sin endpoints públicos nuevos sin Contracts documentados.
- Sin secrets en código; ver `docs/quality/golden-principles.md`.
- Deps y cross-module imports respetan `docs/architecture/dependency-graph.md` (DAG, sin leak de Internal).
- Si toca deploy o env vars, cross-check `docs/architecture/infrastructure.md`.

Si hay issues, arreglarlas antes de step 9.

### 8. QA, documentación, close out

**Verificación**:

1. Backend: `dotnet test backend/ArsDocendi.slnx` — todos verdes.
2. Frontend: `pnpm --filter frontend lint` + `pnpm --filter frontend build` — todos verdes.
3. Manual spot-check de los flujos por rol afectado.
4. `openspec validate --strict <id>` — verde.
5. Correr `/evaluate` para validar contra spec + grading-criteria.

**Documentación** (si cambiaste boundaries, APIs o persistencia): `docs/architecture/dependency-graph.md`, `api-contracts.md`, `data-model.md`, `domains/<dominio>.md`, y `docs/business-rules/<dominio>.md` (si introduce/modifica BR-\*).

**Close out**:

- Las tasks completas quedan marcadas `[x]` en `openspec/changes/<id>/tasks.md`.
- **Después del merge**: archivar el change con `/opsx:archive <id>` — mueve el change a `openspec/changes/archive/` y mergea las delta specs a `openspec/specs/`. Nunca antes del merge.

### 9. Abrir PR

Ver [open-pr.md](../../../docs/workflows/open-pr.md) para el procedimiento canónico de `gh pr create`.

## Hard rules

- **Step 5 hard gate** obligatorio antes de tocar código.
- **Ejecución vía `/opsx:apply`** — no reimplementar a mano el loop de tasks.
- **Sin saltear `/evaluate`** sin acuerdo explícito del equipo.
- **Archivar post-merge** con `/opsx:archive <id>` (nunca antes del merge).
- Usar `git`/`gh` directo; no clientes custom de GitHub API.
- Cumplir las invariantes del [CLAUDE.md](../../../CLAUDE.md) durante toda la implementación.

## Comandos clave

- Backend build: `dotnet build backend/ArsDocendi.slnx`
- Backend test: `dotnet test backend/ArsDocendi.slnx`
- Frontend build: `pnpm --filter frontend build` · lint: `pnpm --filter frontend lint`
- Format: `pnpm format` (raíz)
- OpenSpec: `openspec list`, `openspec status --change <id>`, `openspec validate --strict <id>`

## Arguments

`$ARGUMENTS` — id del change OpenSpec (opcional; para reanudar implementación). Igual correr el hard gate (step 5) antes de tocar código.
