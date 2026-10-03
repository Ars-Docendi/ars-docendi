## ADDED Requirements

### Requirement: The assistant lives only in the top-bar modal

The system SHALL expose the assistant only through the modal opened from the top-bar
launcher, and MUST NOT expose a dedicated assistant page or a navigation entry for one.
A request to the former `/asistente` address SHALL redirect to the application home and
open the modal for a user with access; for a user without access it SHALL land on the
home without opening anything. The redirect marker SHALL be removed from the address
once consumed. The support-history and administration screens SHALL keep their own
routes and permissions.

#### Scenario: An old link opens the modal on the home

- **GIVEN** a user with access to the assistant
- **WHEN** they navigate to `/asistente`
- **THEN** they land on the application home with the assistant modal open, and the address no longer carries the marker

#### Scenario: An old link does nothing for a user without access

- **GIVEN** a user without the assistant permission
- **WHEN** they navigate to `/asistente`
- **THEN** they land on the application home and no assistant surface is shown

#### Scenario: Support and administration keep their routes

- **GIVEN** a user with the support-history permission
- **WHEN** they navigate to `/asistente/soporte-historial`
- **THEN** the support-history screen is shown

#### Scenario: No assistant page remains

- **GIVEN** the application's routes and navigation
- **WHEN** they are inspected
- **THEN** no route renders an assistant page and no navigation item points to one

### Requirement: The modal is laid out as a conversation rail beside the conversation

The system SHALL render the modal, on desktop widths, at up to 1100 × 728 px as two
columns: a conversation rail on the left and the conversation on the right. The rail
SHALL be 268 px wide expanded and 60 px collapsed, switching with a 200 ms transition
(none under reduced motion). Expanded, it SHALL show «Nueva conversación», a collapse
control, a search field, the conversation list grouped by relative date, the «Archivadas»
section and the undo notice. Collapsed, it SHALL show only an expand control, a «Nueva
conversación» icon control and a history icon control that expands the rail. The
collapsed or expanded state SHALL be remembered per user in the browser as a preference,
defaulting to expanded, and the modal SHALL work normally when that preference cannot be
read or written.

#### Scenario: The rail collapses to icons and back

- **GIVEN** the modal open with the rail expanded
- **WHEN** the user activates «Colapsar conversaciones»
- **THEN** the rail is 60 px wide showing only «Expandir conversaciones», «Nueva conversación» and «Historial» icon controls

#### Scenario: The preference survives reopening, per user

- **GIVEN** a user who collapsed the rail
- **WHEN** they close and reopen the modal, or reload the page
- **THEN** the rail opens collapsed for that user, and another user on the same browser still gets it expanded

#### Scenario: Blocked storage does not break the modal

- **GIVEN** a browser that throws on storage access
- **WHEN** the user opens the modal and toggles the rail
- **THEN** the rail toggles normally and the modal keeps working

### Requirement: The header names the active conversation

The system SHALL render a 56 px header in the conversation column showing the active
conversation's title — «Asistente» while the welcome screen is shown — followed by the
«?» help control and the close control. The active conversation SHALL be the one resumed
from the rail or the one the current live conversation was persisted as; its row in the
rail SHALL be highlighted with the accent and marked as current.

#### Scenario: A resumed conversation titles the header

- **GIVEN** a conversation titled "Docentes designados en Algoritmos" in the rail
- **WHEN** the user opens it
- **THEN** the header reads "Docentes designados en Algoritmos" and that row is highlighted and marked as current

#### Scenario: A new live conversation appears and is highlighted after its first answer

- **GIVEN** the welcome screen
- **WHEN** the user asks a question and its answer arrives
- **THEN** the rail lists the new conversation at the top, highlighted, and the header shows its title

#### Scenario: The welcome screen is titled «Asistente»

- **GIVEN** no turns in the conversation
- **WHEN** the user looks at the header
- **THEN** it reads «Asistente»

### Requirement: Archived conversations live in a collapsible section of the rail

The system SHALL list archived conversations apart from the rest, in a collapsible
«Archivadas» section at the bottom of the rail showing their count, collapsed by default
and hidden when there are none. While a search term is present, the rail SHALL show the
matching conversations as a single list in which archived ones are marked «Archivada»,
and the «Archivadas» section SHALL be hidden.

#### Scenario: The section shows the count and expands

- **GIVEN** two archived conversations
- **WHEN** the user looks at the bottom of the rail and activates «Archivadas»
- **THEN** it reads «Archivadas 2» and expands to list both

#### Scenario: Search results mark archived conversations

- **GIVEN** an archived conversation whose question matches "designaciones"
- **WHEN** the user searches for "designaciones"
- **THEN** it appears among the results marked «Archivada»

### Requirement: Archive, unarchive and delete show an undo notice for 10 seconds

