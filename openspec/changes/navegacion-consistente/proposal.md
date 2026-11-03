## Why

Moving around the app loses the user's place. Back sometimes leaves the app (`navegar(-1)` on a deep link) or lands on an unrelated task; list filters, tabs and sort live in `useState` and vanish on back or refresh; nothing warns about unsaved edits; dialogs do not trap focus and one Esc closes every stacked one; unknown routes and missing permissions silently redirect; there is no page title, skip link or focus handling; tab bars and table rows are mouse-first. This is stage 3 of 3 of the internal notifications work: stage 1 laid the destinations, stage 2 added the notifications, this stage makes every way of reaching a destination (and leaving it) consistent. Notifications open details through the same validated "Volver" mechanism.

## What Changes

- **Validated "Volver"**: one shared resolver with an allowlist per entity and a fixed fallback replaces `origenDetalle`. Details receive `state.volverA = pathname + search`; the button is a real link styled as a secondary button ("Volver a Reserva de aulas", "Volver a Revisión", ...). Origin propagates task to child task and across edit flows. `navigate(-1)` is gone. Breadcrumb list link uses the same target and is hidden below 560 px on pages with Volver. Error alerts use `Link` instead of `<a href>`.
- **URL state**: one small shared hook keeps filters, sort and tabs in the query string (defaults omitted, text and sort use `replace`, tabs push). Applied to Aulas (two tables, prefixed), Mis pedidos, Revisión, Personas, Tareas, Proyectos, Períodos and Roles.
- **Scroll**: `.adoc-main` scroll position is saved per history entry and restored on back/forward and on "Volver"; a new page starts at the top; query-only `replace` keeps it.
- **Unsaved changes**: one central router blocker plus `beforeunload`, with a shared dialog ("Tenés cambios sin guardar." / "Seguir editando" / "Descartar cambios"). Guards on the pedido form, persona and rol editors, solicitud aula field, pedido justification and task detail fields.
- **Overlays**: every modal and the mobile menu drawer share the shell behavior (focus trap, focus return, top-most Esc, dirty confirmation, no dismissal while pending). Browser Back with an overlay open closes the top-most overlay and stays on the page. Native `date`/`time` inputs and the library `DatePicker` are replaced by `CampoFecha`/`CampoHora`.
- **Routes and accessibility**: in-shell "Página no encontrada" and "No tenés acceso a esta página" (with "Ir al inicio"), `errorElement` for crashes, the pedido edit route guarded by its section permission, post-login return that keeps path, query and hash (same-origin relative only). Every page sets `document.title`, focuses its `h1` on navigation, announces it in a polite live region and offers a "Saltar al contenido" skip link. Accessible tabs (roving tabindex, Home/End, real `tabpanel`). Table rows get a real link in the primary cell and a visible focus ring.
- **Responsive** (last): the Aulas pages and modals, the solicitud detail and the notifications panel replicate the portal's approach and work from 360 px.

## Capabilities

### New Capabilities

- `navegacion-volver-y-estado-url`: validated Volver, URL state and scroll restoration.
- `cambios-sin-guardar-y-capas`: unsaved-changes guard and overlay behavior.
- `rutas-y-accesibilidad-de-pagina`: 404, no access, crashes, post-login return, title, focus, announcement, skip link, tabs and rows.
- `responsive-aulas-y-notificaciones`: usable layouts from 360 px.

### Modified Capabilities

## Impact

- Frontend only (`frontend/src/shared/navegacion`, `shared/ui`, `app/`, each feature's pages and tables). No backend, API, schema or business rule change; the Microsoft login `target` is only read to confirm it is already validated.
- Docs: `docs/architecture/frontend-conventions.md` (new), the Aulas/Notificaciones design specs and `portal` neighbours where they describe back or modals.
- `@ars-docendi/ui` is not modified: the app wraps its `Modal`, `Drawer` and `Tabs`.
