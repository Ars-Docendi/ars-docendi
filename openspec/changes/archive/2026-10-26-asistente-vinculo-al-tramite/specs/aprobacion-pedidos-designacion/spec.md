## ADDED Requirements

### Requirement: El módulo ubica un trámite por su número legible

El módulo SHALL exponer en su contract público una consulta que, dado un conjunto de textos, devuelva los trámites cuyo número legible coincida **y** que el actor autenticado pueda abrir.

El módulo SHALL aplicar el mismo criterio de ámbito que su endpoint de detalle, reusando la misma regla y sin reescribirla.

El módulo SHALL descartar por forma los textos que no puedan ser un número de trámite, antes de consultar la base. El conocimiento del formato MUST permanecer dentro del módulo.

El módulo MUST NOT devolver un trámite que el actor no pueda abrir, aunque exista.

#### Scenario: El jefe de cátedra ubica el suyo y no el ajeno

- **GIVEN** un jefe de cátedra a cargo de una materia
- **WHEN** se piden los números de dos trámites, uno de su materia y uno de otra
- **THEN** se devuelve sólo el de su materia

#### Scenario: El coordinador ubica los de su carrera

- **GIVEN** un coordinador de una carrera
- **WHEN** se piden números de trámites de su carrera y de otra
- **THEN** se devuelven sólo los de su carrera

#### Scenario: Un texto que no es un número de trámite no llega a la base

- **GIVEN** un conjunto de textos sin ninguno con forma de número de trámite
- **WHEN** se pide ubicarlos
- **THEN** se devuelve vacío sin consultar la base

#### Scenario: Un número inexistente devuelve vacío

- **GIVEN** un número con forma válida que no corresponde a ningún trámite
- **WHEN** se pide ubicarlo
- **THEN** se devuelve vacío
