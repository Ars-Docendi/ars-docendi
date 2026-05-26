# Workflow: Add feature

Flujo completo de implementación de una nueva capability: planning → spec → plan → execute → security → QA → close.

## Cuándo usar

Estás implementando una **nueva capability** (UI o API) que debe trackearse como trabajo de producto + ingeniería.

Para bugs usar [`/fix-bug`](./fix-bug.md). Para nuevo módulo, primero [`/create-module`](./create-module.md).

## Pre-requisitos

- Leer `docs/product/vision.md`.
- Skim de specs existentes (`docs/product/specs/`) y planes activos (`docs/plans/active/`).
- Leer `docs/architecture/dependency-graph.md`.

## Dos fases

| Fase               | Skill           | Escribe                                                        | Termina con         |
| ------------------ | --------------- | -------------------------------------------------------------- | ------------------- |
| **Planning**       | `/plan-feature` | `docs/product/specs/<slug>.md` + `docs/plans/active/<slug>.md` | Pidiendo aprobación |
| **Implementación** | `/add-feature`  | Código + PR                                                    | PR abierto          |

## Steps

### 1. Contexto

- Identificar surface afectadas: `backend/src/Modules.<X>/`, `backend/src/Modules.<X>.Contracts/`, `frontend/src/features/<x>/`, etc.
- Identificar roles afectados y BR-\* aplicables.

### 2. Spec de producto

- Crear `docs/product/specs/<feature-kebab>.md` desde `_template.md`.
- Llenar user stories, roles afectados, BR-\* aplicables, data requirements, API surface, design notes, **definition of done**, anti-patterns.
- Frontmatter: `status: active` cuando se aprueba.

### 3. Inputs de diseño (si hay surfaces UI)

- Confirmar que existen design principles en `docs/product/design-principles.md`.
- Si hay diseño formal (Figma / Pencil / otra herramienta — TBD por el equipo), crear `docs/product/designs/<feature-kebab>-design-spec.md` con la referencia.
- Si no hay herramienta de diseño definida, documentar estados (loading/empty/error/success/awaiting-approval) en la spec.

### 4. Architecture check

- Confirmar dependencias permitidas en `docs/architecture/dependency-graph.md`.
- Si requiere un **nuevo módulo .NET**, correr [`/create-module`](./create-module.md) primero.
- Si extiende API pública de un módulo existente, seguir [`/modify-module`](./modify-module.md) para análisis de impacto.

### 5. Plan de ejecución

- Crear `docs/plans/active/<feature-kebab>.md` desde el template.
- Indicar objetivo, sprint contract / definition of done, approach técnico, riesgos.
- Sección **Acceptance criteria** clara — cada bullet será verificable.
- Frontmatter: status, spec, owner, fechas.

### 6. Human review gate

- **Parar** y presentar plan + definition of done al equipo.
- No tratar "LGTM en chat" como durable; encodear cambios acordados en el archivo del plan.
- **Fin de fase planning**.

### 7. Implementation hard gate

Antes de escribir o editar código:

1. Confirmar que `docs/product/specs/<feature-kebab>.md` existe en disco.
2. Confirmar que `docs/plans/active/<feature-kebab>.md` existe en disco.

Si falta alguno: **parar inmediatamente**. Volver a step 2-5 o invocar `/plan-feature`.

### 8. Execute (implementación)

1. Releer spec + plan end-to-end. Tratar definition of done como contrato.
2. Releer `dependency-graph.md` + `module-anatomy.md`.
3. Si requiere nuevo módulo, correr `/create-module`. Si modifica módulo, `/modify-module`.
4. Implementar **contract-first**: definir/extender `Modules.<X>.Contracts/` antes que internals.
5. Seguir guides path-scoped: `dotnet-modules-guide`, `react-features-guide`.
6. Respetar `docs/quality/golden-principles.md` siempre.
7. Loggear progreso en el plan activo a medida que se avanza.

### 9. Security pass

Read-only / minimal change antes de merge:

- Autorización por rol matchea spec; no nuevos endpoints públicos sin Contracts documentados.
- Sin secrets en código; ver `docs/quality/golden-principles.md`.
- Deps y cross-module imports respetan `docs/architecture/dependency-graph.md` (DAG, sin leak de Internal).
- Si toca deploy o env vars, cross-check `docs/architecture/infrastructure.md`.

Si hay issues, arreglarlas antes de step 10.

### 10. QA, documentación, close out

**Verificación**:

1. Backend: `dotnet test ArsDocendi.slnx` — todos verdes.
2. Frontend: `pnpm --filter frontend lint` + `pnpm --filter frontend build` — todos verdes.
3. Manual spot-check de los flujos por rol afectado.
4. Correr `/evaluate` para validar contra spec + grading-criteria.

**Documentación** (si cambiaste boundaries, APIs, o persistencia):

- `docs/architecture/dependency-graph.md`
- `docs/architecture/api-contracts.md`
- `docs/architecture/data-model.md`
- `docs/architecture/domains/<dominio>.md`
- `docs/business-rules/<dominio>.md` (si la feature introduce/modifica BR-\*)

**Close out**:

- Append entrada final al plan **progress log** + **decisions** con link al PR.
- **Antes del merge**: el plan queda en `docs/plans/active/` con frontmatter `status: in-review`.
- **Después del merge**: seguir [`complete-plan-after-merge.md`](./complete-plan-after-merge.md) — mover a `completed/`, actualizar frontmatter.

### 11. Abrir PR

Ver [`open-pr.md`](./open-pr.md) para procedimiento canonical de `gh pr create`.

## Invocación

- Full flow: `/add-feature` (opcional `<feature-kebab>` para reanudar).
- Resumir implementación: `/add-feature <feature-kebab>` — igual correr step 7 (hard gate) antes de código.
