---
name: plan-feature
description: Expandir un brief o feature request en spec + plan ejecutable. Usar cuando se empieza desde un prompt corto o scope poco claro. Fase 1 del flujo de 2 fases (la 2 es /add-feature).
argument-hint: [<descripción de la feature o kebab-slug>]
---

# Plan feature

**Source of truth:** [docs/workflows/add-feature.md](../../../docs/workflows/add-feature.md) (steps 1–6) + templates en `docs/product/specs/_template.md` y `docs/plans/active/_template.md`.

## Produce

1. `docs/product/specs/<slug>.md` desde `_template.md` con frontmatter completo.
2. `docs/plans/active/<slug>.md` desde `_template.md` con frontmatter completo + sección **Definition of done** + **Acceptance criteria** explícitas.

## Flujo

1. **Si `$ARGUMENTS` es vago**: aclarar con el usuario qué se va a construir (roles afectados, scope, definition of done, BR-\* aplicables).
2. **Skim** specs/planes existentes para no duplicar.
3. **Leer** `docs/architecture/dependency-graph.md` para confirmar dependencias permitidas.
4. **Redactar** la spec desde el template.
5. **Redactar** el plan ejecutable con acceptance criteria verificables (cada bullet → un test o spot-check).
6. **Mostrar al equipo** + iterar.
7. **Aprobación** → fase 2 (`/add-feature <slug>`) implementa.

## Hard rules

- **Solo** archivos bajo `docs/`. Sin código de aplicación.
- **Sin `gh pr create`** en esta fase.
- Una entrada en el plan por sección obligatoria (no skipear "Definition of done" ni "Acceptance criteria").
- Si la feature requiere nuevo módulo .NET, indicar que `/create-module` debe correr antes de `/add-feature`.

## Arguments

`$ARGUMENTS` — descripción de la feature o kebab-slug.
