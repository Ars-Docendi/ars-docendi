# notificaciones-internas Specification

## Purpose

Definir las notificaciones internas por destinatario: emisión desde los módulos, visibilidad, contador y marcado como leídas.

## Requirements

### Requirement: One notification per recipient user

The system SHALL store one notification row per recipient user, holding the recipient, the target profile (`rolDestino`, nullable), the event type, the entity type and id, an immutable title and message built by the emitting module, a free `contexto` document, the read instant and the creation instant. Notifications MUST NOT be deleted. The text snapshot MUST NOT change when the source entity changes later.

#### Scenario: Several Jefes of one materia

- **GIVEN** two active Jefes de Cátedra of the same materia
- **WHEN** an event addressed to the Jefes of that materia is emitted
- **THEN** each of them SHALL get their own notification
- **AND** reading one SHALL NOT mark the other as read

### Requirement: Visibility is scoped to the session user and active profile

Every read and write of notifications MUST be scoped to the authenticated user. A notification with a `rolDestino` MUST be visible only while the user operates with that role; a notification without `rolDestino` (Tareas, or old requests with unknown role) MUST be visible under any profile. All endpoints MUST require an authenticated session and answer 401 otherwise.

#### Scenario: Another profile of the same user

- **GIVEN** a user with the roles `docente` and `jefe_catedra` and a notice addressed to `jefe_catedra`
- **WHEN** the user operates as `docente`
- **THEN** the notice SHALL NOT be listed, counted or readable
- **AND** marking it as read SHALL answer 404

#### Scenario: Another user's notice

- **GIVEN** a notice addressed to user A
- **WHEN** user B marks it as read
- **THEN** the response SHALL be 404 and the notice SHALL stay unread

### Requirement: Unread count

`GET /api/notificaciones/no-leidas/cantidad` SHALL answer `{ cantidad }` with the number of unread notifications visible to the session user and active profile.

#### Scenario: Count follows the profile

- **GIVEN** 2 unread notices for `docente` and 3 for `jefe_catedra` of the same user
- **WHEN** the user operates as `jefe_catedra`
- **THEN** the count SHALL be 3

### Requirement: Paginated list

`GET /api/notificaciones?pagina&tamanio&estado` SHALL answer `{ datos, total, pagina, tamanio, totalPaginas }` ordered by creation instant descending and then id descending. `tamanio` defaults to 20 and MUST be between 1 and 100; `pagina` defaults to 1 and MUST be at least 1; `estado` is `NO_LEIDAS` or `LEIDAS` (omitted means both). Invalid values MUST answer 400 with Problem Details.

#### Scenario: Default page

- **GIVEN** 45 unread notices
- **WHEN** the list is requested with `estado=NO_LEIDAS`
- **THEN** it SHALL return 20 items, `total` 45 and `totalPaginas` 3

#### Scenario: Invalid size

- **WHEN** the list is requested with `tamanio=101`
- **THEN** the response SHALL be 400

### Requirement: Mark one as read

`PATCH /api/notificaciones/{id}/leida` SHALL set the read instant if it is empty and answer the notification. It MUST be idempotent: a second call keeps the original instant. For a missing id, another user's notice or another profile's notice the response MUST be 404 `notificacion-not-found`.

#### Scenario: Idempotence

- **GIVEN** a notice already read at instant T
- **WHEN** it is marked as read again
- **THEN** the response SHALL be 200 and `leidaEn` SHALL still be T

### Requirement: Mark several as read

`PATCH /api/notificaciones/leidas` SHALL accept exactly one of `{ ids }` (1 to 100 unique UUIDs) or `{ todas: true, hasta? }` (`hasta` an ISO 8601 date-time with time zone) and answer `{ cantidadActualizada }`, counting only notices that were unread, visible to the session and selected. Any other combination MUST answer 400 on the offending field. Ids not visible to the session MUST be ignored without revealing that they exist.

#### Scenario: Mark all up to an instant

- **GIVEN** unread notices created before and after instant T
- **WHEN** `{ todas: true, hasta: T }` is sent
- **THEN** only the notices created at or before T SHALL be marked and counted

