# Design

## Context

Breakpoints from `navegacion-consistente` D13 stay: 560 px (phone: paddings, stacked data) and 768 px (shell). `usePantallaAngosta` (max-width 767 px) already exists for the shell. Tables come from `@ars-docendi/ui` and cannot be edited in this repo, so overrides live in app CSS or wrapper components. Reference: the validated prototype (`Main`, `ListaMovil`, `NavMovil`, `Ayuda` and per-page files); its `pc-*` class names are prototype-only.

## Decisions

**D1. Scale.** `--ui-zoom: 1` below 1024 px (phones and tablets); 1.125 stays from 1024 px. The validated prototype dropped the zoom on tablets too: at 768 px the sidebar takes 200 px, and the 1.125 zoom left only about 480 CSS px for the table. Shell and stage rules already divide by `--ui-zoom`, so they follow.

**D2. Table base (A).** A wrapper `ContenedorTabla` (shared/ui) renders the scroll container with `role="region"`, `tabIndex=0`, `aria-label` and the existing `.adoc-tabla-scroll`. CSS: `position: sticky; left: 0` on the first column cell, a right-edge shadow driven by a scroll listener that toggles a `data-desborde` attribute, and `scroll-padding-inline-start`/`scroll-padding-block-start` equal to the sticky column width and header height so focus never lands under them (WCAG 2.4.11). Below 560 px the `maxHeight: calc(100vh - N)` props are not applied (page scrolls); above, the `ALTO_TABLA_*` constants use `dvh`.

