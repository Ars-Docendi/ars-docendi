## Context

El proyecto tiene hoy su planning en `docs/` con automatización propia (skills `/plan-feature` y `/complete-plan`, CI `close-plan-on-merge`, `scripts/generate-indexes.ts`) y declara `docs/` como system of record. En la Fase 0 se bootstrapeó OpenSpec real (`@fission-ai/openspec`, devDep + `openspec/config.yaml` + comandos `/opsx:*`). El equipo decidió **reemplazo total**: `openspec/` pasa a ser la única fuente de verdad de specs y changes.

**Estado real del sistema viejo (verificado)**: `docs/plans/active/` y `docs/product/specs/` solo contienen **plantillas** (`_template.md`, `_bug-template.md`); `docs/plans/completed/` está vacío. No hay specs ni planes reales. Existe `docs/plans/backlog.md` (un backlog) y los docs de producto/UX (`brief.md`, `vision.md`, `design-principles.md`, `docs/product/designs/`), que **no son** specs de feature.

Es un cambio de **tooling y gobernanza**: no toca código `.NET`, ni Contracts, ni el grafo de dependencias, por lo que las invariantes de arquitectura de código (cross-module vía Contracts, DAG, Contracts/Shared puros, Controller→Service→Repository, ping endpoint) no aplican aquí.

## Goals / Non-Goals

**Goals:**

- `openspec/` como única fuente de verdad de specs (`openspec/specs/`) y changes (`openspec/changes/`).
- Un solo frente de planning: `/opsx:explore|propose|apply|archive`.
- Conservar la disciplina de implementación y los gates de arquitectura/calidad del proyecto (las invariantes se inyectan vía `openspec/config.yaml:rules`).
- Migración reversible, por PRs encadenados.

**Non-Goals:**

