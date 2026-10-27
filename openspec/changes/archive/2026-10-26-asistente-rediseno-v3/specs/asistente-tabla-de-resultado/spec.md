## Purpose

Lets a user reorder an answer's result table by any column and read it in a full-modal
expanded view, entirely in the browser and only over the values already displayed, so a
long or wide result can be scanned without re-asking.

## ADDED Requirements

### Requirement: Clicking a column header sorts the table by that column

The system SHALL let the user sort a rendered result table by any column by activating
its header. The first activation SHALL sort ascending; activating the currently sorted
column SHALL toggle between ascending and descending; activating another column SHALL
sort that column ascending. The sort SHALL be stable (rows with equal keys keep their
original relative order), SHALL happen entirely in the browser, and MUST NOT send any
request. An unsorted header SHALL show the neutral «⇅» affordance on hover and focus; the
sorted header SHALL show «↑» or «↓».

#### Scenario: First activation sorts ascending

- **GIVEN** a result table in its original order
- **WHEN** the user activates the «Docente» header
- **THEN** the rows are ordered by «Docente» ascending and the header shows «↑»

#### Scenario: Activating the sorted column toggles the direction

- **GIVEN** a table sorted by «Docente» ascending
- **WHEN** the user activates the «Docente» header again
- **THEN** the rows are ordered by «Docente» descending and the header shows «↓»

#### Scenario: Sorting sends nothing to the backend

- **GIVEN** a result table
- **WHEN** the user sorts it by any column
- **THEN** no request is sent to any assistant endpoint

### Requirement: Sorting respects the column's type and never uses a value that is not displayed

The system SHALL compare numbers numerically, ISO dates and timestamps chronologically,
and any other value as Spanish text (case- and accent-insensitive, with embedded numbers
compared numerically). A column SHALL be treated as numeric or date only when every
non-empty cell in it is of that type. Empty cells SHALL sort last in both directions. The
sort key SHALL be the value exactly as displayed; a masked cell SHALL sort by its masked
representation, and the system MUST NOT use, request, or infer any underlying value it
does not display.

#### Scenario: Numbers sort numerically, not lexically

- **GIVEN** a numeric column with the values 9, 10 and 100
- **WHEN** the user sorts it ascending
- **THEN** the order is 9, 10, 100

#### Scenario: Dates sort chronologically

- **GIVEN** a column whose cells are ISO dates "2026-03-01", "2025-12-15" and "2026-01-10"
- **WHEN** the user sorts it ascending
- **THEN** the order is "2025-12-15", "2026-01-10", "2026-03-01"

#### Scenario: Empty cells go last in both directions

- **GIVEN** a column with some empty cells
- **WHEN** the user sorts it ascending and then descending
- **THEN** the empty cells are at the bottom both times

#### Scenario: A masked cell sorts by what is shown

- **GIVEN** a sensitive column whose cells are displayed masked
- **WHEN** the user sorts by that column
- **THEN** the order follows the masked representations and no underlying value is used

### Requirement: Sorting keeps each cell's link to its own row

The system SHALL keep every cell link attached to the row it identified before sorting,
so a link always opens the record of the row it is displayed in.

#### Scenario: A trámite link follows its row

- **GIVEN** a table whose first row's «Trámite» cell links to trámite 2026-8841
- **WHEN** the user sorts the table so that row moves to the last position
- **THEN** the «Trámite» cell in the last row still links to trámite 2026-8841

### Requirement: Sorting is operable by keyboard and exposed to assistive technology

The system SHALL make each sortable header a keyboard-focusable control activated with
Enter or Space, with an accessible name that states the column and the action. The
sorted column's header cell SHALL expose `aria-sort` with `ascending` or `descending`;
unsorted headers MUST NOT claim a sort direction. A sort change SHALL be announced
through the existing conversation live region without moving focus.

#### Scenario: A header sorts with the keyboard

- **GIVEN** focus on the «Cargo» header control
- **WHEN** the user presses Enter
- **THEN** the table is sorted by «Cargo» ascending and focus stays on that header

#### Scenario: The sorted header exposes its direction

- **GIVEN** a table sorted by «Cargo» descending
- **WHEN** the header cells are inspected
- **THEN** the «Cargo» header cell has `aria-sort="descending"` and no other header cell has an `aria-sort` direction

#### Scenario: The new order is announced

- **GIVEN** a table in its original order
- **WHEN** the user sorts it by «Cargo»
- **THEN** the live region announces the column and direction, and focus does not move

### Requirement: «Ampliar tabla» shows the table over the whole modal

The system SHALL offer «Ampliar tabla» for an answer with at least one row. Activating it
SHALL show, covering the entire modal, a view whose header reads «Tabla ampliada» above
the turn's question as title, with the actions «Copiar tabla» (only when the clipboard is
available), «Exportar a CSV» and «Contraer», followed by the same table with the same
columns, sensitive-column markers and legend, cell links, and truncation notice. The
expanded view and the inline table SHALL share one sort state. «Contraer» and Escape SHALL
close only the expanded view, never the modal, and focus SHALL return to the control that
opened it.

#### Scenario: The expanded view is titled with the question

- **GIVEN** an answer to "¿Qué docentes están designados en Algoritmos y Estructuras de Datos?" with a table
- **WHEN** the user activates «Ampliar tabla»
- **THEN** a view covering the modal shows «Tabla ampliada» and that question as its title, with «Copiar tabla», «Exportar a CSV» and «Contraer»

#### Scenario: Sort state is shared between the two views

- **GIVEN** an inline table sorted by «Docente» descending
- **WHEN** the user expands it, sorts by «Cargo», and collapses it
- **THEN** both views show the table sorted by «Cargo» ascending

#### Scenario: Escape collapses the expanded view and keeps the modal open

- **GIVEN** the expanded view open
- **WHEN** the user presses Escape
- **THEN** the expanded view closes, the modal stays open, and focus returns to the «Ampliar tabla» control that opened it

#### Scenario: The truncation notice is kept

- **GIVEN** a truncated result expanded
- **WHEN** the user reads the expanded view
- **THEN** it shows «Hay más resultados de los que se muestran. Acotá la pregunta para verlos.» without any row count

#### Scenario: An empty result offers no expanded view

- **GIVEN** an answer with zero rows
- **WHEN** the user looks at its actions
- **THEN** there is no «Ampliar tabla»

### Requirement: «Copiar tabla» in the expanded view copies the displayed order

The system SHALL copy the table as tab-separated text with a header row, with its rows in
the order currently displayed, and SHALL confirm the copy the same way the other copy
actions do.

#### Scenario: A sorted table is copied sorted

- **GIVEN** the expanded view sorted by «Docente» ascending
- **WHEN** the user activates «Copiar tabla»
- **THEN** the clipboard holds a header row followed by the rows ordered by «Docente» ascending
