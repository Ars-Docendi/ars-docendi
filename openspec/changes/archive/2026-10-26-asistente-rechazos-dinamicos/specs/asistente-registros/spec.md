## ADDED Requirements

### Requirement: The operational record stores the refusal reason

The operational record SHALL store, for each turn, the refusal reason declared by the generation (`fuera_de_tema`, `otro_sistema`, `muy_general` or `no_cubierto`) when the turn ended as a model-declared refusal, and SHALL store null for every other turn — answered turns, clarifications, degraded or failed turns, validator and engine rejections, empty results, and generations that could not be parsed or were cut by the token ceiling. The column SHALL be nullable, without default, and restricted by the database to the four values. The term named in the refusal MUST NOT be stored.

#### Scenario: A declared reason is recorded

- **GIVEN** a turn refused by the generation with reason `muy_general`
- **WHEN** the operational record is inspected
- **THEN** its row carries `muy_general`

#### Scenario: A validator rejection records no reason

- **GIVEN** a turn whose query the validator rejected
- **WHEN** the operational record is inspected
- **THEN** its row carries a null reason

#### Scenario: An answered turn records no reason

- **GIVEN** a turn that ended answered
- **WHEN** the operational record is inspected
- **THEN** its row carries a null reason

#### Scenario: The database rejects an unknown reason

- **GIVEN** the migrated database
- **WHEN** a row with the reason `clima` is inserted into the operational record
- **THEN** the engine rejects it

#### Scenario: Re-applying the migration converges

- **GIVEN** a database whose operational record existed before the column
- **WHEN** the module migration runs twice
- **THEN** the column exists once and existing rows keep a null reason

### Requirement: The analytic record never stores the refusal reason

The analytic record MUST NOT store the refusal reason, so that no column shared with the operational record, other than the ones that already exist, can narrow who asked what.

#### Scenario: The analytic record has no reason column

- **GIVEN** the migrated database
- **WHEN** the columns of the analytic record are inspected
- **THEN** none of them stores the refusal reason
