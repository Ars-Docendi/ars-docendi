## Context

- The router is already a data router (`createBrowserRouter`), so `useBlocker` works. Many existing unit tests render pages in `MemoryRouter` (non-data), where `useBlocker` throws: the guard therefore lives in one central component mounted by the shell, and pages only register state in a tiny store.
- There is no browser runner in the repo (no Playwright/Cypress). Regression tests use Vitest + Testing Library with `createMemoryRouter` and the real shell pieces (`GestorDeNavegacion`, `Outlet`). Real-browser-only behavior (scroll geometry, `beforeunload`, native Back, 360 px layout) is listed in the report as needing manual confirmation.
- The user waived the human review gate; the decisions below were taken autonomously.

## Decisions

**D1. `shared/navegacion/` holds the cross-cutting pieces.** `volver.ts` (+ `EnlaceVolver`), `estadoURL.ts`, `restauracionDeScroll.tsx`, `cambiosSinGuardar.tsx`, `paginaActual.tsx`. They live in `shared` because features cannot import each other.

**D2. Volver allowlist is data, not code.** Each entity declares `{ destinos: { "<path pattern>": etiqueta }, respaldo(permisos) }`. Patterns accept `:id` segments (a task opened from `/tareas/proyectos/<id>` is labelled "Proyecto"). The candidate must match `^/[a-z0-9\-/]*(\?[\w%.\-:,=&~+]*)?$`, fit a listed pattern and be openable by the user's permissions. `origenDetalle.ts` is deleted; its test (`DetallePedidoPage.test.tsx`, the loop Mis pedidos, edit, detail) is kept and moved to `state.volverA`.

**D3. Origin propagates by passing the already-resolved target.** A detail page that links onward (task to child task, detail to edit) passes `state: { volverA: <its own resolved target> }`, never its own URL. Saving an edit navigates with `replace` and the same state, so one "Volver" still leads to the list. Notifications navigate without state: they use the fixed fallback.

**D4. One URL-state hook, string codec.** `useFiltrosURL(valoresPorDefecto, prefijo?)` reads/writes `string` and `string[]` (comma-joined) values and omits defaults; `useOrdenURL(prefijo?)` stores `columna:direccion`; `usePestanaURL(porDefecto)` pushes. Text filters and sort use `replace`; tabs push, **except the persona editor tabs (Datos personales / Datos docentes), which `replace`**: they are two views of one form, and adding history entries would make Back leave the form after toggling.

**D5. Scroll is stored per `location.key` plus per `pathname+search`.** The key restores POP; the path restores "Volver" (a PUSH to a list the user already visited). Positions live in `sessionStorage` with a `Map` fallback. Restoration retries for ~3 s until the content is tall enough (data renders after the route commits).

**D6. One blocker for the whole app.** `react-router` supports a single active blocker, so `GestorDeNavegacion` registers the only `useBlocker`. Order: (1) a POP with an open overlay closes the top-most overlay and resets the blocker; (2) navigation to another page while a guard is active opens the shared dialog; (3) query-only navigation on the same path never blocks. Pages mark themselves dirty through `useGuardiaDeCambios(sucio)` and call the returned `permitirSalida()` right before navigating after a successful save (a ref, so it is synchronous). `beforeunload` is attached while any guard is active.

**D7. Overlays share the existing `capas.ts` stack.** `ModalDeFormulario` already traps focus and handles Esc, dirty and pending. The app wrapper `shared/ui/Modal.tsx` keeps the library `Modal` call shape (`open`, `onOpenChange`, `title`, `footer`) but renders `ModalDeFormulario`, so each consumer changes only its import and passes `sucio`/`pendiente`. `Drawer` gets the same treatment through `useCapa` (register, focus first element, return focus). The notifications panel uses `useCapa` too.

**D8. Dates.** `CampoFecha`/`CampoHora` replace every native date/time input and library `DatePicker`; values stay ISO strings, so DTOs do not change.

**D9. Page chrome is central.** `PageHeader` sets `document.title` (string titles, or an explicit `tituloPagina`) and gives the `h1` `tabIndex={-1}`. `SeguimientoDePagina` (in the shell) moves focus to the `h1` and fills the live region on pathname change, retrying briefly while the page loads. The skip link targets `<main id="contenido-principal" tabIndex={-1}>`.

**D10. No access vs not found.** `RequirePermission` renders `PaginaSinAcceso` in place (same URL) instead of `Navigate`. `path: "*"` renders `PaginaNoEncontrada`. A route-level `errorElement` renders inside the shell for crashes (the shell layout route owns it).

**D11. Pedido edit route stays unguarded.** The brief asked to guard `/designaciones/pedidos/:id/editar` if the rest of its section requires it. `aprobacion-pedidos-designacion` ("las rutas `pedidos/:id` y `pedidos/:id/editar` accesibles a cualquier rol autenticado que tenga la visibilidad indicada por la respuesta autoritativa del backend") deliberately leaves it open and the page already shows "Este pedido no es editable" from the backend `accionesPermitidas`. No route guard was added.

**D12. Post-login return.** `LoginPage` builds the return target from `from.pathname + from.search + from.hash` and only follows it when it starts with a single `/`. The backend (`EndpointsAutenticacion.RutaLocalODefecto`) already accepts only a single leading `/` without `//`, `\\` or control characters and keeps the query; no backend change. The frontend previously dropped query and hash and forwarded unvalidated values.

**D13. Responsive follows the portal.** Breakpoint 560 px for paddings and stacked data lists, 768 px (existing) for the shell. Tables scroll inside `.adoc-tabla-scroll`. Touch targets: 24 px minimum, 44 px for primary buttons below 560 px.

## Risks / Trade-offs

- [`useBlocker` cannot be exercised in `MemoryRouter` unit tests] -> Guard and overlay tests use `createMemoryRouter`; page tests that do not need it keep `MemoryRouter` and the store is simply unobserved.
- [A native browser Back is simulated in tests with `router.navigate(-1)`] -> Exactly what the browser's popstate triggers in the router; the real popstate path is listed as needing a real browser.
- [Migrating ~15 modals is wide] -> The wrapper keeps their JSX; each gets `sucio`/`pendiente` only where the form has data.

**D14. What stays local on purpose.** The per-project cuadros of the Tareas home each hold their own column filters in memory (many tables on one page; only the single-table views and the project detail table use the `t.` prefix). "Pedido nuevo" cancel returns to Mis pedidos without its filters. Notifications only open detail pages, so the notification highlight needs no list-row variant.

**D15. Esc, Back and the notifications panel.** The panel already registered in the layer stack (stage 2), so browser Back closes it through the same blocker.
