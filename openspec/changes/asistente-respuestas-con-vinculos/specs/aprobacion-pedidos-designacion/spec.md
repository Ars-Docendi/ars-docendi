## ADDED Requirements

### Requirement: El módulo entrega la ficha de un trámite que el actor puede abrir

El módulo SHALL exponer en su contract público, dado el identificador de un trámite, una ficha con su número, novedad, materia, estado, período y fecha de la última actividad, y además el cargo y la dedicación solicitados cuando el trámite los tiene.

El módulo SHALL devolver la ficha sólo si el actor autenticado puede abrir el detalle de ese trámite, con el mismo criterio de ámbito que el endpoint de detalle. En cualquier otro caso MUST NOT devolver nada.

La ficha MUST NOT incluir la ruta de la interfaz.

#### Scenario: El jefe obtiene la ficha de un trámite de su materia

- **GIVEN** un jefe de cátedra y un trámite de una materia a su cargo
- **WHEN** se pide la ficha de ese trámite
- **THEN** el módulo devuelve su número, novedad, materia, estado, período y última actividad, con cargo y dedicación si los tiene

#### Scenario: Un trámite fuera del ámbito no tiene ficha

- **GIVEN** un jefe de cátedra y un trámite de una materia ajena
- **WHEN** se pide la ficha de ese trámite
- **THEN** el módulo no devuelve nada

#### Scenario: Un identificador inexistente no rompe

- **GIVEN** un identificador que no corresponde a ningún trámite
- **WHEN** se pide su ficha
- **THEN** el módulo no devuelve nada
