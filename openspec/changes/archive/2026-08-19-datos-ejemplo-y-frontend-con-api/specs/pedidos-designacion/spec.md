## ADDED Requirements

### Requirement: Persistencia remota del ciclo de pedidos

La creación, consulta, edición, envío, reenvío y eliminación de pedidos MUST ejecutarse mediante la API de Designaciones. El backend MUST asignar el número de trámite, validar el actor y el ámbito, aplicar la máquina de estados y confirmar pedido e historial en una única transacción.

#### Scenario: Crear y recuperar borrador

- **GIVEN** un Jefe de Cátedra autorizado para la materia seleccionada
- **WHEN** crea un pedido válido
- **THEN** el backend devuelve un borrador con identificador y número únicos, y una consulta posterior recupera el mismo pedido

#### Scenario: Transición inválida enviada por cliente

- **GIVEN** un pedido que no admite la acción solicitada
- **WHEN** un cliente intenta ejecutar la transición mediante la API
- **THEN** el backend MUST rechazarla sin cambiar el pedido ni agregar un evento de historial

#### Scenario: Eliminación durable de borrador

- **GIVEN** un borrador propio sin impedimentos para eliminarse
- **WHEN** el Jefe de Cátedra confirma su eliminación
- **THEN** la API lo elimina y las consultas posteriores no lo devuelven

### Requirement: Catálogos persistidos para formularios de pedidos

Los períodos, personas docentes, materias y cargos disponibles para crear o editar pedidos SHALL provenir de la API y utilizar identificadores canónicos en las mutaciones.

#### Scenario: Apertura del formulario

- **GIVEN** un actor con más de una materia a cargo y un período activo
- **WHEN** abre el formulario de pedido
- **THEN** las opciones corresponden a sus ámbitos y a los catálogos activos persistidos
