## ADDED Requirements

### Requirement: The capacity axis covers the jefe de cátedra profile search

The capacity dataset SHALL include items asked by a jefe de cátedra actor about docentes of their materias: skills searched with several related terms, a posgrado, projects with a DOI, and the coverage sentence.

It SHALL include an item whose reference answer excludes a docente with the matching skill who has no current designación in the actor's materias.

The fixture MUST give that actor the permission and portal data (skills, education, projects) for some docentes inside their materias and one outside.

The `ejemplos-sql.json` examples that search skills with several terms MUST be disjoint from these items.

#### Scenario: A docente outside the cátedra is not an expected row

- **GIVEN** the item that asks for the docentes with a skill
- **WHEN** its reference query is executed as the jefe actor
- **THEN** the docente outside the cátedra is not in the reference rows

#### Scenario: The fixture makes the items unpassable without reading

- **GIVEN** the jefe actor without the permission
- **WHEN** a profile-search item is evaluated
- **THEN** it cannot pass

#### Scenario: Examples and items stay disjoint

- **WHEN** the disjointness test runs
- **THEN** no new item repeats a new example
