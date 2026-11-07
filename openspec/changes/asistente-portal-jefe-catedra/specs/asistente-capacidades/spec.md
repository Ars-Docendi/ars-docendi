## ADDED Requirements

### Requirement: The jefe de cátedra catalog announces the docente search with its limit

The presentation for an actor holding `portal.ver_trayectoria_ajena` with a jefe-de-cátedra scope SHALL offer the docente search by formación, certificaciones, habilidades, intereses, experiencia and proyectos, and SHALL say it covers the docentes designated in their materias.

The catalog MUST NOT offer the personal contact, the CV content or the project description, and MUST NOT offer a «who meets the requirements» search.

#### Scenario: The jefe sees the search and its scope

- **GIVEN** a jefe de cátedra with the permission
- **WHEN** they ask what the assistant can do
- **THEN** the answer offers the docente search and names the cátedra scope

#### Scenario: Requirements matching is not offered

- **GIVEN** any actor
- **WHEN** the catalog is read
- **THEN** it does not claim to determine who meets a materia's requirements
