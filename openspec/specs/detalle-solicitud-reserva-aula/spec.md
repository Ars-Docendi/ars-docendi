# detalle-solicitud-reserva-aula Specification

## Purpose

Defines how a room reservation request is opened on its own page and how an approved reservation is cancelled with a reason, so that requests have a stable address and a closed lifecycle.

## Requirements

### Requirement: Request detail visible to its owner or to whoever resolves requests

The system SHALL expose the detail of one request. It MUST be visible only to the person who created it (with `aulas.solicitar`) or to anyone with `aulas.aprobar`. For everyone else, including a role without Aulas permissions, the answer MUST be the same "not found" answer as for an id that does not exist, so that the existence of a request is never revealed. The response SHALL include the requester, the closing reasons and whether the request belongs to the caller.

#### Scenario: Owner opens her own request

- **GIVEN** a requester with `aulas.solicitar` who created a request
- **WHEN** she asks for its detail
- **THEN** she receives the request marked as her own, with the requester data

#### Scenario: Approver opens any request

- **GIVEN** a user with `aulas.aprobar`
- **WHEN** he asks for the detail of a request created by someone else
- **THEN** he receives it, not marked as his own

#### Scenario: Another requester or a role without Aulas permissions

- **GIVEN** a request created by someone else
- **WHEN** a user with `aulas.solicitar` only, or with no Aulas permission, asks for its detail
- **THEN** the answer is 404 `solicitud-not-found`, identical to the answer for an unknown id

### Requirement: Cancel an approved reservation with a reason

A user with `aulas.aprobar` SHALL be able to cancel a reservation in state `aprobada` by giving a reason. The request MUST become `cancelada`, the assigned classroom MUST be released and the reason MUST be stored and shown with the request. The reason MUST be accepted only on cancelled requests, also at the database level.

#### Scenario: Successful cancellation

- **GIVEN** an approved request with a classroom
- **WHEN** an approver cancels it with a reason
- **THEN** the request is `cancelada`, has no classroom and keeps the reason, which its owner sees in the detail

### Requirement: Cancellation is validated and authorized

Cancelling a reservation MUST be rejected with a validation error on `motivo` ("Debés indicar el motivo de la cancelación.") when the reason is blank, and with a conflict `solicitud-no-aprobada` ("Sólo se puede cancelar una reserva en estado Aprobada.") when the request is in any other state. A user without `aulas.aprobar` MUST be denied.

#### Scenario: Blank reason

- **GIVEN** an approved request
- **WHEN** an approver cancels it with a blank reason
- **THEN** the answer is 400 on field `motivo` and the request stays `aprobada`

#### Scenario: Not approved

- **GIVEN** a pending, rejected or already cancelled request
- **WHEN** an approver tries to cancel its reservation
- **THEN** the answer is 409 `solicitud-no-aprobada`

#### Scenario: Requester without approval permission

- **GIVEN** an approved request owned by a requester without `aulas.aprobar`
- **WHEN** the owner tries to cancel the reservation
- **THEN** the answer is 403 and the request stays `aprobada`

### Requirement: Requester role is recorded on creation

When a request is created the system SHALL store the active role of the creating session. Requests created before this change MAY have no role.

#### Scenario: Creation under a role

- **GIVEN** a user whose active session role is `docente`
- **WHEN** the user creates a request
- **THEN** the request stores `docente` as its requester role

### Requirement: Detail page with the actions of each state

The application SHALL provide the page `/aulas/solicitudes/:id`, reachable only with `aulas.solicitar` or `aulas.aprobar`, that shows the request data, the closing reason when there is one and the actions allowed to the viewer. It MUST offer "Volver a Reserva de aulas", which leads to `/aulas`. A request that cannot be loaded MUST show that it does not exist or is not accessible, with a way back.

#### Scenario: Request that cannot be loaded

- **GIVEN** an id that does not exist or that the viewer may not see
- **WHEN** the page is opened
- **THEN** it states that the request does not exist or is not accessible and offers the way back

### Requirement: Actions offered by state and permission

An approver MUST be offered, on a pending request, assigning a classroom and rejecting with a reason; on an approved one, updating the classroom and cancelling the reservation with a reason. The owner MUST be offered cancelling her own pending request. A closed request, or an approved one seen by a viewer who cannot resolve it, MUST show that no actions are available.

#### Scenario: Approver resolves a pending request

- **GIVEN** an approver on a pending request
- **WHEN** he assigns a classroom, or rejects with a reason
- **THEN** the backend applies the transition and the page shows the new state

#### Scenario: Approver cancels an approved reservation

- **GIVEN** an approver on an approved request
- **WHEN** he confirms "Cancelar reserva" with a reason
- **THEN** the request shows state Cancelada and the reason

#### Scenario: Owner on an approved request

- **GIVEN** a requester without `aulas.aprobar` on her approved request
- **WHEN** the page is shown
- **THEN** it says the reservation is approved and that cancelling it is requested from Administración, with no action buttons

#### Scenario: Closed request

- **GIVEN** a rejected or cancelled request
- **WHEN** the page is shown
- **THEN** the closing reason is listed and the page states that no more actions are accepted

### Requirement: Request tables open the detail

In "Mis solicitudes" and "Todas las solicitudes" a single click on any row, whatever its state, or Enter on the focused row, SHALL open the request detail. The primary cell of each row MUST be a real link to the detail. The row action "Cancelar" of "Mis solicitudes" MUST keep cancelling without opening the detail. Double click MUST NOT trigger any extra behavior.

#### Scenario: Click on a row

- **GIVEN** the list of requests
- **WHEN** the user clicks a row, or focuses it and presses Enter
- **THEN** the detail of that request opens

#### Scenario: Cancel button of the row

- **GIVEN** a pending request in "Mis solicitudes"
- **WHEN** the user presses its "Cancelar" button
- **THEN** the cancel confirmation opens and the detail does not