The system SHALL, after archiving, unarchiving or deleting a conversation, show a notice
at the bottom of the rail reading «Conversación archivada», «Conversación restaurada» or
«Conversación eliminada» (or «Conversaciones eliminadas» after deleting all) with a
«Deshacer» action, for 10 seconds. Only one notice SHALL exist at a time; a new action
replaces the previous notice. «Deshacer» SHALL revert the action; for a conversation that
was active, undoing SHALL resume it again. The notice SHALL stay reachable when the rail
is collapsed.

#### Scenario: Deleting shows the notice and the row disappears

- **GIVEN** a conversation in the rail
- **WHEN** the user deletes it from its «⋮» menu
- **THEN** the row disappears at once and «Conversación eliminada» with «Deshacer» is shown

#### Scenario: Undo within 10 seconds restores the conversation

- **GIVEN** a conversation deleted 5 seconds ago with its notice showing
- **WHEN** the user activates «Deshacer»
- **THEN** the conversation is back in the rail with its title and turns

#### Scenario: The notice disappears after 10 seconds

- **GIVEN** a notice shown after deleting a conversation
- **WHEN** 10 seconds pass
- **THEN** the notice is gone and the deletion can no longer be undone from the interface

#### Scenario: Archiving the active conversation clears the thread

- **GIVEN** the conversation currently open is archived from its «⋮» menu
- **WHEN** the action completes
- **THEN** the conversation column returns to the welcome screen and the notice offers «Deshacer», which resumes it again

### Requirement: Each answer carries an icon action bar

The system SHALL render under each answer a toolbar of icon controls, each with a
tooltip and an accessible name: «Copiar respuesta» (only when the clipboard is available),
«Ampliar tabla» and «Exportar a CSV» (only when the answer has at least one row), a
separator, and «Sirvió» / «No sirvió» (only when the turn carries a feedback token). The
toolbar SHALL be visible on the last turn and on any voted turn; on other turns it SHALL be
revealed on pointer hover and on keyboard focus within the turn, and its controls SHALL
always be reachable by Tab. The toolbar replaces the previous text buttons.

#### Scenario: An answer with a table shows the full bar

- **GIVEN** the last turn answered with rows and a feedback token, and a clipboard available
- **WHEN** the user looks under the answer
- **THEN** «Copiar respuesta», «Ampliar tabla», «Exportar a CSV», «Sirvió» and «No sirvió» are shown as icons with their names

#### Scenario: Keyboard focus reveals the bar on an older turn

- **GIVEN** an older, unvoted turn whose bar is hidden
- **WHEN** the user tabs into it
- **THEN** the bar becomes visible while focus is inside the turn

#### Scenario: A turn without rows has no table actions

- **GIVEN** an answer without rows
- **WHEN** the user looks at its bar
- **THEN** there is no «Ampliar tabla» and no «Exportar a CSV»

## MODIFIED Requirements

### Requirement: Los cuatro estados se renderizan distinguibles

El sistema SHALL renderizar de forma distinguible la respuesta, el rechazo, la aclaración y el servicio degradado, y MUST NOT renderizar chips de sugerencias en ninguno de ellos; los únicos ejemplos clicables del asistente son los de la pantalla de bienvenida.

#### Scenario: El degradado se muestra como estado y no como error

- **GIVEN** un turno que resolvió como servicio degradado
- **WHEN** el usuario lo ve
- **THEN** se le presenta como una situación temporal del asistente y no como un fallo de su pregunta

#### Scenario: La aclaración ofrece sus opciones para elegir

- **GIVEN** un turno que necesita aclaración
- **WHEN** el usuario lo ve
- **THEN** puede elegir una de las opciones sin volver a escribir la pregunta

#### Scenario: Un rechazo no ofrece sugerencias

- **GIVEN** un turno no contestable
- **WHEN** el usuario lo ve
- **THEN** lee el texto del rechazo y no hay ningún chip de sugerencia

#### Scenario: Una respuesta no ofrece sugerencias de seguimiento

- **GIVEN** un turno respondido
- **WHEN** el usuario lo ve
- **THEN** no hay ninguna sección de sugerencias

### Requirement: The user can see and open a list of their own past conversations

The system SHALL present, in the rail of the assistant modal, the actor's own past
conversations that are not archived and not pending deletion, grouped by relative date of
last activity (Hoy / Ayer / Últimos 7 días / Anteriores; empty groups hidden), each as a
one-line title with its full text available on hover. The system SHALL let the actor open
one to resume it, and SHALL keep the list current after every turn, rename, archive,
unarchive, delete and undo.

#### Scenario: The list is visible in the rail

- **GIVEN** the actor has conversations and opens the modal
- **WHEN** they look at the expanded rail
- **THEN** their conversations are listed, grouped by relative date

