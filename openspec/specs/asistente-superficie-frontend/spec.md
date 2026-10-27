# asistente-superficie-frontend Specification

## Purpose

Defines the assistant's frontend surface: access follows the real permission rather than the role, the four answer states are distinguishable, sensitive columns render as a table, internal labels never reach the interface, and answers can be rated with an optional reason.

## Requirements

### Requirement: El acceso se decide por el permiso real y no por el rol

El sistema SHALL decidir la visibilidad del asistente consultando al backend, y MUST NOT decidirla con una lista de roles del frontend.

#### Scenario: Sin permiso no se ve el lanzador

- **GIVEN** un usuario cuyo rol no tiene el permiso del asistente
- **WHEN** mira la barra superior
- **THEN** no encuentra el lanzador

#### Scenario: Con permiso se ve el lanzador

- **GIVEN** un usuario cuyo rol tiene el permiso
- **WHEN** mira la barra superior
- **THEN** encuentra el lanzador habilitado

#### Scenario: El botón deshabilitado desaparece

- **GIVEN** la barra superior
- **WHEN** se la inspecciona
- **THEN** no queda ningún botón de ayuda deshabilitado con leyenda de «próximamente»

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

### Requirement: Las columnas sensibles se muestran como tabla

El sistema SHALL renderizar las filas devueltas cuando el resultado trae columnas, con los valores reales.

#### Scenario: Un resultado con filas se muestra en tabla

- **GIVEN** un turno respondido con columnas y filas
- **WHEN** el usuario lo ve
- **THEN** ve la tabla con los valores

#### Scenario: Un resultado truncado lo dice sin declarar cuántas filas faltan

- **GIVEN** un turno cuyo resultado se truncó
- **WHEN** el usuario lo ve
- **THEN** se le informa que hay más resultados y **no** se le dice cuántos

### Requirement: Ninguna etiqueta interna llega a la interfaz

El sistema MUST NOT mostrar identificadores internos, nombres de excepción ni mensajes crudos de transporte.

#### Scenario: Un error de transporte se muestra comprensible

- **GIVEN** un pedido que falla por red
- **WHEN** el usuario lo ve
- **THEN** lee un mensaje en español que no contiene códigos de estado ni nombres técnicos

### Requirement: An answered turn shows a way to rate it

The system SHALL render a thumbs-up/thumbs-down control on a turn that
carries a feedback token, and MUST NOT render it on a turn that does not.

#### Scenario: An answered turn shows the rating control

- **GIVEN** a turn answered with a feedback token
- **WHEN** the user views it
- **THEN** the thumbs control is visible next to that turn

#### Scenario: A rejection, clarification, or degraded turn shows no rating control

- **GIVEN** a turn that ended without a feedback token
- **WHEN** the user views it
- **THEN** no thumbs control is shown for that turn

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

### Requirement: A submitted vote is visibly confirmed and remains changeable

The system SHALL show the user which vote is currently recorded for a turn,
and SHALL allow changing it.

#### Scenario: The recorded vote is visibly marked

- **GIVEN** a turn already rated thumbs-up
- **WHEN** the user views it again
- **THEN** the thumbs-up control shows as the active choice

#### Scenario: The user can change their vote

- **GIVEN** a turn already rated thumbs-up
- **WHEN** the user picks thumbs-down instead
- **THEN** the turn now shows thumbs-down as the active choice

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

### Requirement: A past answered turn offers a "volver a consultar" action

The system SHALL offer, on a past answered turn viewed from the actor's own
history, an action to re-run its stored query and show the resulting table.

#### Scenario: The action is offered only on an answered turn

- **GIVEN** a past turn that ended answered
- **WHEN** the actor views it
- **THEN** a "volver a consultar" action is available

#### Scenario: The action is absent on a turn with nothing to re-run

- **GIVEN** a past turn that ended in clarification, rejection, or degraded service
- **WHEN** the actor views it
- **THEN** no "volver a consultar" action is offered

### Requirement: A permission-gated screen lets support read another user's history

The system SHALL present a support-history screen, reachable only by an
actor holding the dedicated support-history permission, that lets them
select a subject, supply a reason, and read that subject's conversations.
The system MUST NOT show this screen, or a way to reach it, to an actor
lacking that permission.

#### Scenario: The screen is unreachable without the permission

- **GIVEN** an actor without the support-history permission
- **WHEN** they navigate the application
- **THEN** no screen or link offers access to another user's history

#### Scenario: The screen requires a reason before showing another user's history

- **GIVEN** an actor with the support-history permission, on the support-history screen
- **WHEN** they select a subject but have not entered a reason
- **THEN** that subject's history is not shown until a reason is entered

#### Scenario: The screen offers no re-execution or export of another user's query results

- **GIVEN** an actor with the support-history permission viewing a subject's answered turn
- **WHEN** they look for a way to re-run it or see its result rows
- **THEN** no such action or data is present on the screen

### Requirement: Cada respuesta se presenta en un bloque propio

El cliente SHALL mostrar todo lo que responde a una pregunta —texto, tabla, ficha, opciones, avisos de estado, procedencia y barra de acciones— dentro de un bloque con fondo elevado, borde y radio del tema, separado de la burbuja de la pregunta.

El bloque MUST usar sólo tokens del tema y verse igual en los dos montajes.

#### Scenario: La respuesta se distingue de la pregunta

- **GIVEN** una conversación con un turno respondido
- **WHEN** el usuario mira el hilo
- **THEN** la pregunta se ve en su burbuja y la respuesta completa dentro de un bloque con borde