**D3. Card list (B).** `ListaTarjetas` renders a `<ul>` of `<li>` cards from a typed item list: `titulo` (the existing `EnlaceFila`, stretched with a `::after` overlay), `meta` (mono line), `linea` (secondary), `insignias` (the module's real status badge components, reused, not copies), optional `acciones`, optional `tinte` (`por-vencer`/`vencida`). The "⋯" button (44 px) uses the existing `MenuAcciones` and calls the same handlers as the table row, and is omitted when `acciones` is empty. Each page builds the items next to its table with a small mapper; the table and the cards share the same filtered/sorted rows, so there is one source of data. Rendering switches in JS via `usePantallaTelefono()` (matchMedia `max-width: 559px`), not by restyling table elements, so table semantics stay intact. Detail navigation is unchanged (Períodos opens its edit modal).

**D4. Filters (C).** `HojaFiltros` reuses the app `Drawer` with `side="bottom"`. Each page declares its filter groups once (`GrupoFiltro[]`: id, title, options, current value, setter) reusing `MultiSelectFiltro` options and the existing URL hooks (`useFiltrosURL`, `useOrdenURL`). The sheet edits a local draft; "Ver N resultados" computes N from the same filter function over the unpaginated rows and applies the draft, "Limpiar" resets the draft. Applied filters render as removable chips plus "Limpiar filtros". Sort is a native `<select>` bound to the sort hook. Search is an `<input type="search" inputmode="search">` at 44 px / 16 px. No filter state is added.

**D5. Tabs (D).** `Pestanas` (shared/ui) gets a phone style: horizontal scroll with `scroll-snap`, edge shadows when more tabs exist, and `scrollIntoView({ inline: "center" })` of the active tab on select. Counts are kept. Aulas sections and Tareas states become tabs on phones (Tareas replaces the Pendientes/Terminadas/Todas select); desktop keeps the select.

**D6. Pagination (E).** Phone card lists paginate client-side with 20 items per page using the `@ars-docendi/ui` `Pagination` where its markup fits 44 px targets, otherwise a thin local wrapper. The page resets to 1 when filters, search or sort change. This is an explicit exception to "Grillas sin paginación" because a 150-card page is unusable on a phone; desktop is unchanged.

**D7. Detail pages (F).** A `BarraAccionesFija` (sticky bottom, `padding-bottom: env(safe-area-inset-bottom)`) wraps the primary actions on phones. The back link pattern (`adoc-enlace-volver`, crumbs hidden below 560 px via `.con-volver`) is unchanged.

**D8. Help (G).** Below 900 px the sidebar is hidden; `BuscadorAyuda` moves to the top of the content (44 px / 16 px); navigation is home, module, article with the existing back link. On articles an "En esta página" `<details>` index (links to step anchors) replaces the expanded step tree. The horizontal overflow is fixed at its source (long words/images/code with `min-width: 0`, `overflow-wrap: anywhere`, `max-width: 100%`), verified at 390 px. Captures keep opening in `VisorImagen`.

**D9. Global (H).** Drawer items inside `.adoc-nav-drawer .body` get a right margin so highlight/hover do not touch the 1.5 px border. `100vh` becomes `100dvh` in `ayuda.css:23`, `LoginPage.css:10`, `TerminosPage.css:4`, `FiltroEncabezado.css:55` and the `ALTO_TABLA` constants. Inputs, selects and textareas are `font-size: 16px` below 560 px to avoid iOS zoom on focus.

**D10. Naming and scope.** Spanish identifiers (`ListaTarjetas`, `HojaFiltros`, `usePantallaTelefono`, `adoc-lista-tarjetas`, `adoc-hoja-filtros`). The shared component and hook are justified by ten consuming pages, not speculative. No new dependencies.

**D11. Sistema and assistant after the develop merge.** The tabbed Sistema section replaced the Auditoría and Dashboard pages this change first adapted, so the phone work is re-applied to the new components instead of restoring the old ones:

- Sistema uses the shared `Pestanas` (same `.adoc-tabs` markup, so desktop is unchanged) to get the phone strip; its grid gets one shrinkable track so no child widens the page.
- The audit list was already a list of row buttons, not a table, so it is restyled on phones (two lines per event) instead of duplicated as `ListaTarjetas`. The detail panel, beside the list on desktop, renders under the selected row on phones. `FiltrosAuditoriaMovil` maps the URL filter state onto `HerramientasLista`: the period stays a visible segmented control (it is the primary filter and has a non-empty default, which a chip cannot express), action and module become single-choice groups (`unica` on `GrupoOpciones`, additive), dates are typed dd/mm/aaaa and a changed date covers the whole day while an unchanged one keeps its desktop hour. Search keeps the desktop 300 ms debounce.
- The assistant chat is a modal that already went full-screen at 640 px. On phones the rail becomes a drawer rendered inside the modal (absolute, like the expanded table) rather than a portal `Drawer`, because the library modal closes on any Escape that reaches `window`: the drawer stops Escape itself. It stays mounted while closed (`hidden`) so the undo notice survives. The modal height follows `visualViewport` through `useAltoVisible` (CSS variables on the root while open), because `dvh` does not shrink for the on-screen keyboard on iOS Safari nor, by default, on Chrome for Android; the global viewport meta is left alone.
- Result tables switch from `Table.Root` to `ContenedorTabla` at every width (sticky first column, region, shadow), like the other tables in D2.
- Assistant administration: the usage rows hold inline editors, so the cards reuse the very same cell renderers through two additive `ListaTarjetas` options: a plain title when a row has no destination, and a `detalle` slot for inline controls. «Respuestas con problemas» is server-paginated, so it uses `paginar={false}`. The small numeric tables (per model, chart table view) stay tables in `ContenedorTabla`, per D2.

## Risks / Trade-offs

- Two render paths per page can drift. Mitigation: both consume the same filtered rows and the same handlers; the flow tests exercise the card path.
- Sticky first column can crowd narrow tablets. Mitigation: only applied when the container overflows.
- Zoom 1 on 768 px+ phones in landscape changes density; accepted.

## Migration / Rollback

CSS and React only. Revert the commit.

## Open Questions

- Page size 20: tune if lists feel short on large phones.
