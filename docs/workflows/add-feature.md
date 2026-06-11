# Workflow: Add feature

Flujo completo de implementación de una nueva capability: planning (OpenSpec) → gate → execute → security → QA → close.

## Cuándo usar

Estás implementando una **nueva capability** (UI o API) que debe trackearse como trabajo de producto + ingeniería.

Para bugs usar [`/fix-bug`](./fix-bug.md). Para nuevo módulo, primero [`/create-module`](./create-module.md).

## Modelo: OpenSpec planifica, `/add-feature` aplica la disciplina del proyecto

El planning vive en OpenSpec (`openspec/changes/<id>/`). `/add-feature` **no planifica**: orquesta la disciplina de implementación del proyecto (architecture check, security pass, `/evaluate`, PR) sobre un change OpenSpec ya aprobado, y **delega la ejecución de tasks a `/opsx:apply`**.

| Fase               | Comando         | Escribe                                                      | Termina con         |
| ------------------ | --------------- | ------------------------------------------------------------ | ------------------- |
| **Planning**       | `/opsx:propose` | `openspec/changes/<id>/` (proposal + design + specs + tasks) | Pidiendo aprobación |
| **Implementación** | `/add-feature`  | Código + PR (ejecución vía `/opsx:apply`)                    | PR abierto          |

## Pre-requisitos

- Leer `docs/product/vision.md`.
- Revisar los changes existentes: `openspec list`.
- Leer `docs/architecture/dependency-graph.md`.

## Steps

### 1. Contexto

- Identificar surfaces afectadas: `backend/src/Modules.<X>/`, `backend/src/Modules.<X>.Contracts/`, `frontend/src/features/<x>/`.
- Identificar roles afectados y BR-\* aplicables.

### 2. Planning con OpenSpec (si todavía no existe el change)

- Si no hay un change para esta feature, correr `/opsx:propose "<descripción>"` → genera `openspec/changes/<id>/` (proposal, design, specs, tasks).
- `openspec/config.yaml` inyecta el contexto e invariantes del proyecto en cada artefacto generado.
- Para pensar antes de proponer: `/opsx:explore`.

### 3. Architecture check

- Confirmar dependencias permitidas en `docs/architecture/dependency-graph.md`.
- Si requiere un **nuevo módulo .NET**, correr [`/create-module`](./create-module.md) primero.
- Si extiende API pública de un módulo existente, seguir [`/modify-module`](./modify-module.md) para análisis de impacto.

### 4. Human review gate (aprobación del plan)

- **Parar** y presentar el change (`openspec show <id>` / `openspec view`) al equipo.
- No tratar "LGTM en chat" como durable; los cambios acordados se encodean en los artefactos del change.
- **Fin de fase planning.**

### 5. Implementation hard gate

Antes de escribir o editar código:

1. Confirmar que el change está apply-ready: `openspec status --change "<id>"` con todos los artefactos de `applyRequires` (típicamente `tasks`) en estado `done`.

Si no está listo: **parar inmediatamente**. Volver al step 2 o completar el change con `/opsx:propose`.

### 6. Execute (vía `/opsx:apply`)

1. Delegar la ejecución de las tasks a **`/opsx:apply <id>`** (OpenSpec es dueño del loop de tasks + checkboxes en `tasks.md`).
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

1. Backend: `dotnet test ArsDocendi.slnx` — todos verdes.
2. Frontend: `pnpm --filter frontend lint` + `pnpm --filter frontend build` — todos verdes.
3. Manual spot-check de los flujos por rol afectado.
4. `openspec validate --strict <id>` — verde.
5. Correr `/evaluate` para validar contra spec + grading-criteria.

**Documentación** (si cambiaste boundaries, APIs o persistencia):

- `docs/architecture/dependency-graph.md`
- `docs/architecture/api-contracts.md`
- `docs/architecture/data-model.md`
- `docs/architecture/domains/<dominio>.md`
- `docs/business-rules/<dominio>.md` (si la feature introduce/modifica BR-\*)

**Close out**:

- Las tasks completas quedan marcadas `[x]` en `openspec/changes/<id>/tasks.md`.
- **Después del merge**: archivar el change con [`/opsx:archive <id>`](../../openspec/) — mueve el change a `openspec/changes/archive/` y mergea las delta specs a `openspec/specs/`.

### 9. Abrir PR

Ver [`open-pr.md`](./open-pr.md) para el procedimiento canónico de `gh pr create`.

## Invocación

- Full flow: `/add-feature` (opcional `<id>` del change para reanudar).
- Reanudar implementación: `/add-feature <id>` — igual correr el hard gate (step 5) antes de tocar código.
