# asistente-accesibilidad Specification

## Purpose

Defines the accessibility requirements of the assistant interface: a live region limited to messages, thresholded progress announcements, predictable focus management, and keyboard-operable, announced controls for rating, exporting and navigating conversations.

## Requirements

### Requirement: La región viva contiene sólo los mensajes

El sistema SHALL acotar la región viva a la lista de mensajes, y MUST NOT incluir en ella la línea de métricas ni el resto de la vista.

#### Scenario: Las métricas quedan fuera de la región viva

- **GIVEN** la vista del asistente con una respuesta
- **WHEN** se inspecciona la región viva
- **THEN** la línea de métricas no está adentro

#### Scenario: La región de mensajes tiene rol de registro

- **GIVEN** la vista del asistente
- **WHEN** se inspecciona la lista de mensajes
- **THEN** tiene el rol de registro de conversación

### Requirement: El estado de proceso se anuncia con umbral

El sistema SHALL anunciar a lectores de pantalla que está procesando, y SHALL mostrar el indicador recién después de un umbral de tiempo.

#### Scenario: Una respuesta rápida no muestra indicador

- **GIVEN** un turno que resuelve antes del umbral
- **WHEN** el usuario espera
- **THEN** no aparece ningún indicador de proceso

#### Scenario: Una respuesta lenta sí lo muestra

- **GIVEN** un turno que tarda más que el umbral
- **WHEN** el usuario espera
- **THEN** aparece el indicador, anunciado como estado

#### Scenario: No hay etapas simuladas

- **GIVEN** un turno en curso
- **WHEN** se observa el indicador
- **THEN** muestra un solo estado honesto y no una secuencia de pasos inventada

### Requirement: El foco vuelve al campo de entrada

El sistema SHALL dejar el foco en el campo de entrada cuando llega la respuesta.

#### Scenario: Al responder, se puede seguir escribiendo

- **GIVEN** un turno enviado
- **WHEN** llega la respuesta
- **THEN** el foco está en el campo de entrada

#### Scenario: Elegir una opción también devuelve el foco

- **GIVEN** un turno que necesita aclaración
- **WHEN** el usuario elige una opción y llega la respuesta
- **THEN** el foco está en el campo de entrada

### Requirement: The thumbs control is keyboard-operable and states its pressed state

The system SHALL make the thumbs-up and thumbs-down controls reachable and
operable by keyboard alone, and SHALL expose each control's pressed state
via `aria-pressed`.

#### Scenario: A thumbs control is reachable by Tab

- **GIVEN** an answered turn with a rating control
- **WHEN** the user tabs through the page
- **THEN** both thumbs controls receive focus in sequence

#### Scenario: A thumbs control activates with the keyboard

- **GIVEN** a focused thumbs control
- **WHEN** the user presses Enter or Space
- **THEN** the vote is submitted the same way a pointer click would submit it

#### Scenario: The active vote is exposed to assistive technology

- **GIVEN** a turn rated thumbs-up
- **WHEN** the thumbs-up control is inspected
- **THEN** its `aria-pressed` is `true`, and the thumbs-down control's `aria-pressed` is `false`

### Requirement: A submitted vote is announced without moving focus

The system SHALL announce a submitted vote's confirmation through a live
region, and MUST NOT move keyboard focus away from the thumbs control when
announcing it.

#### Scenario: Voting announces confirmation

- **GIVEN** a user who submits a vote
- **WHEN** the vote is recorded
- **THEN** a live region announces that the vote was recorded

#### Scenario: Voting keeps focus on the thumbs control

- **GIVEN** a user who submits a vote via keyboard
- **WHEN** the vote is recorded
- **THEN** focus remains on the thumbs control that was activated

### Requirement: The export action is keyboard-operable and announces its outcome

The system SHALL make the CSV export action reachable and operable by
keyboard alone, and SHALL announce, through a live region, whether the
export succeeded.

#### Scenario: The export action activates with the keyboard

- **GIVEN** a focused export action
- **WHEN** the user presses Enter or Space
- **THEN** the export runs the same way a pointer click would trigger it

#### Scenario: A completed export is announced

- **GIVEN** a user who triggers the export
- **WHEN** the file has been generated
- **THEN** a live region announces that the export is ready

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

### Requirement: The re-run action is keyboard-operable and announces its outcome

The system SHALL make the "volver a consultar" action on a past turn
reachable and operable by keyboard, announcing its outcome via the live
region.

#### Scenario: The re-run action is keyboard-operable and announces completion

- **GIVEN** a past answered turn's "volver a consultar" action, reached by keyboard
- **WHEN** the actor activates it and the table finishes loading
- **THEN** completion is announced via the live region

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
