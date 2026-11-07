## ADDED Requirements

### Requirement: The catalog announces aulas questions by the live permissions

The system SHALL derive from the actor's live permissions whether the capability presentation announces questions about reservation requests, and for whom: an actor with `aulas.aprobar` hears about all requests, an actor with only `aulas.solicitar` hears about their own requests, anyone else hears nothing about them.

The areas, counts and examples of the catalog MUST follow the same gate, even though the GRANT is common to both read roles.

The presentation MUST NOT offer questions about free aulas or aula capacity.

#### Scenario: An approver is offered all requests

- **GIVEN** an actor who holds `aulas.aprobar`
- **WHEN** the actor asks for the capability catalog
- **THEN** the presentation offers questions about the requests, their state and assigned aula

#### Scenario: A requester is offered their own

- **GIVEN** an actor who holds only `aulas.solicitar`
- **WHEN** the actor asks for the capability catalog
- **THEN** the presentation offers "tus solicitudes de reserva de aula" and not the whole table

#### Scenario: Without a permission they are not offered

- **GIVEN** an actor with neither permission
- **WHEN** the actor asks for the capability catalog
- **THEN** the presentation, areas and examples do not mention reservation requests
