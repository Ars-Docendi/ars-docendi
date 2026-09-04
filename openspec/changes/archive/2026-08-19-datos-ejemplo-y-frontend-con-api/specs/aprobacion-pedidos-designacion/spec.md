## ADDED Requirements

### Requirement: Backend autoritativo para acciones de revisión

Aceptar, rechazar, devolver, reenviar, priorizar y despriorizar MUST ejecutarse mediante la API. El backend MUST resolver la identidad, el rol activo y el ámbito; no MUST confiar en nombres, roles, carrera o cátedras enviados como autoridad por el frontend.

#### Scenario: Acción autorizada

- **GIVEN** un revisor autenticado cuyo rol activo y ámbito corresponden a la etapa
- **WHEN** confirma una acción válida
- **THEN** el backend actualiza el estado y registra el evento con actor y rol persistidos en una única transacción

#### Scenario: Cliente falsifica el ámbito

- **GIVEN** un actor sin alcance sobre el pedido
- **WHEN** envía una solicitud declarando un ámbito o rol que no posee
- **THEN** el backend MUST denegar la acción y no modificar el pedido ni su historial

### Requirement: Consultas de revisión filtradas por el backend

Las listas y detalles de revisión MUST ser filtrados por el backend según el rol activo y los ámbitos persistidos del actor.

#### Scenario: Coordinador consulta su tablero

- **GIVEN** un Coordinador asignado a una carrera
- **WHEN** consulta los pedidos para revisión
- **THEN** recibe sólo pedidos visibles dentro de esa carrera y las acciones admitidas para la etapa actual
