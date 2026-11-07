# Spec Delta

## MODIFIED Requirements

### Requirement: Catálogos canónicos

Personas, materias, cargos, dedicaciones, sectores externos y períodos SHALL provenir de la API. Las mutaciones MUST enviar sus UUID canónicos. Para un Alta, el formulario SHALL ofrecer como materias únicamente las materias activas donde el actor tenga una membresía vigente de Jefe de Cátedra. Para una Baja o un Cambio de cargo o dedicación, el formulario SHALL ofrecer únicamente las materias activas que estén simultáneamente dentro del ámbito del actor y entre las designaciones vigentes del docente seleccionado. En un catálogo solicitado por un actor acotado, `personas` SHALL incluir sólo personas con al menos una designación vigente en una materia visible para ese actor y `designacionesVigentes` SHALL incluir únicamente esas designaciones visibles. Las dedicaciones seleccionables SHALL ser Categoría 0 a 6, de elección libre incluso al mantener o reducir la dedicación actual; la API MUST validar todas las referencias canónicas.

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
- **THEN** el selector MUST ofrecer las siete categorías y backend MUST aceptar 0, 1, 2 o 6 con los demás datos válidos

#### Scenario: Categoría ajena al catálogo

- **GIVEN** una nueva solicitud
- **WHEN** un cliente envía texto libre, un UUID inexistente o una categoría inactiva
- **THEN** backend MUST rechazar la selección sin persistir el pedido

### Requirement: Completitud de Alta y Cambio antes del circuito

Enviar, reenviar y aprobar un Alta o Cambio MUST exigir horas de materia positivas y no nulas y, además, cargo válido y dedicación válida para un docente interno, o sector externo válido para un docente externo, junto con las reglas existentes por novedad. La falta de datos SHALL devolver validación por campo sin avance, snapshot ni eventos parciales. El guardado de borradores MUST NOT ampliar la admisión de datos inválidos ni imponer a una Baja los campos de un Alta.

#### Scenario: Enviar Alta sin cargo

- **GIVEN** un borrador Alta interno con documentación válida pero sin cargo
- **WHEN** se intenta enviarlo
- **THEN** se rechaza con error de campo y conserva su estado, asociaciones e historial

#### Scenario: Enviar Alta externa sin sector

- **GIVEN** un borrador Alta marcado como externo, con documentación y horas válidas pero sin sector
- **WHEN** se intenta enviarlo
- **THEN** se rechaza con error en el campo de sector y conserva su estado, asociaciones e historial

#### Scenario: Reenviar Cambio sin horas

- **GIVEN** un Cambio devuelto con horas nulas
- **WHEN** su propietario intenta reenviarlo
- **THEN** se rechaza antes de confirmar la transición y el snapshot inicial permanece inmutable

#### Scenario: Aprobar legado incompleto

- **GIVEN** un pedido antiguo en revisión con cargo u horas faltantes
- **WHEN** el revisor autorizado intenta aprobarlo
- **THEN** recibe un error consumible sin materialización parcial ni valores inventados

#### Scenario: Baja válida conserva su semántica

- **GIVEN** una Baja con tipo, justificativo, legajo y vigencia válidos
- **WHEN** se envía sin cargo nuevo ni horas nuevas
- **THEN** no se rechaza por campos exclusivos de Alta/Cambio

### Requirement: Edición y eliminación

Un borrador SHALL ser editable y eliminable sólo por quien puede cargar pedidos sobre su materia —el Jefe de Cátedra dentro de su ámbito, o un Administrativo o Secretaría—. Un pedido devuelto SHALL ser editable por su propietario actual —Jefe, Coordinador o Secretaría— dentro de su ámbito; cuando el propietario es el primer nivel, también por un Administrativo o Secretaría. Un devuelto MUST NOT ser eliminable [BR-designaciones-008, BR-designaciones-009].

#### Scenario: Corregir una devolución

- **GIVEN** un pedido devuelto a Coordinación o Secretaría
- **WHEN** el propietario edita datos válidos y lo reenvía
- **THEN** la API acepta ambas operaciones y conserva el snapshot inicial

#### Scenario: Borrar un borrador

- **WHEN** su Jefe de Cátedra elimina un borrador dentro de su ámbito
- **THEN** el pedido deja de existir

#### Scenario: Administrativo borra un borrador

- **WHEN** un Administrativo elimina un borrador de cualquier materia
- **THEN** el pedido deja de existir
