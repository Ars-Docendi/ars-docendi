## ADDED Requirements

### Requirement: Collapsing and expanding the rail keeps focus on the rail's toggle

The system SHALL operate the rail's collapse and expand controls with Enter or Space,
expose their state with `aria-expanded`, and after each toggle SHALL place focus on the
toggle control that is visible in the new state (expand when collapsed, collapse when
expanded), never on the document body.

#### Scenario: Collapsing by keyboard moves focus to the expand control

- **GIVEN** focus on «Colapsar conversaciones»
- **WHEN** the user presses Enter
- **THEN** the rail collapses and focus is on «Expandir conversaciones», whose `aria-expanded` is `false`

### Requirement: Undo notices are announced, keyboard-reachable and keep focus oriented

The system SHALL announce each archive, unarchive and delete outcome, including that it
can be undone for 10 seconds, through the existing conversation live region and MUST NOT
add a second live region for it. Because the row that held focus disappears, focus SHALL
move to the notice's «Deshacer»; activating «Deshacer» SHALL announce the restoration and
move focus to the restored row; if the notice expires while holding focus, focus SHALL
move to the conversation list. «Deshacer» SHALL be operable with Enter or Space.

#### Scenario: Deleting by keyboard lands on «Deshacer»

- **GIVEN** focus on «Eliminar» in a row's «⋮» menu
- **WHEN** the user presses Enter
- **THEN** the live region announces that the conversation was deleted and can be undone for 10 seconds, and focus is on «Deshacer»

#### Scenario: Undoing by keyboard returns focus to the row

- **GIVEN** focus on «Deshacer» after a deletion
- **WHEN** the user presses Enter
- **THEN** the restoration is announced and focus is on the restored conversation's row

#### Scenario: An expiring notice does not drop focus on the body

- **GIVEN** focus on «Deshacer»
- **WHEN** the 10 seconds elapse
- **THEN** focus is on the conversation list, not on the document body

### Requirement: Icon-only controls have accessible names and tooltips

The system SHALL give every icon-only control of the modal — rail toggles, «Nueva
conversación» when collapsed, «Historial», «⋮», the answer action bar, the question tools,
sort headers, «?» and close — a Spanish accessible name and a matching tooltip, and SHALL
expose toggle state (`aria-pressed` for votes and reason pills, `aria-expanded` for menus,
the rail and the «Archivadas» section).

#### Scenario: Every icon control is named

- **GIVEN** the modal with an answered turn
- **WHEN** the accessibility tree is inspected
- **THEN** every button without visible text has a non-empty Spanish accessible name

## MODIFIED Requirements

### Requirement: The conversation list is fully keyboard-operable

The system SHALL make every action of the rail — collapsing and expanding it, starting a
new conversation, searching, opening a conversation, its «⋮» menu with renaming,
archiving, unarchiving and deleting, expanding the «Archivadas» section, deleting all, and
«Deshacer» — reachable and operable using only the keyboard, in a logical tab order.
Escape SHALL close an open «⋮» menu and return focus to its trigger without closing the
modal.

#### Scenario: Every rail action is reachable by Tab

- **GIVEN** the rail expanded and reached via the keyboard
- **WHEN** the actor tabs through it
- **THEN** the collapse control, «Nueva conversación», search, each conversation, each «⋮», «Archivadas» and «Borrar todas» are reachable, in that order

#### Scenario: An action activates with Enter or Space

- **GIVEN** a focused action in the rail
- **WHEN** the actor presses Enter or Space
- **THEN** the action activates the same way a click would

#### Scenario: Escape closes a row menu without closing the modal

- **GIVEN** a row's «⋮» menu open
- **WHEN** the actor presses Escape
- **THEN** the menu closes, focus is on its «⋮» trigger and the modal stays open

### Requirement: Renaming and resuming are keyboard-operable and announce their result

The system SHALL let the actor rename a conversation inline and resume a conversation
entirely by keyboard — «Renombrar» from the «⋮» menu puts focus in the title field with
its text selected, Enter saves, Escape cancels — and SHALL return focus to the renamed
row. The system SHALL announce, via the live-region mechanism, when a rename is saved and
when a resume completes.

#### Scenario: Renaming by keyboard announces the saved title

- **GIVEN** the actor chooses «Renombrar» with the keyboard
- **WHEN** they type a new title and press Enter
- **THEN** the save is announced via the live region and focus is on the renamed row

#### Scenario: Cancelling a rename by keyboard keeps the title

- **GIVEN** a title field opened with «Renombrar»
- **WHEN** the actor presses Escape
- **THEN** the title is unchanged and focus is on that row

#### Scenario: Resuming by keyboard announces that the conversation is ready

- **GIVEN** the actor resumes a conversation using only the keyboard
- **WHEN** its past turns finish loading
- **THEN** readiness is announced via the live region and focus moves to the composer

## REMOVED Requirements

### Requirement: Destructive actions confirm and announce their outcome without disorienting focus

**Reason**: Deleting one conversation no longer has a confirmation step; the product decision (ARS-140, ARS-144) replaces it with a 10-second undo notice, which changes where focus must go.
**Migration**: See "Undo notices are announced, keyboard-reachable and keep focus oriented" in this capability; "Borrar todas" keeps its inline confirmation per `asistente-superficie-frontend`.
