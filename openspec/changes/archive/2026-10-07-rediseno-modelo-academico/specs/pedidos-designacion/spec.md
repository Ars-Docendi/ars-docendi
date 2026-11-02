## MODIFIED Requirements

### Requirement: Una materia por pedido

Cada pedido SHALL referir una materia canónica (`materia_id`), una carrera (`carrera_id`) y su carga horaria. En un Alta, la materia se elegirá entre las materias a cargo del Jefe de Cátedra. En una Baja o un Cambio de cargo o dedicación, la materia se elegirá entre las designaciones vigentes del docente seleccionado que estén a cargo del actor. La carrera MUST elegirse junto con la materia (el catálogo informativo materia–plan ofrece las carreras donde esa materia se dicta de verdad) y se usa para determinar al Coordinador competente. Una materia compartida por varias carreras MUST permitir generar pedidos distintos según la carrera elegida.

#### Scenario: Enrutamiento

- **GIVEN** un pedido de una materia y una carrera
- **WHEN** se envía
- **THEN** queda en revisión del Coordinador de esa carrera

#### Scenario: Una única materia compatible se autoselecciona

- **GIVEN** un docente seleccionado con exactamente una materia compatible con el ámbito del actor
- **WHEN** se carga una Baja o un Cambio
- **THEN** esa materia (y su carrera) queda seleccionada automáticamente y sus datos vigentes se muestran como solo lectura

#### Scenario: Varias materias compatibles requieren elección

- **GIVEN** un docente seleccionado con dos o más materias compatibles con el ámbito del actor
- **WHEN** se carga una Baja o un Cambio
- **THEN** el formulario exige elegir una de esas materias antes de guardar

#### Scenario: Alta se enruta por la materia elegida

- **GIVEN** un Jefe de Cátedra con más de una materia propia
- **WHEN** crea un Alta y elige una materia y una carrera
- **THEN** el pedido queda persistido y se enruta según la carrera elegida

#### Scenario: Materia compartida entre dos carreras

- **GIVEN** un Jefe de Cátedra de la materia `01032`, que se dicta en las carreras `201` y `202`
- **WHEN** crea un Alta eligiendo la carrera `202`
- **THEN** el pedido queda enrutado al Coordinador de `202`, y el de `201` no lo ve
