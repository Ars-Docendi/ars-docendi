## ADDED Requirements

### Requirement: Proyección completa dentro del ámbito docente

La API de docentes MUST aplicar el conjunto de materias visibles del actor a todos los datos de cada respuesta, no sólo a la selección de personas. Para un actor acotado, cada docente listado o consultado SHALL incluir únicamente asignaciones vigentes y membresías docentes vinculadas a materias visibles. Un docente sin asignación visible SHALL quedar fuera del listado y su detalle SHALL responder como recurso no visible. Los usuarios con alcance departamental SHALL conservar todas sus asignaciones y membresías.

#### Scenario: Listado con asignaciones mixtas

- **GIVEN** un Jefe de Cátedra que puede ver la Materia A y un docente con asignaciones vigentes en A y B
- **WHEN** consulta el listado de docentes
- **THEN** el docente aparece una sola vez, con la asignación de A, sin la asignación de B ni sus datos derivados

#### Scenario: Detalle con asignaciones mixtas

- **GIVEN** un Jefe de Cátedra que puede ver la Materia A y un docente con asignaciones vigentes en A y B
- **WHEN** consulta el detalle del docente
- **THEN** la respuesta contiene sólo la asignación de A y las membresías docentes de materias visibles

#### Scenario: Persona sólo fuera de ámbito

- **GIVEN** un docente con una única asignación vigente en la Materia B y un actor que sólo puede ver A
- **WHEN** el actor lista o consulta ese docente
- **THEN** el listado no lo incluye y el detalle responde `404`

#### Scenario: Consulta global

- **GIVEN** un usuario con alcance departamental
- **WHEN** consulta el listado o detalle de docentes
- **THEN** recibe todas las asignaciones y membresías vigentes permitidas por la vista global
