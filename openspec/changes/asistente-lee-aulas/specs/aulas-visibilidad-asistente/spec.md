## Purpose

Defines who can read the reservation requests of aulas through the assistant, what is exposed, and how an answer built on them is scoped and declared. The boundary is enforced by the database engine, not by the application.

## ADDED Requirements

### Requirement: Requests are visible by permission, mirroring the Aulas screen

The system SHALL return a row of `aulas.solicitudes_reserva` to an assistant actor only if the actor holds `aulas.aprobar`, or holds `aulas.solicitar` and is the requester (`docente_id` equals the actor's persona).

The system MUST NOT condition that visibility on the actor's materia or carrera scope.

An actor with neither permission, an actor who requests but has no linked persona, and a connection with no actor set SHALL see no row, and the turn MUST NOT fail.

#### Scenario: An approver sees every request

- **GIVEN** an actor who holds `aulas.aprobar`
- **WHEN** the actor reads the requests
- **THEN** the actor sees the same rows as the table owner

#### Scenario: A requester sees only their own

- **GIVEN** an actor who holds `aulas.solicitar` and not `aulas.aprobar`, and requests made by other people
- **WHEN** the actor reads the requests
- **THEN** only the requests whose requester is the actor's persona are returned

#### Scenario: Without a permission or a persona nothing is visible

- **GIVEN** an actor holding neither permission, or a requester with no linked persona, or a connection with no actor
- **WHEN** it reads the table
- **THEN** no row is returned and no error is raised

#### Scenario: The Aulas screen is not affected

- **GIVEN** the application connection, which owns the table
- **WHEN** it lists requests without setting any actor
- **THEN** it sees every row

### Requirement: Exposure is column by column and every column is classified

The system SHALL grant the two read roles `SELECT` on every column of `aulas.solicitudes_reserva` and nothing else in the schema; `aulas.__EFMigrationsHistory` stays denied.

Every granted column MUST be listed in the privileges manifest and classified in the sensitivity manifest. `motivo_rechazo` and `motivo_cancelacion` MUST be `sensible-texto`; the other columns `publica`.

Neither read role MUST be able to insert, update, delete or truncate the table.

#### Scenario: Free-text motives do not reach the model

- **GIVEN** a result that includes `motivo_rechazo` or `motivo_cancelacion`
- **WHEN** the result is prepared for the model
- **THEN** the columns are suppressed for the model and returned unmasked to the user

#### Scenario: The roles cannot mutate

- **GIVEN** either read role
- **WHEN** it attempts to modify the table
- **THEN** the engine rejects it for lack of privilege

### Requirement: Requests and comisiones are not confused

The comments on the table SHALL state that these are reservation requests for an exam, not the aula where a comisión is taught, that there is no aula catalog, and that `aula_asignada` exists only when the request is approved.

The system SHALL answer "in which aula is X taught" from the comisiones schedules and SHALL use the requests only when the question speaks of reservations, requests or orders for an aula, or of a request state.

The system MUST NOT answer which aulas are free or their capacity.

#### Scenario: The prefix carries the distinction

- **GIVEN** a prefix built for either read role
- **WHEN** the comment of the requests table is read
- **THEN** it says the table is not where a comisión is taught and that there is no aula catalog

#### Scenario: Free aulas and capacity abstain

- **GIVEN** a question about free aulas or the capacity of an aula
- **WHEN** the turn is resolved
- **THEN** the assistant declares it cannot answer and does not read the requests as if they were a catalog
