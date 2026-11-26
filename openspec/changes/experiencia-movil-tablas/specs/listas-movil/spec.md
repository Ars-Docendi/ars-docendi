## ADDED Requirements

### Requirement: Accessible, scrollable table region

Every list table SHALL render its scroll container as a focusable region with an accessible name (`role="region"`, `tabindex="0"`, `aria-label`). When columns overflow horizontally, the first column MUST stay sticky and a shadow MUST mark the right edge while more content remains. Below 560 px the table MUST NOT have its own vertical scroll: the page scrolls. Where a height limit remains it SHALL use `dvh`. The scroll container SHALL set `scroll-padding` so a focused element is never hidden behind the sticky header or column.

#### Scenario: Overflowing columns on a phone

- **GIVEN** a 390 px viewport and a table wider than the screen
- **WHEN** the user opens the page
- **THEN** the first column stays visible while scrolling sideways
- **AND** a shadow on the right edge indicates more columns
- **AND** the page, not the table, scrolls vertically

#### Scenario: Keyboard focus is not obscured

- **GIVEN** a table region with a sticky header and first column
- **WHEN** the user tabs to a control in an off-screen cell
- **THEN** the region scrolls so the control is fully visible

### Requirement: Adjustable column widths on phones

Below 560 px, every table rendered in the shared table region SHALL offer, at the right edge of each header cell, a button labelled "Ajustar ancho de «<column>»" that changes that column's width: dragging it changes the width by the distance dragged, ArrowRight and ArrowLeft change it by 16 px, and a double tap or double click restores the width the column had before it was first adjusted. A column MUST NOT become narrower than 64 px. Until a handle is used the table SHALL look exactly as before; once widths are set, the columns MUST keep them and long values SHALL wrap inside their cell. Using a handle MUST NOT activate the header's own controls, such as sorting. The first column stays pinned and the page MUST NOT scroll sideways. Widths last while the table is shown; the inline result table and its expanded view share them. From 560 px no handle is shown.

#### Scenario: Widen a column by dragging

- **GIVEN** a seven-column assistant result on a 390 px phone
- **WHEN** the user drags the handle of «Código» 80 px to the right
- **THEN** that column is 80 px wider and the other columns keep their width
- **AND** the table is not sorted and the page does not scroll sideways

#### Scenario: Keyboard and restore

- **GIVEN** a result table on a phone
- **WHEN** the user focuses the handle of a column and presses ArrowRight twice
- **THEN** the column is 32 px wider
- **AND** a double tap on the same handle restores its previous width

#### Scenario: The expanded view keeps the widths

- **GIVEN** a result table whose column the user widened on a phone
- **WHEN** the user opens «Ampliar tabla»
- **THEN** the expanded table shows the same column widths

#### Scenario: No handles from 560 px

- **GIVEN** a 768 px viewport
- **WHEN** the Calidad tables or an assistant result render
- **THEN** no header cell shows a width handle

### Requirement: Card list below 560 px

Below 560 px, Personas, Roles, Mis pedidos, Revisión, Períodos, Reserva de aulas (Mis solicitudes and Todas las solicitudes), Tareas and Proyectos SHALL render their rows as a `<ul>` of cards instead of the table (the Sistema section, which replaced Registros de auditoría and Dashboard del sistema, follows its own requirement below). The card list MUST be a separate render path, not table elements restyled with `display: block`. Each card SHALL show: the existing row link as its title, stretched over the whole card; a monospace meta line; a secondary line; and the module's real status badges. The card list MUST show the same rows, in the same order, as the table for the same filters and sort. Opening a card SHALL navigate to the same destination as opening the row (Períodos opens its edit modal).

#### Scenario: Personas as cards on a phone

- **GIVEN** a 390 px viewport
- **WHEN** an administrator opens Personas
- **THEN** a list of cards is shown and no table is rendered
- **AND** tapping a card opens that person's detail

#### Scenario: Table kept from 560 px

- **GIVEN** a 600 px viewport
- **WHEN** the user opens Personas
- **THEN** the table is rendered and no card list is rendered

### Requirement: Row actions in a card menu

When the page has row actions, each card SHALL show a "⋯" button with a 44 px touch target that opens the actions menu with the same actions, labels and handlers as the table row, subject to the same permission conditions. When the page has no row actions, cards MUST NOT show the button.

#### Scenario: Action from the card menu

- **GIVEN** a Períodos card on a phone
- **WHEN** the user opens "⋯" and chooses an action
- **THEN** the same handler runs as from the table row

