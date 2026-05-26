---
status: draft # draft | active | in-review | completed | deferred
owner: ""
spec: "docs/product/specs/<feature-kebab>.md"
module: "" # designaciones | aulas | portal | tareas | shared
started: YYYY-MM-DD
last_updated: YYYY-MM-DD
target: YYYY-MM-DD # fecha estimada de PR abierto
---

# Plan: &lt;feature-name&gt;

## Objetivo

Una oración: qué se entrega al final de este plan.

## Spec de referencia

Linkear a la spec correspondiente: [`docs/product/specs/<feature-kebab>.md`](../../product/specs/<feature-kebab>.md)

## Sprint contract / Definition of done

Checklist alta nivel que el evaluador / reviewer va a usar. Cada item DEBE ser verificable.

- [ ] _(checkpoint 1)_
- [ ] _(checkpoint 2)_

## Acceptance criteria

Lista granular, verificable con tests o spot-check. Cada bullet → un test o una verificación manual documentada.

- [ ] `bug-reproduces-in-test` (si aplica) — el red test captura el bug
- [ ] _(criterio funcional)_
- [ ] _(criterio de autorización por rol)_

## Approach técnico

Breve descripción del cómo:

- **Módulos tocados**: `Modules.<X>`, `Modules.<X>.Contracts`, `frontend/src/features/<x>/`
- **Cambios en Contracts** (si aplica): qué interfaces / DTOs nuevos o modificados.
- **Migrations** (si aplica): qué tablas / columnas.
- **Endpoints nuevos** (si aplica): listar.
- **Dependencias cross-module**: si introduce nuevos edges en `dependency-graph.md`.

## BR-\* aplicables

- `BR-<modulo>-NNN` — link a `docs/business-rules/<modulo>.md`

## Roles afectados

- [ ] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [ ] Administrativos
- [ ] Docente

## Riesgos / Open questions

- _(riesgo o pregunta sin resolver)_

## Progress log

Append-only. Una entrada por sesión de trabajo significativa.

- `YYYY-MM-DD` — _(qué se hizo, qué se aprendió, qué bloquea)_

## Decisions

Decisiones tomadas durante implementación que se desvían del plan original. Justificar.

- _(decisión)_ — _(motivo)_

## Completion

_(se llena al cerrar el plan — ver `complete-plan-after-merge.md`)_

- **Fecha**:
- **PR**:
- **Outcome**:
- **Variaciones del plan original**:
- **Follow-ups**:
