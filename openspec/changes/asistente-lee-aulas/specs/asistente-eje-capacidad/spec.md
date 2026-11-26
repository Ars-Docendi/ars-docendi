## ADDED Requirements

### Requirement: The capacity axis covers reservation requests

The capacity dataset SHALL include items answerable from `aulas.solicitudes_reserva` (pending requests, approved requests with their aula for a date, rejected requests per materia, an actor's own requests, requests per state) and robustness variants of some of them.

The items about free aulas and aula capacity MUST remain non-answerable and declare their acceptable refusal reasons.

The dataset MUST stay disjoint from the SQL examples of the module.

#### Scenario: Answerable aulas items exist

- **WHEN** the dataset is grouped by category
- **THEN** reservation-request items appear among the answerable categories

#### Scenario: Free aulas and capacity still abstain

- **GIVEN** the items that ask which aulas are free or the capacity of an aula
- **WHEN** they are loaded
- **THEN** they are non-answerable and declare their acceptable refusal reasons

### Requirement: The fixture makes aulas items unpassable without reading

The synthetic fixture MUST seed requests in all four states (with and without motives, a cancellation with and without motive, several dates and materias), grant `aulas.aprobar` to the global evaluation actor and `aulas.solicitar` to the jefe de cátedra actor, and make that actor's persona the requester of some requests and not of others.

#### Scenario: The scope is measurable

- **GIVEN** the evaluation fixture
- **WHEN** the global actor and the jefe actor read the requests
- **THEN** the global actor sees every request and the jefe sees a strict subset