- No se reescriben ni se tocan las skills de implementación/calidad (`/create-module`, `/modify-module`, `/fix-bug`, `/add-tests`, `/pr-review`, `/security-audit`, `/architecture-drift-check`, `/ci-fix`).
- No se modifican reglas de negocio `BR-<modulo>-NNN` ni sus citas (siguen en `docs/business-rules/`).
- No se toca código de los módulos backend ni el frontend.
- **No se tocan los docs de producto (`brief.md`, `vision.md`, `design-principles.md`), de UX (`docs/product/designs/`, invariante #12) ni el `backlog.md`**: se quedan en `docs/`, OpenSpec no los reemplaza.

## Decisions

### D1: OpenSpec real, no el modo "openspec" de Gentle SDD

Se adopta `@fission-ai/openspec` (CLI con `validate`/`archive`/`status`). **Alternativa descartada**: seguir con el orquestador Gentle SDD y su modo `openspec`, que es un clon casero del layout sin validación por CLI y que solo usaba un dev. Razón: estándar del equipo + validación automatizable + una sola herramienta dueña de `openspec/`.

### D2: Reemplazo total, no coexistencia

`openspec/` reemplaza a `docs/plans/` y `docs/product/specs/` como fuente de verdad de planning. **Alternativa descartada**: coexistencia fina (OpenSpec como front-end, docs/ como capa publicada). Razón: dos fuentes de verdad generan drift; el equipo eligió consolidar.

### D3 (resuelto): `/add-feature` reapuntado, con ejecución delegada a `/opsx:apply` (híbrido)

`/add-feature` se conserva como orquestador de la disciplina de implementación del proyecto, con dos cambios:

- Su **precondición** deja de ser `docs/product/specs/<slug>.md` + `docs/plans/active/<slug>.md` y pasa a ser un change OpenSpec aprobado (`openspec/changes/<id>/` con `tasks` `done`).
- Su **step de ejecución (7)** delega en `/opsx:apply` (OpenSpec es dueño del loop de tasks + checkboxes). `/add-feature` conserva solo lo específico del proyecto: gate contra change aprobado, architecture check previo, security pass, `/evaluate` y apertura del PR.

**Alternativas descartadas**: (a) eliminar `/add-feature` y usar solo `/opsx:apply` + `/pr-review` — empuja toda la disciplina al review post-hoc, el lugar más caro para cachar violaciones de arquitectura; (b) `/add-feature` con su propio loop de ejecución — duplica lo que `/opsx:apply` ya hace. El híbrido evita ambos: cada herramienta hace lo que mejor hace, sin duplicación, con los gates de alto valor antes y durante el código.

### D4: Archivado deliberado, no automático en CI

El cierre de un change se hace con `/opsx:archive` (mueve a `archive/` y mergea delta specs a `openspec/specs/`) como **paso explícito** (commit/PR), no como CI automático post-merge. **Alternativa descartada**: replicar `close-plan-on-merge` como workflow que corra `openspec archive` solo. Razón: el archive muta `openspec/specs/` (la fuente de verdad); hacerlo en CI silencioso puede generar conflictos y cambios no revisados.

### D5: Sin migración de contenido — solo retiro de plantillas

El sistema viejo **no tiene contenido real que migrar** (verificado): solo plantillas (`docs/plans/active/_template.md`, `docs/product/specs/_template.md`, `docs/product/specs/_bug-template.md`) y un `backlog.md`. Por lo tanto NO hay conversión de specs ni recreación de planes. Las plantillas viejas se **retiran** junto con las skills que las usaban; OpenSpec aporta sus propias plantillas vía `openspec instructions`. **Alternativa descartada**: convertir/migrar contenido. Razón: no existe tal contenido. Los docs de producto, UX y el backlog se quedan en `docs/`.

### D6: `openspec validate` en CI

Se agrega `openspec validate --strict` al pipeline para que la fuente de verdad no se rompa silenciosamente (el formato de scenarios es estricto). `scripts/generate-indexes.ts` deja de indexar planes/specs; `openspec list` cumple esa función.

### D7 (resuelto): el glue de OpenSpec se versiona en el repo (Modelo A)

Los archivos generados por `openspec init` que son **glue de la CLI** — `.claude/commands/opsx/` y `.claude/skills/openspec-*` — se **commitean** al repo (no se gitignorean). Esto separa dos cosas: `openspec/` (config + specs + changes) es fuente de verdad y siempre se versiona; el glue es un artefacto derivado de la versión de la CLI que igual se versiona.

Razón: matchea la filosofía del proyecto (skills versionadas, clone-and-go, sin frameworks globales) y el `pnpm-lock.yaml` ya **pinea la versión de la CLI** para todo el equipo, lo que neutraliza el drift (todos corren la misma CLI → el glue commiteado matchea para todos).

Disciplina asociada: (1) al bumpear el devDep `@fission-ai/openspec`, correr `openspec update` y commitear el glue regenerado en el **mismo PR**; (2) **nadie corre `openspec init` global** — se clona y listo (evita el único caso real de duplicación: glue project-local + global).

**Alternativa descartada (Modelo B)**: gitignorear el glue y que cada miembro corra `openspec init`/`update`. Es el default de la doc de OpenSpec, pero agrega setup por persona, riesgo de versiones distintas entre miembros, y contradice el clone-and-go del proyecto.

## Risks / Trade-offs

- **Período de doble sistema durante la migración** (skills viejas aún presentes mientras se reapunta `/add-feature`) → Mitigación: PRs encadenados; se retiran `/plan-feature` y `/complete-plan` en el mismo PR que reapunta `/add-feature`.
- **Curva de aprendizaje del equipo (`/opsx:*`)** → Mitigación: actualizar `ONBOARDING.md` + cheat sheet en el mismo corte.
- **`openspec/specs/` como fuente de verdad sin validación en CI** → Mitigación: D6 (`openspec validate` en CI).
- **Las skills `openspec-*` conviven con las del proyecto en `.claude/skills/`** → Mitigación: documentar en la tabla de skills de `CLAUDE.md` cuáles son de planning (OpenSpec) y cuáles de implementación (proyecto).

## Migration Plan

1. **Fase 0 (hecha)**: devDep + `openspec init` + `openspec/config.yaml`. Independiente y reversible.
2. **Fase 2 — Retiro del sistema viejo**: retirar `/plan-feature` y `/complete-plan` + sus plantillas; reapuntar `/add-feature` (D3); reemplazar `close-plan-on-merge` (CI + script) por `openspec archive`; recortar `generate-indexes`; agregar `openspec validate` al CI (D6).
3. **Fase 3 — Gobernanza**: `CLAUDE.md` (tabla de navegación, tabla de skills, estructura, invariantes #5/#10), `ONBOARDING.md`, `README.md`, `CONTRIBUTING.md`, `docs/workflows/*`.
4. **Verificación y cierre**: sin referencias colgadas, `openspec validate --strict` en verde, smoke del flujo, archivar el change.

**Rollback**: cada fase es un PR revertible. Como no hay contenido real que migrar, no hay riesgo de pérdida de información.

## Open Questions

Ninguna pendiente.

> **Resuelto — D3**: `/add-feature` reapuntado con la ejecución delegada a `/opsx:apply` (híbrido). Ver Decisions.
> **Resuelto — backlog**: `docs/plans/backlog.md` se queda en `docs/`. Ver Non-Goals.