#### Scenario: Both ids and todas

- **WHEN** `{ ids: [...], todas: true }` is sent
- **THEN** the response SHALL be 400 with "Enviá ids o todas: true, pero no ambos a la vez"

#### Scenario: Selection messages

- **THEN** the other 400 messages SHALL be "Enviá ids o todas: true", "hasta solo se puede usar con todas: true", "ids debe tener al menos 1 elemento", "ids no puede tener más de 100 elementos", "ids no puede tener elementos repetidos", "Cada id debe ser un UUID", "todas solo acepta el valor true" and "hasta debe ser una fecha ISO 8601 con hora y zona horaria (ej. 2026-10-15T12:00:00.000Z)"

### Requirement: Emission is best effort and after the commit

Emitting modules MUST emit only after their business transaction committed. A failure to resolve recipients or write notifications MUST NOT undo or alter the business action; it is logged without personal data and the notice is lost. The emission MUST NOT reuse the database context or transaction of the caller.

#### Scenario: Notifier failure

- **GIVEN** a notifier that throws
- **WHEN** an administrative user assigns a room
- **THEN** the request SHALL be approved and answer 200 with the room assigned

#### Scenario: Recipient hygiene

- **THEN** the actor is never notified of their own action, nobody is notified twice for one event, inactive users are skipped, and a persona without usuario is skipped and logged

#### Scenario: Actor exclusion

- **GIVEN** a Secretaría user who is also a recipient of the event
- **WHEN** that same user performs the action
- **THEN** no notification SHALL be created for them

### Requirement: Aulas events

The system SHALL notify the owner teacher, with `rolDestino` equal to the request's `rol_solicitante` (NULL meaning any profile), when a room is assigned ("Aula asignada"), when an approved request's room changes to a different one ("Aula actualizada"), when the request is rejected ("Solicitud rechazada") and when an approved reservation is cancelled ("Reserva cancelada").

#### Scenario: Reason and released room

- **THEN** the rejection notice SHALL carry the reason, and the cancellation notice SHALL carry the reason and the released room, read before it is cleared

#### Scenario: Teacher cancels

- **GIVEN** a teacher cancelling their pending request
- **THEN** every active user with the `administrativo` role and every one with `secretaria` SHALL get "Solicitud cancelada por el docente", with `rolDestino` equal to their role

#### Scenario: Room assigned

- **GIVEN** a pending request of a teacher who has a user
- **WHEN** an administrative user assigns "Aula 204"
- **THEN** the teacher SHALL get "Aula asignada" mentioning "Aula 204"

#### Scenario: Same room again

- **GIVEN** an approved request with "Aula 204"
- **WHEN** an administrative user assigns "Aula 204" again
- **THEN** no notification SHALL be created

### Requirement: Designaciones events

After a transition committed, the system SHALL notify with `rolDestino` equal to the role that must act: accept to the next review stage (Secretaría or Decanato users, "Pedido para revisar"); reject to the Jefes de Cátedra of the request's materia ("Pedido rechazado"); return to the owner role of the stage it was returned from, in the request's scope ("Pedido devuelto").

#### Scenario: Accept into the batch

- **WHEN** Decanato accepts a request and it becomes `en_lote`
- **THEN** no notification SHALL be created

#### Scenario: Return from Secretaría

- **GIVEN** a request in review at Secretaría
- **WHEN** Secretaría returns it
- **THEN** the Coordinadores of the request's carrera SHALL get "Pedido devuelto" with `rolDestino` `coordinador_carrera`

### Requirement: Tareas events

The system SHALL notify, with no `rolDestino`: the new responsable when a task is created or reassigned ("Tarea asignada"); the responsable and the creator, excluding the actor, when the state changes ("Cambio de estado en la tarea") or a comment is added ("Nuevo comentario en la tarea").

#### Scenario: Reassignment

- **GIVEN** a task whose responsable changes to user B
- **WHEN** the creator saves the change
- **THEN** user B SHALL get "Tarea asignada"
