## MODIFIED Requirements

### Requirement: Catálogos canónicos

Personas, materias, cargos, dedicaciones y períodos SHALL provenir de la API. Las mutaciones MUST enviar sus UUID canónicos. Para un Alta, el formulario SHALL ofrecer como materias únicamente las materias activas donde el actor tenga una membresía vigente de Jefe de Cátedra. Para una Baja o un Cambio de cargo o dedicación, el formulario SHALL ofrecer únicamente las materias activas que estén simultáneamente dentro del ámbito del actor y entre las designaciones vigentes del docente seleccionado. Las dedicaciones seleccionables SHALL ser Categoría 1 a 6, de elección libre incluso al mantener o reducir la dedicación actual; la API MUST validar todas las referencias canónicas.

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

### Requirement: Reglas por novedad

El backend MUST aplicar las reglas por novedad al crear y editar. `Alta` SHALL recibir los datos de una persona nueva —DNI, nombre y apellido— y exigir CV, DNI frente y DNI dorso [BR-designaciones-002]. `Baja` exige tipo, adjunto justificativo y persona con legajo [BR-designaciones-003, BR-designaciones-018]. `Cambio de cargo o dedicación` exige justificación y persona con legajo [BR-designaciones-004, BR-designaciones-018]. Las únicas novedades admitidas para nuevos pedidos, ediciones y avance del circuito SHALL ser Alta, Baja y Cambio de cargo o dedicación. El formulario MUST exigir una elección explícita. `Sin novedad` MUST NOT ofrecerse ni aceptarse como nueva solicitud; los registros legados SHALL conservarse para lectura histórica.

#### Scenario: Alta con datos nuevos

- **WHEN** el Jefe elige "Alta"
- **THEN** el formulario muestra campos de DNI y apellido/nombre, no un selector de personas ya registradas

#### Scenario: Alta incompleta

- **WHEN** se guarda un Alta sin DNI, nombre, apellido o alguno de sus tres adjuntos
- **THEN** el backend rechaza el pedido

#### Scenario: Baja o Cambio sin legajo

- **WHEN** la persona no tiene legajo
- **THEN** el backend rechaza una Baja o Cambio, pero permite un Alta con datos y documentación completos

#### Scenario: Solicitud Sin novedad rechazada

- **GIVEN** un cliente que omite el formulario
- **WHEN** intenta crear, editar, enviar, reenviar o aprobar un pedido Sin novedad
- **THEN** la API MUST rechazar la operación sin cambiar datos ni historial

#### Scenario: Lectura de pedido legado

- **GIVEN** un pedido Sin novedad anterior al cambio
- **WHEN** se consulta su detalle autorizado
- **THEN** MUST conservar su número, texto original e historial sin transformarse en otra novedad

### Requirement: Una materia por pedido

Cada pedido SHALL referir exactamente una materia y su carga horaria. En un Alta, la materia se elegirá entre las materias a cargo del Jefe de Cátedra. En una Baja o un Cambio de cargo o dedicación, la materia se elegirá entre las designaciones vigentes del docente seleccionado que estén a cargo del actor. La carrera se deriva de la materia para determinar al Coordinador competente.

#### Scenario: Enrutamiento

- **GIVEN** un pedido de una materia perteneciente a una carrera
- **WHEN** se envía
- **THEN** queda en revisión del Coordinador de esa carrera

#### Scenario: Una única materia compatible se autoselecciona

- **GIVEN** un docente seleccionado con exactamente una materia compatible con el ámbito del actor
- **WHEN** se carga una Baja o un Cambio
- **THEN** esa materia queda seleccionada automáticamente y sus datos vigentes se muestran como solo lectura

#### Scenario: Varias materias compatibles requieren elección

- **GIVEN** un docente seleccionado con dos o más materias compatibles con el ámbito del actor
- **WHEN** se carga una Baja o un Cambio
- **THEN** el formulario exige elegir una de esas materias antes de guardar

#### Scenario: Alta se enruta por la materia elegida

- **GIVEN** un Jefe de Cátedra con más de una materia propia
- **WHEN** crea un Alta y elige una materia
- **THEN** el pedido queda persistido y se enruta según la carrera de esa materia

## ADDED Requirements

### Requirement: Alta de persona sin cuenta de autenticación

Un Alta SHALL poder registrar los datos de una persona que todavía no tiene una fila en `identity.personas`, o reutilizar la referencia canónica ya resuelta por el backend cuando corresponda. La operación MUST no exigir UPN, cuenta Azure AD ni legajo. El pedido SHALL conservar la referencia canónica de la persona y MUST rechazar un documento duplicado sin crear un pedido parcial.

#### Scenario: Alta de persona nueva

- **GIVEN** un DNI que no existe en `identity.personas`
- **WHEN** el Jefe guarda o envía un Alta válida
- **THEN** el sistema registra la persona sin legajo ni usuario Azure AD y persiste el pedido con su `personaId` canónico

#### Scenario: Documento ya registrado

- **GIVEN** un DNI que ya existe en `identity.personas`
- **WHEN** se intenta registrar una nueva persona para un Alta
- **THEN** la API rechaza el conflicto de identidad sin duplicar la persona ni persistir un pedido incompleto

#### Scenario: Primer login posterior al Alta

- **GIVEN** una persona registrada por un Alta sin usuario Azure AD
- **WHEN** se autentica por primera vez con el documento correspondiente
- **THEN** el sistema crea o vincula `identity.users` a la persona existente sin crear una segunda persona
