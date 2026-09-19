## ADDED Requirements

### Requirement: Las llamadas al proveedor tienen timeout

El sistema SHALL imponer un tiempo máximo de respuesta a cada llamada al proveedor del modelo.

#### Scenario: Una llamada que no responde se corta

- **GIVEN** un proveedor que no responde
- **WHEN** el turno le pide una completación
- **THEN** la llamada se corta al vencer el timeout y no queda esperando indefinidamente

### Requirement: El turno tiene un techo de tiempo total medido punta a punta

El sistema SHALL acotar la duración total del turno con un único presupuesto de tiempo, y MUST NOT derivarlo de la suma de los tiempos máximos de cada etapa.

#### Scenario: Varias etapas lentas agotan el presupuesto del turno

- **GIVEN** un presupuesto de turno menor que la suma de los tiempos máximos de sus etapas
- **WHEN** las etapas consumen más que el presupuesto entre todas
- **THEN** el turno se corta al agotarse el presupuesto, aunque ninguna etapa haya superado el suyo

#### Scenario: Al vencer el presupuesto el estado es servicio degradado

- **GIVEN** un turno que agota su presupuesto de tiempo
- **WHEN** termina
- **THEN** resuelve como servicio degradado y no como error crudo

#### Scenario: El abandono del usuario no se confunde con una degradación

- **GIVEN** un turno en curso
- **WHEN** el usuario cancela el request
- **THEN** el turno no se registra como servicio degradado

### Requirement: El techo de llamadas es global del turno

El sistema SHALL contar las llamadas al modelo de un turno contra un único techo, y MUST NOT repartir el techo por capa.

#### Scenario: El total nunca supera el techo configurado

- **GIVEN** un turno que intenta más llamadas que su techo
- **WHEN** pide la que excede
- **THEN** se le niega, y el total de llamadas emitidas es igual al techo

#### Scenario: El techo agotado resuelve como servicio degradado

- **GIVEN** un turno que agotó su techo de llamadas
- **WHEN** termina
- **THEN** resuelve como servicio degradado

### Requirement: Los topes del turno son verificables en el resultado

El sistema SHALL exponer en el resultado del turno cuántas llamadas al modelo consumió.

#### Scenario: El resultado reporta el consumo real

- **GIVEN** un turno que consumió dos llamadas
- **WHEN** se inspecciona su resultado
- **THEN** informa dos
