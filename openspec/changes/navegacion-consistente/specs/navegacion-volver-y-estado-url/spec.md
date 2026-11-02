## ADDED Requirements

### Requirement: Validated Volver

Every detail and editor page SHALL offer "Volver a <destino>" as a real link styled as a secondary button. The target SHALL be `location.state.volverA` only when it is a relative path (optionally with a query) whose path is in the allowlist of that entity and which the user may open; otherwise it SHALL be the entity's fixed fallback. The app MUST NOT navigate with `-1`.

#### Scenario: Opened from a filtered list

- **GIVEN** the Revisión board filtered by a tab
- **WHEN** the user opens a pedido and presses "Volver a Revisión"
- **THEN** the board SHALL show the same tab and filters

#### Scenario: Deep link

- **GIVEN** a task opened directly by URL
- **WHEN** the user presses "Volver a Tareas"
- **THEN** the app SHALL go to the tasks list and MUST NOT leave the application

#### Scenario: Forged origin

- **GIVEN** `state.volverA` equal to an external URL, a protocol-relative URL or an unlisted path
- **THEN** the page SHALL use the fixed fallback

#### Scenario: Origin survives child tasks and editing

- **GIVEN** a task opened from the tasks list and then one of its child tasks
- **THEN** "Volver" on the child SHALL still go to the tasks list
- **AND** after saving a pedido edit (which replaces the entry) "Volver" on the detail SHALL still go to the original list

### Requirement: List state in the URL

Filters, sort and tabs SHALL live in the query string through one shared hook. Default values SHALL be omitted. Text filters and sort SHALL replace the history entry; tab changes SHALL push one. When two tables share a page their keys SHALL be prefixed (`mias.`, `todas.`). Reloading SHALL restore the state.

#### Scenario: Back restores filters

- **GIVEN** a list with a text filter and sort applied
- **WHEN** the user opens a row and goes back
- **THEN** the filter and sort SHALL still be applied

### Requirement: Scroll restoration

The `.adoc-main` scroll position SHALL be saved per history entry, restored on back/forward and on "Volver" once the data has rendered, reset to the top on a new page and kept on a query-only replace.

#### Scenario: Back to a long list

- **GIVEN** a list scrolled down whose row was opened
- **WHEN** the user presses "Volver" or the browser back button
- **THEN** the list SHALL return to the previous scroll position
