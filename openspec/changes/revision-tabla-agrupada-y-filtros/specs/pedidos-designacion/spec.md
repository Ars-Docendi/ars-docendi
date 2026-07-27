## MODIFIED Requirements

### Requirement: Creación de pedido de designación

El sistema SHALL permitir al Jefe de Cátedra crear un pedido de designación desde
`/designaciones/pedidos/nuevo`, capturando los datos comunes del pedido: docente (DNI, nombre,
**legajo**), antigüedad, cargo y dedicación actual (read-only, mock), una o más asignaciones de
materia con sus horas, horas de investigación, horas externas (otro departamento) y novedad. El
**legajo** es **opcional** en un pedido de Alta (el docente todavía no existe en el sistema, no tiene
legajo asignado) y se precarga automáticamente (solo lectura) para el resto de las novedades, tomado
del catálogo de docentes existentes. El pedido se crea en estado `borrador`.

#### Scenario: Alta de un pedido en borrador

- **GIVEN** un Jefe de Cátedra en el form de nuevo pedido
- **WHEN** completa los datos comunes, al menos una asignación de materia válida y una novedad válida
  con sus campos requeridos
- **THEN** el pedido se persiste en estado `borrador` y aparece en "Mis pedidos"

#### Scenario: Un pedido por docente por período [BR-designaciones-001]

- **GIVEN** un docente que ya tiene un pedido en el período abierto
- **WHEN** el Jefe de Cátedra intenta crear un segundo pedido para ese mismo docente en el mismo
  período
- **THEN** el sistema MUST rechazar la creación e indicar que ya existe un pedido para ese docente en
  el período, sin importar si las materias asociadas al segundo pedido difieren de las del primero

#### Scenario: Un Alta no tiene legajo todavía

- **GIVEN** un Jefe de Cátedra creando un pedido de Alta (docente nuevo, no existe en el sistema)
- **WHEN** completa los datos del docente a mano (DNI, nombre)
- **THEN** el pedido se crea sin legajo asignado — el form no ofrece un campo para tipearlo

#### Scenario: El legajo se precarga al elegir un docente existente

- **GIVEN** un Jefe de Cátedra creando un pedido de Baja, Cambio o Sin novedad
- **WHEN** selecciona un docente del catálogo de docentes existentes
- **THEN** el pedido queda con el legajo de ese docente, tomado del catálogo (solo lectura)
