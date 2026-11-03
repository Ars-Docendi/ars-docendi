# Tasks

## 1. Shared foundation

- [x] 1.1 In `frontend/src/index.css`, set `--ui-zoom: 1` below 1024 px and keep 1.125 from 1024 px. Verify in the browser at 390, 800 and 1280 px that the shell, stages and drawer still lay out (they divide by `--ui-zoom`).
- [x] 1.2 Fix the drawer highlight: right margin on items inside `.adoc-nav-drawer .body` in `app/shell/shell.css`. Verify visually that hover and active do not touch the border.
- [x] 1.3 Replace the remaining `100vh` with `100dvh` in `features/ayuda/ayuda.css`, `LoginPage.css`, `TerminosPage.css`, `shared/ui/FiltroEncabezado.css` and the `ALTO_TABLA` constants (TablaPersonas, TablaRoles, TablaMisPedidos, TablaRevision, TablaPeriodos). Below 560 px do not apply the max-height.
- [x] 1.4 Add 16 px inputs/selects/textareas below 560 px in the global CSS.
- [x] 1.5 Create `shared/ui/ContenedorTabla` (region role, `tabindex`, `aria-label`, sticky first column, overflow shadow, `scroll-padding`) and use it in the 13 tables. Verify keyboard focus on an off-screen cell stays visible.
- [x] 1.6 Create `usePantallaTelefono` (matchMedia `max-width: 559px`) next to `usePantallaAngosta`.
- [x] 1.7 Create `shared/ui/ListaTarjetas` (`<ul>` cards: stretched row link, meta, secondary line, badges, optional tint, optional 44 px "⋯" built on `MenuAcciones`) with its CSS (`adoc-lista-tarjetas`) and client-side pagination (20 per page, resets on filter/search/sort change).
- [x] 1.8 Create `shared/ui/HojaFiltros` (bottom `Drawer`, draft state, "Ver N resultados", "Limpiar"), the applied-filter chips with "Limpiar filtros", the native sort select and the 44 px search field, all driven by the page's existing `useFiltrosURL`/`useOrdenURL` state.
- [x] 1.9 Add the phone style to `shared/ui/Pestanas` (scroll strip, edge shadows, scroll active tab into view). Keep `Pestanas.test.tsx` green.
- [x] 1.10 Create `BarraAccionesFija` (sticky bottom bar with `env(safe-area-inset-bottom)`).

## 2. Pilot: Personas

- [x] 2.1 Wire Personas: filter groups, sort options and card mapper next to `TablaPersonas`, rendering cards below 560 px and the table otherwise.
- [x] 2.2 Add a test on a narrow viewport: the filters sheet applies a filter and the card list shows only the filtered people; the card "⋯" menu reaches the same handler as the table row action.
- [x] 2.3 Check the pilot in the browser at 360, 390, 559, 560 and 1280 px, with no horizontal page scroll. Adjust the shared components before replicating.

## 3. Remaining pages

- [x] 3.1 Roles (cards, filters, "⋯" actions).
- [x] 3.2 Mis pedidos and Revisión (cards without "⋯" where rows only open the detail; Revisión areas as swipeable tabs).
- [x] 3.3 Períodos (card opens the edit modal; "⋯" actions as in the table).
- [x] 3.4 Reserva de aulas: Mis solicitudes / Todas las solicitudes cards; sections become tabs on phones.
- [x] 3.5 Tareas (semáforo tint; Pendientes/Terminadas/Todas as tabs on phones, select kept on desktop) and Proyectos.
- [x] 3.6 Registros de auditoría and Dashboard del sistema.
- [x] 3.7 Detail pages (pedido, solicitud, tarea, proyecto, persona): move primary actions into `BarraAccionesFija` below 560 px; keep the back link pattern.

## 4. Centro de ayuda

- [x] 4.1 Hide the sidebar at 900 px or less (`ayuda.css`, `LayoutAyuda`, `BarraLateralAyuda`), move `BuscadorAyuda` to the top of the content at 44 px / 16 px, and fix the horizontal overflow at 360 and 390 px.
- [x] 4.2 Add the "En esta página" `details/summary` index on articles on phones (replaces the step tree), linking to the step anchors; captures keep using `VisorImagen`.
- [x] 4.3 Add a test: on a narrow viewport the article index jumps to a step and the sidebar is not rendered.

## 5. Docs and help content (AGENTS rule 9)

- [x] 5.1 Add a "Teléfono" section to the existing design specs under `docs/product/designs/` for affected features: `administracion-usuarios-docentes`, `administracion-roles-permisos`, `administracion-sistema`, `reserva-aulas`, `proyecto-docente` (Tareas/Proyectos), `centro-de-ayuda` and `bloqueo-pedido-duplicado`/Designaciones where it describes the pedidos grids. Cover cards, filter sheet, tabs, pagination and sticky actions.
- [x] 5.2 Update the Centro de ayuda articles that describe filters, tabs or tables: `designaciones/revisar-el-tablero`, `designaciones/seguir-el-estado-de-un-pedido`, `designaciones/gestionar-periodos`, `tareas/filtrar-y-ordenar-tareas`, `tareas/entender-el-listado-de-tareas`, `administracion/administrar-personas`, `administracion/administrar-roles-y-permisos`, `administracion/consultar-la-auditoria`, `administracion/leer-el-dashboard-del-sistema`, `primeros-pasos/usar-el-centro-de-ayuda`, `primeros-pasos/recorrer-la-pantalla`. Keep `contenido/integridad.test.ts` green.
- [x] 5.3 Note the phone-only pagination exception in `docs/` where the grids' behaviour is documented, if any page lists it.

## 6. Verification

- [x] 6.1 `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`.
- [x] 6.2 `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`.
- [x] 6.3 Manual pass in the browser at 360, 390, 559, 560, 768 and 1280 px over the ten pages and the help: no horizontal page scroll, targets of at least 44 px for primary controls, desktop unchanged. Record any check the environment cannot complete.
