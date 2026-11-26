## ADDED Requirements

### Requirement: The projects comments state what the table does and does not hold

The system SHALL document `portal.proyectos` and each granted column with `COMMENT ON`, stating that a project is self-declared by the docente, that `rol` and `nombre` are free text (related terms may be spelled differently), that `hasta` null means ongoing, that `doi` is optional, and that the description is not available.

The comment MUST NOT promise that the data proves a docente meets the requirements of a materia.

#### Scenario: The prefix carries the projects facts

- **WHEN** the prefix is built for either read role
- **THEN** it lists `portal.proyectos` with the granted columns only
- **AND** it does not list `descripcion`

### Requirement: The prefix with projects fits the local profile

The system SHALL keep the longest evaluator request, with the projects table added, within the local 3070 profile (one slot of 16,384 tokens) including the output ceiling.

If it does not fit, comments are compacted first and examples second, before any column is dropped.

#### Scenario: The measured request fits

- **WHEN** the evaluator runs against the local profile
- **THEN** the longest input plus the output ceiling is below 16,384 tokens and the numbers are recorded in `docs/architecture/modelo-local.md`