#### Scenario: El estado «Consultando…» ocupa el lugar del bloque

- **GIVEN** un turno en vuelo
- **WHEN** pasa el umbral de espera
- **THEN** «Consultando…» se muestra dentro del bloque de esa respuesta

### Requirement: El texto muestra como chips los elementos vinculados

El cliente SHALL mostrar cada posición señalada en el texto como un enlace en línea con aspecto de chip, con un nombre accesible que diga qué abre, y SHALL dejar el resto del texto sin cambios.

Seguir el chip SHALL comportarse como seguir el vínculo de una celda: cierra el asistente, navega al detalle y conserva la conversación.

Cuando la respuesta tiene elemento principal, el cliente MUST NOT mostrar chips en el texto: el enlace lo lleva la ficha.

#### Scenario: Un trámite nombrado en el texto se abre desde el chip

- **GIVEN** una respuesta de varias filas cuyo texto nombra un trámite vinculado
- **WHEN** el usuario sigue el chip de ese trámite
- **THEN** el asistente se cierra y la aplicación navega al detalle del trámite

#### Scenario: Con ficha no hay chips

- **GIVEN** una respuesta con elemento principal cuyo texto nombra ese mismo trámite
- **WHEN** se muestra la respuesta
- **THEN** el texto no tiene chips y la ficha ofrece «Abrir pedido»

### Requirement: Una respuesta con elemento principal lo muestra en una ficha

Cuando la respuesta tiene elemento principal de un tipo que el cliente conoce, el cliente SHALL mostrar una ficha con el tipo, el identificador legible, el estado y los datos clave del elemento, y un botón que abre su detalle.

Con ficha, el cliente MUST NOT repetir la tabla de una fila debajo: la tabla SHALL seguir disponible con «Ampliar tabla» de la barra de acciones.

Para un tipo que el cliente no conoce, el cliente SHALL mostrar la respuesta como si no tuviera elemento principal.

#### Scenario: Un pedido se muestra en su ficha

- **GIVEN** una respuesta cuyo principal es el trámite 2026-9005 con su ficha
- **WHEN** se muestra la respuesta
- **THEN** se ve la ficha del pedido con su estado y el botón «Abrir pedido»
- **AND** la tabla no se muestra debajo y se puede abrir con «Ampliar tabla»

#### Scenario: Abrir el pedido desde la ficha

- **GIVEN** una ficha de pedido
- **WHEN** el usuario elige «Abrir pedido»
- **THEN** el asistente se cierra y la aplicación navega al detalle de ese pedido

### Requirement: La respuesta dice de dónde salen sus datos

Cuando la respuesta nombra áreas de datos, el cliente SHALL mostrar al pie del bloque una línea «Datos de …» con el nombre de cada área para el usuario.

El cliente MUST NOT mostrar cantidades de filas, la consulta ni ninguna otra referencia a cómo se obtuvo el dato en esa línea.

#### Scenario: El pie nombra el área

- **GIVEN** una respuesta que nombra el área `designaciones`
- **WHEN** se muestra la respuesta
- **THEN** el pie dice «Datos de Designaciones»

### Requirement: La última respuesta se puede repetir

El cliente SHALL ofrecer «Repetir consulta» debajo de la última pregunta cuando su turno terminó con una respuesta, no hay un turno en vuelo y el asistente no está bloqueado.

Repetir SHALL reenviar exactamente la misma pregunta, con sus menciones, por el mismo camino que «Editar y reenviar»: reemplaza la última pregunta y su respuesta, y es un turno nuevo para el cupo.

#### Scenario: Repetir la última consulta

- **GIVEN** una conversación cuyo último turno está respondido
- **WHEN** el usuario elige «Repetir consulta»
- **THEN** la misma pregunta se envía otra vez
- **AND** la nueva respuesta reemplaza a la anterior en el hilo

#### Scenario: Sólo el último turno se repite

- **GIVEN** una conversación con dos turnos respondidos
- **WHEN** el usuario mira el primero
- **THEN** no se ofrece «Repetir consulta» en él

#### Scenario: Sin cupo no se ofrece repetir

- **GIVEN** un actor que alcanzó su límite de consultas
- **WHEN** mira el último turno
- **THEN** no se ofrece «Repetir consulta»

### Requirement: Después de seguir un vínculo se puede volver a la conversación

Cuando el asistente se cierra porque el usuario siguió un vínculo de una respuesta, el cliente SHALL mostrar en la pantalla de destino un botón flotante «Volver a la conversación» que reabre el asistente en el mismo hilo.

El botón SHALL desaparecer al reabrir el asistente o al navegar a otra pantalla, y MUST NOT aparecer cuando el asistente se cerró por otro motivo.

#### Scenario: Volver desde el detalle

- **GIVEN** un usuario que siguió un vínculo desde una respuesta
- **WHEN** llega al detalle
- **THEN** ve «Volver a la conversación»
- **AND** al elegirlo el asistente se abre con la conversación completa

#### Scenario: Cerrar el asistente no ofrece volver

- **GIVEN** un asistente abierto con una conversación
- **WHEN** el usuario lo cierra con «×»
- **THEN** no aparece «Volver a la conversación»

#### Scenario: Seguir navegando lo quita

- **GIVEN** el botón «Volver a la conversación» visible en el detalle
- **WHEN** el usuario navega a otra pantalla
- **THEN** el botón ya no se muestra

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
