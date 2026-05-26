---
name: complete-plan
description: Post-merge cleanup que mueve un plan activo a docs/plans/completed/, actualiza frontmatter, llena la sección Completion y opcionalmente actualiza el spec asociado. Usar después de mergear el PR de una feature.
argument-hint: [<feature-kebab>]
---

# Complete plan

**Source of truth:** [docs/workflows/complete-plan-after-merge.md](../../../docs/workflows/complete-plan-after-merge.md). Leerlo.

## Cuándo usar

- Manual: el equipo prefiere cerrar planes a mano en vez de via workflow automático.
- Cuando la automatización (`.github/workflows/close-plan-on-merge.yml`) no aplica (PR tocó múltiples planes, o solo docs).

## Produce

1. `docs/plans/active/<feature>.md` → `docs/plans/completed/<feature>.md` (git mv).
2. Frontmatter actualizado: `status: completed`, `completed_at`, `pr`.
3. Sección `## Completion` llena.
4. Si aplica: `docs/product/specs/<feature>.md` frontmatter → `status: completed`.

## Flujo

1. Verificar pre-condiciones (plan existe en `active/`, PR está mergeado a `develop`).
2. Editar frontmatter + sección Completion del plan.
3. Si aplica, actualizar frontmatter del spec.
4. `git mv` del plan a `completed/`.
5. Branch `docs/complete-<feature>`, commit, push, PR a `develop`.
6. (Opcional) regenerar índices: `pnpm exec tsx scripts/generate-indexes.ts`.

## Hard rules

- **Solo** mover planes con PR mergeado.
- Sección `## Completion` no puede quedar vacía (mínimo: fecha + PR + outcome).
- Sin tocar código en este PR (solo docs).

## Arguments

`$ARGUMENTS` — kebab-slug de la feature a cerrar.
