## MODIFIED Requirements

### Requirement: Catálogos canónicos

Personas, materias, cargos, dedicaciones y períodos SHALL provenir de la API. Las mutaciones MUST enviar sus UUID canónicos. Para un Alta, el formulario SHALL ofrecer como materias únicamente las materias activas donde el actor tenga una membresía vigente de Jefe de Cátedra. Para una Baja o un Cambio de cargo o dedicación, el formulario SHALL ofrecer únicamente las materias activas que estén simultáneamente dentro del ámbito del actor y entre las designaciones vigentes del docente seleccionado. En un catálogo solicitado por un actor acotado, `personas` SHALL incluir sólo personas con al menos una designación vigente en una materia visible para ese actor y `designacionesVigentes` SHALL incluir únicamente esas designaciones visibles. Las dedicaciones seleccionables SHALL ser Categoría 1 a 6, de elección libre incluso al mantener o reducir la dedicación actual; la API MUST validar todas las referencias canónicas.

#### Scenario: Abrir formulario

- **WHEN** un actor abre el alta o edición
- **THEN** ve el período activo y los catálogos persistidos que puede utilizar, con las materias contextualizadas por la novedad y el docente cuando corresponda

#### Scenario: Abrir formulario para un Jefe con varias materias

- **GIVEN** un Jefe de Cátedra con membresías vigentes en Materia A y Materia B
- **WHEN** abre un nuevo pedido
- **THEN** no se muestra un selector de materia separado en la parte superior y la materia se solicita dentro del flujo de novedad y docente

#### Scenario: Alta ofrece todas las materias propias del Jefe

- **GIVEN** un Jefe de Cátedra con materias A y B
- **WHEN** elige la novedad "Alta"
- **THEN** puede elegir A o B como materia del pedido, después de completar los datos del docente nuevo

#### Scenario: Baja o Cambio limita las materias al docente seleccionado

- **GIVEN** el Jefe tiene A y B, y el docente seleccionado tiene designaciones vigentes en B y C
- **WHEN** selecciona "Baja" o "Cambio de cargo o dedicación"
- **THEN** la materia del pedido sólo ofrece B

#### Scenario: El docente no tiene una materia compatible

- **GIVEN** el Jefe tiene A y el docente seleccionado sólo tiene una designación vigente en B
- **WHEN** se selecciona "Baja" o "Cambio de cargo o dedicación"
- **THEN** el formulario no permite guardar y explica que el docente no tiene una designación en una materia a cargo del actor

#### Scenario: El catálogo no expone personas fuera de ámbito

- **GIVEN** un Jefe de Cátedra que puede ver A, una persona con designaciones vigentes en A y B y otra persona con una única designación vigente en B
- **WHEN** consulta el catálogo de Designaciones
- **THEN** la primera persona aparece con sólo su designación de A y la segunda no aparece

#### Scenario: El cliente envía una materia fuera del catálogo contextual

- **GIVEN** un pedido cuyo `materiaId` no pertenece al conjunto permitido para su novedad, docente y actor
- **WHEN** intenta crearlo o editarlo
- **THEN** la API lo rechaza sin persistir cambios ni historial

#### Scenario: Cambio de dedicación libre

- **GIVEN** un docente con Categoría 2
- **WHEN** se carga un Cambio
- **THEN** el selector MUST ofrecer las seis categorías y backend MUST aceptar 1, 2 o 6 con los demás datos válidos

#### Scenario: Categoría ajena al catálogo

- **GIVEN** una nueva solicitud
- **WHEN** un cliente envía Categoría 0, texto libre o un UUID inexistente
- **THEN** backend MUST rechazar la selección sin persistir el pedido
