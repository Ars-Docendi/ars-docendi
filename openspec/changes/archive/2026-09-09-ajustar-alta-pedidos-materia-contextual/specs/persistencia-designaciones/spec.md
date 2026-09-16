## MODIFIED Requirements

### Requirement: El pedido cubre exactamente una materia

El sistema SHALL persistir cada pedido de designación con una única `materia_id` —la cátedra sobre la que el Jefe de Cátedra opera— y sus `horas` como columnas del propio pedido. Para un Alta, la materia deberá pertenecer al conjunto de materias donde el actor tiene una membresía vigente de Jefe de Cátedra. Para una Baja o un Cambio de cargo o dedicación, la materia deberá pertenecer tanto al ámbito del actor como a una designación vigente del docente seleccionado. La carrera del pedido SHALL derivarse de `identity.materias.carrera_id` y MUST NOT almacenarse denormalizada.

#### Scenario: La carrera del pedido se deriva de su materia

- **GIVEN** un pedido persistido sobre una materia
- **WHEN** el Coordinador consulta su tablero de revisión
- **THEN** el pedido MUST aparecer bajo la carrera a la que pertenece esa materia, resolviendo un único Coordinador competente

#### Scenario: Un Jefe de Cátedra no puede cargar un pedido sobre una cátedra ajena

- **GIVEN** un Jefe de Cátedra sin rol vigente sobre una materia dada
- **WHEN** intenta crear un pedido sobre esa materia
- **THEN** el sistema MUST denegar la creación

#### Scenario: Un rol revocado deja de habilitar la carga

- **GIVEN** un usuario cuyo rol de Jefe de Cátedra sobre una materia fue revocado (`deleted_at` no nulo)
- **WHEN** intenta crear un pedido sobre esa materia
- **THEN** el sistema MUST denegar la creación

#### Scenario: Baja sobre una materia no designada al docente

- **GIVEN** un Jefe de Cátedra con ámbito sobre Materia A y un docente con designación vigente sólo en Materia B
- **WHEN** intenta crear una Baja para Materia A
- **THEN** el backend MUST rechazarla porque la materia no pertenece al estado vigente del docente

#### Scenario: Cambio sobre la materia seleccionada del docente

- **GIVEN** un docente con designaciones vigentes en Materias A y B, y un Jefe con ámbito sobre ambas
- **WHEN** crea un Cambio para Materia B
- **THEN** el pedido MUST referir `materia_id` de B y tomar de B los datos vigentes del snapshot
