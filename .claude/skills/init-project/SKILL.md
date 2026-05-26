---
name: init-project
description: Bootstrap los docs fundacionales de producto (brief, vision, design-principles) desde un prompt humano. Usar una sola vez por proyecto, antes de /plan-feature o /add-feature. Saltar si docs/product/brief.md ya tiene contenido no-placeholder.
argument-hint: [<descripción libre del proyecto>]
---

# Init project

**Source of truth:** [docs/workflows/init-project.md](../../../docs/workflows/init-project.md). Leerlo. Este archivo es un puntero, no el playbook.

## Produce

1. `docs/product/brief.md` — reemplazar el placeholder con 1–4 oraciones del producto + milestone actual.
2. `docs/product/vision.md` — llenar todas las secciones: Summary, Goals, Non-goals, Success metrics. Sin `_(placeholder)_` sin completar.
3. `docs/product/design-principles.md` — mantener el scaffold; ajustar principios + anti-patterns + plataforma específicos del producto.

## Flujo

1. **Aclarar** (si el prompt no cubre todo): preguntar al usuario sobre:
   - Producto en una oración (qué es, para quién).
   - Problema central + por qué ahora.
   - Plataformas (web/mobile/API).
   - In-scope vs out-of-scope del milestone inicial.
   - Métricas de éxito.
   - Dirección de diseño / brand.
2. **Redactar** los 3 docs desde las respuestas. NO inventar datos no provistos; marcar gaps con `_(needs owner input: <qué>)_`.
3. **Mostrar al equipo** para revisión + aprobación.
4. **Sobre aprobación**:
   - Branch `chore/init-project`
   - Commit + push
   - PR a `develop` (ver [open-pr.md](../../../docs/workflows/open-pr.md))

## Hard rules

- **Solo** editar `docs/product/{brief,vision,design-principles}.md`. Sin código, sin otros docs, sin `docs/plans/`, sin `docs/business-rules/`.
- Sin escribir specs (`docs/product/specs/*`) — eso es `/plan-feature`.
- Si `docs/product/brief.md` ya tiene contenido real, parar y preguntar antes de sobreescribir.
- No abrir PR antes de aprobación del equipo.

## Arguments

`$ARGUMENTS` — descripción libre del proyecto. Puede ser vacío; la fase de clarificación cubre faltantes.
