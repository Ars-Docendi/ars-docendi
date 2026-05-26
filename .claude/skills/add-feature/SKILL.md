---
name: add-feature
description: Implementar una feature de punta a punta. Usar cuando el usuario pide una nueva capability, una mejora, o un comportamiento visible para el usuario, o cuando se reanuda implementación desde una spec + plan aprobados. Sigue specs, planes, fronteras arquitecturales y gates de calidad.
argument-hint: [<feature-kebab-name>]
---

# Add feature

**Source of truth:** [docs/workflows/add-feature.md](../../../docs/workflows/add-feature.md). Leerlo. Este archivo es un puntero, no el playbook.

## Slug

`$ARGUMENTS` es el kebab-case usado en `docs/product/specs/$ARGUMENTS.md` y `docs/plans/active/$ARGUMENTS.md`. Si no se provee, derivar del request y usarlo consistentemente.

## Fast-forward path

Si la spec + plan ya existen aprobados, **saltear los steps 1-6**; arrancar en step 7 (Execute).

## Orden

1 → 2 → 3 (architecture check) → 4 → 5 (human review) → 6 (hard gate) → 7 (execute) → 8 (security) → 9 (QA, docs, close) → 10 (open PR). Sin reordenar.

## Hard rules

- **Step 6 hard gate** OBLIGATORIO antes de tocar código: verificar que existen `docs/product/specs/<slug>.md` Y `docs/plans/active/<slug>.md`.
- **Sin saltear `/evaluate`** sin acuerdo explícito del equipo.
- **No mover plan a `completed/`** antes del merge — usar `/complete-plan` o automatización post-merge.
- Reemplazar `git`/`gh` solo cuando sea necesario; no usar clientes custom de GitHub API.
- Cumplir invariantes del [CLAUDE.md](../../../CLAUDE.md) durante toda la implementación.

## Comandos clave

- Backend build: `dotnet build backend/ArsDocendi.slnx`
- Backend test: `dotnet test backend/ArsDocendi.slnx`
- Frontend build: `pnpm --filter frontend build`
- Frontend lint: `pnpm --filter frontend lint`
- Format: `pnpm format` (raíz)

## Arguments

`$ARGUMENTS` — kebab-slug de la feature.