#### Scenario: Opening a listed conversation resumes it

- **GIVEN** a conversation in the rail
- **WHEN** the actor opens it
- **THEN** the conversation resumes with its past turns visible and ready for a follow-up

### Requirement: A conversation can be renamed, searched for, and deleted from the interface

The system SHALL let the actor, from the rail: rename a conversation inline — from
«Renombrar» in its «⋮» menu or by double-clicking its title — where Enter or leaving the
field saves and Escape cancels, and an empty title keeps the previous one; search their
own conversations by text; archive or unarchive a conversation from its «⋮» menu
(«Renombrar» / «Archivar» / «Eliminar» for active rows, «Desarchivar» / «Eliminar» for
archived rows); delete one conversation from its «⋮» menu without a confirmation step,
relying on the undo notice; and delete all of their conversations, including archived
ones, after an explicit inline confirmation, also followed by the undo notice.

#### Scenario: Renaming inline saves on Enter

- **GIVEN** a conversation in the rail
- **WHEN** the actor double-clicks its title, types a new one and presses Enter
- **THEN** the new title is saved and shown in the rail and, if active, in the header

#### Scenario: Escape cancels a rename

- **GIVEN** a title being edited inline
- **WHEN** the actor presses Escape
- **THEN** the previous title is kept and nothing is saved

#### Scenario: Searching narrows the list as the actor types

- **GIVEN** the actor's list of conversations
- **WHEN** they enter search text
- **THEN** only conversations matching that text remain visible, archived ones marked «Archivada»

#### Scenario: Deleting one conversation needs no confirmation and can be undone

- **GIVEN** a conversation in the rail
- **WHEN** the actor chooses «Eliminar» in its «⋮» menu
- **THEN** it disappears at once without a confirmation step, and the undo notice is shown

#### Scenario: Deleting all conversations asks for confirmation and can be undone

- **GIVEN** the actor's conversations, some archived
- **WHEN** they choose «Borrar todas»
- **THEN** they must confirm «¿Borrar TODAS tus conversaciones, incluidas las archivadas? Vas a poder deshacerlo durante 10 segundos.» before all of them disappear, and the undo notice is shown

### Requirement: A thumbs-down vote offers a reason before submitting

The system SHALL, when the user picks thumbs-down, open a panel «¿Qué falló? Opcional»
offering the fixed reasons as single-choice pills — «Datos incorrectos», «No entendió la
pregunta», «Faltan datos», «Otro» — with «Omitir» and «Enviar». «Enviar» SHALL submit the
thumbs-down with the chosen reason, or with none; «Omitir» SHALL submit the thumbs-down
with no reason. After submitting, the system SHALL show «Gracias. Tu comentario ayuda a
mejorar el asistente.» The system MUST NOT offer a free-text field.

#### Scenario: Picking thumbs-down surfaces the reason choices

- **GIVEN** a user about to rate a turn thumbs-down
- **WHEN** they pick «No sirvió»
- **THEN** they see «¿Qué falló? Opcional» with the four reasons before the vote is sent

#### Scenario: Skipping a reason still submits the vote

- **GIVEN** the reason panel open
- **WHEN** the user activates «Omitir»
- **THEN** the thumbs-down vote is sent with no reason and the thanks message is shown

#### Scenario: Only one reason can be chosen

- **GIVEN** the reason panel with «Datos incorrectos» chosen
- **WHEN** the user chooses «Faltan datos»
- **THEN** only «Faltan datos» is marked as chosen

#### Scenario: There is no free-text field

- **GIVEN** the reason panel open
- **WHEN** it is inspected
- **THEN** it contains no text input

### Requirement: The result table offers a CSV export action

The system SHALL offer «Exportar a CSV» in the answer's action bar and in the expanded
table view for a result with at least one row, and MUST NOT offer it for an empty result.

#### Scenario: A non-empty result offers export

- **GIVEN** a turn answered with at least one row
- **WHEN** the user looks at its action bar
- **THEN** «Exportar a CSV» is available there and in the expanded view

#### Scenario: An empty result offers no export

- **GIVEN** a turn answered with zero rows
- **WHEN** the user views it
- **THEN** no export action is shown

## REMOVED Requirements

### Requirement: El asistente tiene dos montajes

**Reason**: The dedicated `/asistente` page is removed by product decision (ARS-140, ARS-151); the assistant lives only in the top-bar modal.
**Migration**: See "The assistant lives only in the top-bar modal": old `/asistente` links redirect to the home with the modal open; the launcher requirements are unchanged.

### Requirement: Follow-up suggestions after a successful answer are presented like other suggestions

**Reason**: Suggestions are removed everywhere except the welcome screen (ARS-140, ARS-149); the backend no longer produces follow-up suggestions.
**Migration**: None for users; the welcome screen keeps its verified examples.
