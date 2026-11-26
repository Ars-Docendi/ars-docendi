## ADDED Requirements

### Requirement: The aulas comments state the facts the model cannot infer

The comments on `aulas.solicitudes_reserva` SHALL state which request state accompanies each nullable column (`aula_asignada`, `motivo_rechazo`, `motivo_cancelacion`), that `dia` is the date the aula is requested for, that `aula_asignada` and `comision` are free text, and the real foreign keys to `identity.personas` and `identity.materias`.

The prefix MUST NOT depend on the values of the table: the prompt connection has no actor and the policy would return no row.

#### Scenario: The prefix carries the caveats

- **GIVEN** a prefix built for either read role
- **WHEN** the comments of the requests table are read
- **THEN** they state that `aula_asignada` exists only when approved and that there is no aula catalog

#### Scenario: The prefix does not read aulas values

- **GIVEN** a prefix built for either read role
- **WHEN** the closed-value lists of the prefix are listed
- **THEN** none of them is a column of an `aulas` table

### Requirement: The prefix with aulas fits the local profile

The measured prefix with the aulas table and comments SHALL leave the longest request of the evaluation datasets within the context of the local single-slot profile with room for the model's output.

#### Scenario: The size is recorded

- **WHEN** the change is ready for review
- **THEN** the prefix size before and after is written in the architecture documentation
