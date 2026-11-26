## ADDED Requirements

### Requirement: The comisiones comments state the facts the model cannot infer

The comments on the exposed comisiones objects SHALL state that the snapshot accumulates several períodos lectivos, that `aula` and `responsabilidad` are free text, and how `dia` maps to weekdays.

The comments MUST say how a comisión relates to materia and carrera through the foreign keys, and that a docente or materia not matched to the system has a null link.

The comments MUST distinguish a docente of a comisión (who teaches, per Guaraní) from a designación (the administrative appointment).

#### Scenario: The prefix carries the period and aula caveats

- **GIVEN** a prefix built for either read role
- **WHEN** the comments of the comisiones tables are read
- **THEN** they state that several períodos accumulate and that aula is free text

### Requirement: The prefix does not depend on comisiones row values

The prefix MUST NOT depend on the values of comisiones rows: the prompt connection has no actor, so a value list read from a row-protected table would be empty.

#### Scenario: The prefix does not read comisiones values

- **GIVEN** a prefix built for either read role
- **WHEN** the closed-value lists of the prefix are listed
- **THEN** none of them is a column of a `comisiones` table

#### Scenario: Denied comisiones objects are absent

- **GIVEN** a prefix built for either read role
- **WHEN** it is searched for the import log
- **THEN** it does not appear

### Requirement: The prefix with comisiones fits the local profile

The measured prefix, with the comisiones tables, comments and examples, SHALL leave the longest request of the evaluation datasets within the context of the local single-slot profile with room for the model's output.

If it does not fit, the change MUST NOT be promoted until either the comments are compacted or the reduced capacity of the default two-slot profile is documented.

#### Scenario: The longest request fits

- **GIVEN** the prefix measured with comisiones
- **WHEN** the longest request of the datasets is added to it
- **THEN** the total plus the output ceiling is within the local profile's context

#### Scenario: The measurement is recorded

- **WHEN** the change is ready for review
- **THEN** the prefix size before and after is written in the architecture documentation
