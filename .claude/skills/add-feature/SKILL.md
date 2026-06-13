---
name: add-feature
description: Implementar una feature de punta a punta sobre un change OpenSpec aprobado. Usar cuando el usuario pide una nueva capability, una mejora, o un comportamiento visible para el usuario, o cuando se reanuda implementación. Aplica los gates de arquitectura/calidad del proyecto y delega la ejecución de tasks a /opsx:apply.
argument-hint: [<change-id>]
---

# Add feature

**Source of truth:** [docs/workflows/add-feature.md](../../../docs/workflows/add-feature.md). Leerlo. Este archivo es un puntero, no el playbook.

## Modelo

El planning vive en OpenSpec (`/opsx:propose` → `openspec/changes/<id>/`). `/add-feature` NO planifica: orquesta la disciplina del proyecto (architecture check, security, `/evaluate`, PR) sobre un change aprobado y **delega la ejecución de tasks a `/opsx:apply`**.

## Change id

`$ARGUMENTS` es el id (kebab-case) del change OpenSpec en `openspec/changes/$ARGUMENTS/`. Si no se provee, derivar del request o listar con `openspec list`.

## Orden

1 (contexto) → 2 (planning `/opsx:propose` si falta) → 3 (architecture check) → 4 (human review / aprobación) → 5 (hard gate) → 6 (execute vía `/opsx:apply`) → 7 (security) → 8 (QA, docs, close) → 9 (open PR). Sin reordenar.

## Hard rules

- **Step 5 hard gate** OBLIGATORIO antes de tocar código: `openspec status --change "$ARGUMENTS"` con los artefactos de `applyRequires` (típicamente `tasks`) en estado `done`.
- **Ejecución vía `/opsx:apply`** — no reimplementar a mano el loop de tasks.
- **Sin saltear `/evaluate`** sin acuerdo explícito del equipo.
- **Archivar post-merge** con `/opsx:archive $ARGUMENTS` (nunca antes del merge).
- Usar `git`/`gh` directo; no clientes custom de GitHub API.
- Cumplir las invariantes del [CLAUDE.md](../../../CLAUDE.md) durante toda la implementación.

## Comandos clave

- Backend build: `dotnet build backend/ArsDocendi.slnx`
- Backend test: `dotnet test backend/ArsDocendi.slnx`
- Frontend build: `pnpm --filter frontend build`
- Frontend lint: `pnpm --filter frontend lint`
- Format: `pnpm format` (raíz)
- OpenSpec: `openspec list`, `openspec status --change <id>`, `openspec validate --strict <id>`

## Arguments

`$ARGUMENTS` — id del change OpenSpec.
