## ADDED Requirements

### Requirement: El endpoint del turno exige el permiso del asistente

El sistema SHALL exponer el turno en `POST /api/asistente/consultas` y SHALL exigir el permiso de consulta del asistente.

#### Scenario: Sin el permiso se rechaza

- **GIVEN** un usuario autenticado sin el permiso del asistente
- **WHEN** hace un pedido al endpoint
- **THEN** el sistema lo rechaza sin procesar la pregunta

#### Scenario: Sin autenticar se rechaza

- **WHEN** un pedido llega sin identidad
- **THEN** el sistema lo rechaza

### Requirement: La clave de idempotencia es obligatoria

El sistema SHALL exigir una clave de idempotencia en cada pedido del turno y SHALL rechazar el pedido que no la traiga, con un error que diga qué falta.

#### Scenario: Sin la clave el pedido se rechaza

- **WHEN** llega un pedido sin clave de idempotencia
- **THEN** el sistema lo rechaza y el mensaje nombra la cabecera faltante

#### Scenario: Una clave vacía no cuenta como clave

- **WHEN** llega un pedido con la clave en blanco
- **THEN** el sistema lo rechaza

### Requirement: Un pedido repetido no vuelve a llamar al proveedor

El sistema SHALL devolver la respuesta ya calculada cuando llegue un pedido con una clave de idempotencia ya vista, y MUST NOT emitir ninguna llamada al proveedor del modelo.

#### Scenario: El segundo pedido no cuesta llamadas

- **GIVEN** un pedido ya resuelto con una clave
- **WHEN** llega otro pedido con la misma clave y el mismo actor
- **THEN** devuelve la misma respuesta y el proveedor no recibe ninguna llamada

#### Scenario: Una clave distinta sí procesa

- **GIVEN** un pedido ya resuelto
- **WHEN** llega otro pedido con otra clave
- **THEN** el turno se procesa normalmente

### Requirement: La clave de idempotencia se acota por actor

El sistema SHALL asociar cada clave de idempotencia al actor que la usó, y la clave de un actor MUST NOT devolver la respuesta calculada para otro.

#### Scenario: La clave de un usuario no sirve para otro

- **GIVEN** un pedido resuelto por un actor con una clave
- **WHEN** otro actor manda un pedido con la misma clave
- **THEN** su turno se procesa por separado y no recibe la respuesta del primero

### Requirement: La idempotencia no persiste nada

El sistema SHALL resolver la idempotencia en memoria con expiración corta, y MUST NOT persistir ninguna fila para hacerlo.

#### Scenario: Al expirar la clave, el turno se vuelve a procesar

- **GIVEN** un pedido resuelto con una clave
- **WHEN** pasa el período de expiración y llega otro pedido con esa clave
- **THEN** el turno se procesa de nuevo

#### Scenario: No hay tabla de idempotencia del asistente

- **GIVEN** la base migrada
- **WHEN** se inspeccionan las tablas del schema del asistente
- **THEN** ninguna guarda claves de idempotencia ni cuerpos de respuesta
