## Purpose

Defines who can read the SIU-Guaraní comisiones snapshot through the assistant, which tables and columns are exposed, which stay denied, and how an answer built on comisiones is scoped and declared. The boundary is enforced by the database engine, not by the application.

## ADDED Requirements

### Requirement: Comisiones are readable only with the comisiones.ver permission

The system SHALL return a row of an exposed `comisiones` table to an assistant actor only if that actor holds `comisiones.ver`.

The system MUST NOT condition that visibility on the actor's scope: an actor with the permission sees every comisión, whatever their materia or carrera.

Each exposed table MUST carry its own policy that names the actor, and MUST NOT rely on the policy of another table.

#### Scenario: An actor with the permission sees every comisión

- **GIVEN** an actor of carrera scope who holds `comisiones.ver`
- **WHEN** the actor reads the comisiones, their docentes, their carreras and their schedules
- **THEN** the actor sees the same rows as the table owner

#### Scenario: An actor without the permission sees nothing

- **GIVEN** an actor who does not hold `comisiones.ver`
- **WHEN** the actor reads any exposed comisiones table
- **THEN** no row is returned and the turn does not fail

#### Scenario: A connection with no actor sees nothing

- **GIVEN** a read-role connection with no actor set
- **WHEN** it reads an exposed comisiones table
- **THEN** no row is returned

#### Scenario: Every policy names the actor

- **GIVEN** the policies of the four exposed tables
- **WHEN** their predicates are read
- **THEN** each one resolves the actor's `comisiones.ver` permission by itself

#### Scenario: The Comisiones screen is not affected

- **GIVEN** the application connection, which owns the tables
- **WHEN** it lists comisiones without setting any actor
- **THEN** it sees every row

### Requirement: Exposure is column by column and the import log stays denied

The system SHALL grant the two read roles `SELECT` on the listed columns of `comisiones.sga_comisiones`, `comisiones.sga_comision_carreras`, `comisiones.sga_comision_docentes` and `comisiones.sga_comision_horarios`, and nothing else in the schema.

The system MUST NOT grant any privilege on `comisiones.sga_importaciones`. Access to it SHALL fail for lack of privilege, not return zero rows.

#### Scenario: The import log is rejected by the engine

- **GIVEN** an actor with `comisiones.ver`
- **WHEN** a query reads `comisiones.sga_importaciones`
- **THEN** the engine rejects it for lack of privilege

### Requirement: Every comisiones column is classified and justified

Every granted column MUST be listed in the privileges manifest and classified in the sensitivity manifest. Every column that exists and is not granted MUST carry its reason in the privileges manifest.

#### Scenario: Effective privileges match the manifest

- **GIVEN** the privileges manifest and the migrated database
- **WHEN** the effective privileges of both read roles are compared with the manifest
- **THEN** they match in every direction, and the `comisiones` schema is classified as exposed

#### Scenario: The roles cannot mutate the snapshot

- **GIVEN** both read roles
- **WHEN** their privileges on the exposed tables are listed
- **THEN** none of them can insert, update, delete or truncate

### Requirement: The docente name of a comisión is readable and public

The system SHALL grant `comisiones.sga_comision_docentes.nombre` to both read roles and classify it `publica`, the same class as `identity.personas.nombre`.

The system MUST keep a docente who has no matching person (`persona_id` null) visible in answers by that name.

#### Scenario: An unmatched docente appears in answers

- **GIVEN** a comisión whose docente has no `persona_id`
- **WHEN** the actor asks who teaches that comisión
- **THEN** the docente appears by name

#### Scenario: The name is not masked on its way to the model

- **GIVEN** the sensitivity manifest
- **WHEN** the classification of the docente name is read
- **THEN** it is `publica`

### Requirement: A cross-domain answer carries the scope limit even with rows

When a query touches comisiones and designaciones and the actor does not satisfy the designaciones rule, the answer MUST carry the scope limit even if rows were returned, because a comparison between the two domains can produce rows that exist only because one side is truncated by scope.

#### Scenario: A cross-check keeps the designaciones limit

- **GIVEN** a jefe de cátedra
- **WHEN** a query compares comisiones docentes with designaciones and returns rows
- **THEN** the answer states that the designaciones side was limited to the actor's scope

### Requirement: An empty comisiones answer declares which períodos are imported

The system SHALL, when a query that touches comisiones returns zero rows, add the list of períodos lectivos currently imported to the empty-result text, in addition to any scope notice.

The system MUST NOT run that lookup for a query that does not touch comisiones, and MUST answer without it if the lookup fails.

#### Scenario: The empty result names the imported períodos

- **GIVEN** a comisiones query that returns zero rows
- **WHEN** the text is built
- **THEN** it says which períodos are imported

#### Scenario: A query outside comisiones does no extra lookup

- **GIVEN** a query that touches only designaciones
- **WHEN** the text is built
- **THEN** no períodos are looked up

#### Scenario: A failing lookup does not stop the turn

- **GIVEN** a comisiones query with zero rows where the períodos lookup raises an error
- **WHEN** the turn is resolved
- **THEN** the answer is delivered without the períodos and without error
