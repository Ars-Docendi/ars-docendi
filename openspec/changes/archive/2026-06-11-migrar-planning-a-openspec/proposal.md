## Why

El proyecto mantiene su capa de planning en `docs/` (specs en `docs/product/specs/`, planes en `docs/plans/active|completed/`) con automatización propia (skills `/plan-feature` y `/complete-plan`, CI `close-plan-on-merge`, `scripts/generate-indexes.ts`). El equipo decidió estandarizar en **OpenSpec** (`@fission-ai/openspec`, ya bootstrapeado en la Fase 0). Mantener los dos sistemas en paralelo crea **dos fuentes de verdad** para las specs y duplica el flujo de planificación. Esta migración consolida todo en OpenSpec: `openspec/` pasa a ser la única fuente de verdad de specs y changes.

## What Changes

- **BREAKING**: Se retiran las skills `/plan-feature` y `/complete-plan`. Su rol lo cumplen `/opsx:propose` (genera proposal + design + specs + tasks) y `/opsx:archive` (cierra el change).
- **BREAKING**: El gate de implementación de `/add-feature` deja de exigir `docs/product/specs/<slug>.md` + `docs/plans/active/<slug>.md` y pasa a exigir un change OpenSpec aprobado (`openspec/changes/<id>/` con `tasks` completas según `openspec status`).
- **Sin migración de contenido**: `docs/plans/` y `docs/product/specs/` solo contienen plantillas (`_template.md`, `_bug-template.md`); no hay planes ni specs reales. Las plantillas se retiran junto con las skills viejas — OpenSpec aporta las suyas vía `openspec instructions`. Los docs de **producto** (`brief.md`, `vision.md`, `design-principles.md`), de **UX** (`docs/product/designs/`, invariante #12) y el `docs/plans/backlog.md` **se quedan en `docs/`** (OpenSpec no los reemplaza).
- **Automatización**: se reemplaza `scripts/close-plan-on-merge.ts` + `.github/workflows/close-plan-on-merge.yml` por el flujo `openspec archive`. `scripts/generate-indexes.ts` deja de indexar planes y specs (esa vista la da `openspec list`).
- **Gobernanza**: se reformulan los invariantes **#5** (spec + plan) y **#10** (plan lifecycle) en términos de changes OpenSpec; se actualizan `CLAUDE.md` (tabla de navegación, tabla de skills, estructura del repo), `ONBOARDING.md`, `README.md`, `CONTRIBUTING.md` y `docs/workflows/*`.
- **Se conservan sin cambios** las skills de implementación y calidad: `/create-module`, `/modify-module`, `/fix-bug`, `/add-tests`, `/pr-review`, `/security-audit`, `/architecture-drift-check`, `/ci-fix`, `/init-project`, `/architecture-proposal`, y las guías path-scoped. OpenSpec reemplaza el **frente de planning**, no la disciplina de implementación ni los gates de arquitectura.

## Capabilities

### New Capabilities

- `openspec-planning-workflow`: El planning del proyecto se hace con OpenSpec (`/opsx:explore` → `/opsx:propose` → `/opsx:apply` → `/opsx:archive`). `openspec/` es la fuente de verdad; `openspec/config.yaml` inyecta el contexto e invariantes del proyecto en cada artefacto generado.
- `feature-implementation-gate`: La implementación de cualquier feature se gatea contra un change OpenSpec aprobado con `tasks` listas, reemplazando el gate de `docs/product/specs/` + `docs/plans/active/` (invariante #5 reformulado).
- `change-lifecycle`: Ciclo de vida de un change (creación → apply → archive con `openspec archive`), reemplazando el plan lifecycle `active/` → `completed/` y la automatización `close-plan-on-merge` (invariante #10 reformulado).

### Modified Capabilities

Ninguna. `openspec/specs/` está vacío al iniciar esta migración, por lo que todas las capabilities son nuevas (no hay specs OpenSpec previas cuyos requisitos cambien).

## Impact

- **Módulos backend / grafo de dependencias**: sin impacto. Es un cambio de tooling y gobernanza; no toca código `.NET`, ni Contracts, ni el grafo de dependencias de `docs/architecture/dependency-graph.md`. No hay consumidores cross-module afectados.
- **Tooling / repo**: `package.json` + `pnpm-lock.yaml` (devDep, ya en Fase 0), `.claude/` (skills/comandos), `scripts/`, `.github/workflows/`, `docs/` (gobernanza + retiro de plantillas), `CLAUDE.md`, `ONBOARDING.md`, `README.md`, `CONTRIBUTING.md`.
- **Normativa institucional / BR-\***: sin impacto directo. Las reglas `BR-<modulo>-NNN` y sus citas siguen vigentes en `docs/business-rules/`; la migración solo cambia dónde viven specs y planes, no las reglas de negocio.
- **Plan de rollback**: la migración va por PRs encadenados y reversibles. Como no hay contenido real que migrar (solo plantillas), no hay riesgo de pérdida de información; revertir = revertir los PRs. La Fase 0 (devDep + `openspec/`) ya es independiente y reversible por sí sola.
