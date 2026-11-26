## ADDED Requirements

### Requirement: Aulas requests are evaluated by their own scope rule

A query that touches `aulas.solicitudes_reserva`, plus identity catalogs, SHALL be considered to reach everything if and only if the actor holds `aulas.aprobar`.

For an actor who holds only `aulas.solicitar`, the answer SHALL be scoped to the actor's own requests: an empty result MUST NOT be presented as "there are none" about the Departamento, and a non-empty result MUST carry the scope notice.

For an actor with neither permission, a result MUST NOT be presented as the absence of requests.

A query that touches aulas and another domain SHALL require the rule of each domain.

#### Scenario: An approver can hear that there are none

- **GIVEN** an actor who holds `aulas.aprobar`
- **WHEN** a query on pending requests returns no rows
- **THEN** the answer may say that no record matches

#### Scenario: A requester is told the answer is about their own requests

- **GIVEN** a jefe de cátedra who holds `aulas.solicitar` and not `aulas.aprobar`
- **WHEN** a query on requests returns no rows or some rows
- **THEN** the answer acknowledges that it covers only what the actor can see and does not state that no other request exists

#### Scenario: Aulas combined with designaciones needs both rules

- **GIVEN** an approver who does not reach designaciones
- **WHEN** a query crosses requests with designaciones
- **THEN** the answer carries the designaciones scope limit