#### Scenario: Page without row actions

- **GIVEN** Revisión, whose rows only open the detail
- **WHEN** it renders as cards
- **THEN** no card shows a "⋯" button

### Requirement: Task cards keep the due-date semáforo

Task cards SHALL tint the card for "por vencer" and "vencida" with the same classification as the table row semáforo, and MUST NOT rely on colour alone: the due-date text and status badge SHALL still be shown.

#### Scenario: Overdue task card

- **GIVEN** an overdue task
- **WHEN** the Tareas page renders as cards
- **THEN** its card shows the "vencida" tint and the due date in text

### Requirement: Swipeable tabs on phones

Below 560 px, tab groups (Revisión areas, Aulas sections, Tareas Pendientes/Terminadas/Todas) SHALL render as a horizontally scrollable strip with edge shadows when more tabs exist off-screen. Selecting a tab SHALL scroll it into view. Tab counts MUST be preserved.

#### Scenario: Selecting an off-screen tab

- **GIVEN** a tab strip wider than the screen
- **WHEN** the user taps a partially hidden tab
- **THEN** it becomes active and is scrolled fully into view
- **AND** its count is still displayed

### Requirement: Paginated card lists

Card lists SHALL paginate client-side at 20 cards per page, with controls of at least 44 px, and MUST reset to page 1 when filters, search or sort change. Lists with 20 cards or fewer MUST NOT show pagination. The audit log is the exception: it keeps its server-side pagination at 50 records per page.

#### Scenario: Long list

- **GIVEN** 45 matching rows on a phone
- **WHEN** the user opens the page
- **THEN** 20 cards and pagination controls are shown
- **AND** going to page 3 shows the remaining 5

### Requirement: Primary actions in a sticky bar on detail pages

On detail pages below 560 px, primary actions SHALL be placed in a bar fixed to the bottom of the viewport that respects the device safe-area inset and does not cover the last content.

#### Scenario: Action reachable on a long detail

- **GIVEN** a long pedido detail on a phone
- **WHEN** the user scrolls to the middle of the page
- **THEN** the primary actions remain visible at the bottom

### Requirement: Sistema section on phones

Below 560 px the Sistema section SHALL fit the viewport width with no horizontal page scroll. Its tabs (Estado, Asistente, Modelo, Calidad, Auditoría) SHALL use the swipeable tab strip, keeping each tab's status dot. Estado SHALL stack component cards one per row and show each recent change with its summary above its time and action. The Auditoría tab SHALL keep its day-grouped list with each event on two lines (summary and context, then time and action), SHALL open the event detail directly under the tapped row instead of beside the list, and SHALL return focus to that row when the detail closes. Its server-side pagination (50 per page) MUST be kept, with controls of at least 44 px.

#### Scenario: Five tabs on a 390 px phone

- **GIVEN** an administrator with every Sistema permission on a 390 px viewport
- **WHEN** they open Sistema
- **THEN** the page does not scroll sideways
- **AND** the tab strip scrolls sideways and tapping «Auditoría» brings it fully into view

#### Scenario: Audit detail under its row

- **GIVEN** the Auditoría tab on a phone
- **WHEN** the user taps an event
- **THEN** its detail appears right below that event
- **AND** closing the detail returns focus to the event

### Requirement: Assistant administration on phones

Below 560 px the assistant usage, model and quality tabs of Sistema SHALL fit the viewport width. The usage list (per user and per role) SHALL render one card per row, in the same order as the table, carrying the same access and daily-quota controls with the same handlers and announcements; it paginates like the other card lists. «Respuestas con problemas» SHALL render one card per answer and keep its server-side pagination without client pagination. Small comparison tables («Por modelo», the chart's table view) SHALL stay tables inside the accessible scroll region with a sticky first column. KPI grids SHALL stack to at most two columns, with the monthly cap and the model server at full width. Segmented toggles (period, metric, problem type) SHALL span the width with targets of at least 44 px.

#### Scenario: Editing a daily quota from a card

- **GIVEN** the usage tab on a phone with two users
- **WHEN** the administrator edits the daily quota on a user's card and saves
- **THEN** no table is rendered
- **AND** the same save handler as the table row runs with that user's key

#### Scenario: Problem answers keep server paging

- **GIVEN** 40 answers with problems and a server page of 25
- **WHEN** the quality tab renders on a phone
- **THEN** 25 cards are shown with the server's «Anterior»/«Siguiente» controls and no card pagination
