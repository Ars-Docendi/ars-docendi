---
name: init-project
description: Bootstrap los docs fundacionales de producto (brief, vision, design-principles) desde un prompt humano. Usar una sola vez por proyecto, antes de /opsx:propose o /add-feature. Saltar si docs/product/brief.md ya tiene contenido no-placeholder.
argument-hint: [<descripción libre del proyecto>]
---

# Init project

Bootstrap único que llena `docs/product/*` con info real del proyecto. Corre **una sola vez** al iniciar, antes de cualquier `/opsx:propose` o `/add-feature`.

## Cuándo usar

- Proyecto recién creado o `docs/product/brief.md` todavía contiene el placeholder del template.
- Skills downstream (`/opsx:propose`, `/add-feature`) necesitan contexto de producto real.

Skip si `docs/product/brief.md` ya tiene contenido específico del proyecto.

## Artefactos producidos

| Archivo                             | Rol                                                                 |
| ----------------------------------- | ------------------------------------------------------------------- |
| `docs/product/brief.md`             | 1–4 oraciones describiendo el producto + milestone actual           |
| `docs/product/vision.md`            | Summary, Goals, Non-goals, Success metrics                          |
| `docs/product/design-principles.md` | Principios + anti-patterns + plataforma — del producto, no defaults |

NO crea specs, planes, business rules, ni código.

## Flujo

### 1. Aclarar requisitos

Antes de escribir, hacer preguntas al usuario sobre:

1. **Producto en una oración** — qué es, para quién.
2. **Problema central** + por qué ahora.
3. **Plataformas** — web, mobile, API, o combinación.
4. **In scope** vs **out of scope** para el milestone inicial.
5. **Métricas de éxito** — cómo sabemos que funciona.
6. **Dirección de diseño / brand** — tono, referencias, anti-patterns a evitar.

### 2. Redactar los 3 docs

Llenar brief / vision / design-principles con las respuestas. Reglas:

- Sin copy placeholder (`_(...)_`, `TODO`, `lorem ipsum`).
- Sin inventar datos. Donde no haya respuesta, marcar inline `_(needs owner input: <qué>)_`.
- Mantener la estructura de los templates — solo reemplazar contenido.
- `design-principles.md`: mantener headers (Principios, Anti-patterns, Plataforma). Ajustar bullets al brand del producto.

### 3. Aprobar y commitear

- Mostrar los 3 docs al usuario / equipo.
- Iterar con feedback hasta aprobación.
- Crear branch `chore/init-project`, commit los 3 archivos, push.
- Abrir PR a `develop` (ver [open-pr.md](../../../docs/workflows/open-pr.md)).

## Reglas duras

- **Solo** editar `docs/product/{brief,vision,design-principles}.md`. Cualquier otro path está fuera de scope de esta skill.
- **Sin código**, sin migrations, sin infra, sin `docs/plans/`, sin `docs/business-rules/`.
- Sin escribir specs — eso es `/opsx:propose`.
- **Sin `gh pr create` antes de aprobación**.
- **Una vez por proyecto**. Si `brief.md` ya tiene contenido real, preguntar antes de sobreescribir.

## Handoff

Después del merge:

- `/architecture-proposal` — para bootstrap de la arquitectura.
- `/opsx:propose` — para la primera feature.

## Arguments

`$ARGUMENTS` — descripción libre del proyecto. Puede ser vacío; la fase de clarificación cubre faltantes.
