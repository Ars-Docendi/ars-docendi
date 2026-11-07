# Spec Delta

## Purpose

Garantiza que las designaciones vigentes cambien sólo a través del circuito de pedidos, y que ese circuito lo puedan iniciar, además del Jefe de Cátedra, los roles operativos del Departamento, también para personas que ya existen en el sistema.

## ADDED Requirements

### Requirement: Designaciones de sólo lectura en la administración de personas

La administración de personas SHALL mostrar las designaciones vigentes de una persona (internas o externas) sólo a modo informativo. MUST NOT permitir agregarlas, quitarlas ni modificarlas, y la API de personas MUST NOT aceptar designaciones al crear o editar una persona. El alta de una persona con rol Docente o Jefe de Cátedra MUST NOT exigir designaciones: la designación surge de un pedido de Alta aprobado.

#### Scenario: Ver designaciones de una persona

- **GIVEN** una persona con una designación interna y otra externa
- **WHEN** un administrador abre su ficha
- **THEN** ve ambas designaciones con su materia, carrera, cargo y dedicación o sector, y horas, sin controles para editarlas

#### Scenario: Editar una persona no toca sus designaciones

- **GIVEN** una persona con designaciones vigentes
- **WHEN** un administrador cambia su teléfono y guarda
- **THEN** sus designaciones vigentes quedan exactamente igual

#### Scenario: Alta de docente sin designación

- **WHEN** un administrador crea una persona con rol Docente en una materia
- **THEN** la persona y su rol se crean sin designación vigente

#### Scenario: Cliente que envía designaciones

- **WHEN** un cliente envía designaciones en la creación o edición de una persona
- **THEN** la API no las aplica y las designaciones vigentes no cambian

### Requirement: Carga de pedidos por Administrativo y Secretaría

Además del Jefe de Cátedra sobre sus materias, los roles Administrativo y Secretaría SHALL poder cargar, editar, eliminar en borrador y enviar pedidos de Alta, Baja y Cambio sobre cualquier materia del Departamento. El pedido SHALL recorrer el mismo circuito (Coordinador, Secretaría, Decanato). Cuando una devolución lo regresa al primer nivel, SHALL poder corregirlo y reenviarlo el Jefe de Cátedra de la materia o un Administrativo o Secretaría.

#### Scenario: Administrativo carga un Cambio de cualquier materia

- **GIVEN** un usuario Administrativo sin membresía de Jefe de Cátedra
- **WHEN** abre un nuevo pedido
- **THEN** puede elegir cualquier materia del Departamento y cualquier docente con designación vigente en ella, y enviarlo al Coordinador de la carrera

#### Scenario: Devolución al primer nivel de un pedido cargado por Administrativo

- **GIVEN** un pedido cargado por un Administrativo y devuelto por el Coordinador
- **WHEN** un Administrativo o el Jefe de Cátedra de la materia lo corrige y lo reenvía
- **THEN** el pedido vuelve a la etapa del Coordinador

#### Scenario: Rol sin permiso de carga

- **WHEN** un Coordinador o Decanato intenta crear un pedido
- **THEN** la API lo rechaza sin persistir nada

### Requirement: Alta de una persona existente

El Alta SHALL permitir elegir una persona que ya existe en el sistema mediante un buscador emergente por nombre, apellido, DNI o legajo, como alternativa a cargar una persona nueva. El buscador MUST ofrecer sólo personas sin designación vigente y sin un pedido vivo en el período, y el backend MUST validar la persona elegida.

#### Scenario: Alta de una persona creada en la administración

- **GIVEN** una persona creada desde la administración, sin designación
- **WHEN** quien carga el pedido abre el buscador, la encuentra por su DNI y la elige
- **THEN** el Alta queda asociada a esa persona sin pedir nombre, apellido ni DNI

#### Scenario: Persona ya designada

- **GIVEN** una persona con una designación vigente
- **WHEN** se la busca en el buscador del Alta
- **THEN** no aparece entre los resultados

#### Scenario: Persona con pedido en curso

- **GIVEN** una persona que ya tiene un pedido vivo en el período
- **WHEN** se la busca en el buscador del Alta
- **THEN** no aparece entre los resultados
