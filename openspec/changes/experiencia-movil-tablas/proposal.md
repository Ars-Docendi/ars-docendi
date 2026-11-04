# Proposal

## Why

The data pages (Personas, Roles, Mis pedidos, Revisión, Períodos, Aulas, Tareas, Proyectos, Auditoría, Dashboard) and the Centro de ayuda are built for desktop. On a 390 px phone:

- `index.css` applies `zoom: 1.125` at every width, so the page only gets about 347 CSS px.
- All 13 list tables come from `@ars-docendi/ui` (not editable here), keep `nowrap` cells and only scroll sideways with no affordance. Several add a `calc(100vh - N px)` max-height, which produces a second scroll and ignores the mobile browser chrome.
- Filters are per-column popovers inside a sticky `th`, unusable when the column is off-screen.
- The Centro de ayuda sidebar stacks above the content and fills the first screen, and the page overflows horizontally.
- The navigation drawer highlight touches the drawer border.

The earlier `portal-docente-responsive` and `navegacion-consistente` changes made the shell, the Portal and Aulas usable at 360 px but explicitly left grids and administration out. This change closes that gap. The behaviours were validated with the user in an interactive prototype.

## What Changes

- **Table base (A)**: every table scroll container becomes an accessible region with a sticky first column, a right-edge shadow when columns overflow, no inner vertical scroll on phones and `dvh` where a height remains.
- **Card lists below 560 px (B)**: a separate `<ul>` render path replaces the table on ten pages, with a stretched row link, status badges, a 44 px "⋯" actions menu where the page has row actions, and the task semáforo as card tint.
- **Filters and sort on phones (C)**: one "Filtros (n)" button opens a bottom sheet with every filter group, a live "Ver N resultados" button and "Limpiar"; applied filters show as removable chips; sort is a native select; search is 44 px / 16 px.
- **Tabs (D)**: swipeable tab strip with edge shadows for Revisión areas, Aulas sections and Tareas states.
- **Pagination (E)**: card lists paginate client-side, 20 per page, with 44 px targets. This is a deliberate exception to "Grillas sin paginación", limited to phones.
- **Detail pages (F)**: primary actions in a sticky bottom bar with safe-area inset.
- **Centro de ayuda (G)**: no sidebar on phones, search on top of the content, an "En esta página" collapsible index on articles, no horizontal overflow.
- **Sistema and assistant (I)**: after develop replaced Auditoría and Dashboard with the tabbed Sistema section and added the assistant chatbot and its administration tabs, those screens get the same treatment: swipeable Sistema tabs, audit filters through the shared sheet with the detail under its row, a full-screen chat that keeps the input above the keyboard, the conversation rail as a drawer, result tables in the scroll region, and usage and quality lists as cards.
- **Global (H)**: `--ui-zoom: 1` below 768 px, drawer highlight margin, remaining `100vh` to `dvh`, 16 px inputs on phones.

No data, filter state or route changes: the existing URL state hooks drive both renderings.

## Capabilities

### New Capabilities

- `listas-movil`: table base for narrow viewports, card list, tabs, pagination and sticky detail actions.
- `filtros-movil`: filter sheet, chips, native sort select and search field on phones.
- `escala-y-viewport-movil`: UI zoom, viewport units, input font size and drawer highlight.
- `ayuda-movil`: Centro de ayuda layout and navigation on phones.
- `asistente-movil`: the assistant modal, conversation rail and result tables on phones.

### Modified Capabilities

- `grillas-datos`: scroll requirement gains the phone behaviour; "Grillas sin paginación" gets the phone exception.

## Impact

- Frontend only. New shared pieces under `shared/ui` (`ListaTarjetas`, `HojaFiltros`, `usePantallaTelefono`), a small CSS layer in `index.css`/`shared/ui`, and per-page adapters in `features/{personas,roles,designaciones,aulas,tareas,sistema,asistente}`. Help: `ayuda.css`, `LayoutAyuda`, `BarraLateralAyuda`, `BuscadorAyuda`, article page.
- Docs: design specs under `docs/product/designs/` for the affected features, and the Centro de ayuda articles that describe filters, tabs and tables (rule 9).
- No backend, API, schema or dependency changes. Dependency graph unaffected.
- Rollback: revert the commit; no persisted state is introduced.
