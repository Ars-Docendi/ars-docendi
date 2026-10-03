## ADDED Requirements

### Requirement: Conexión y transacción nuevas por ejecución

Cada ejecución de una consulta generada MUST abrir una conexión y una transacción nuevas, incluido el caso del reintento.

La transacción MUST declararse de solo lectura.

#### Scenario: El reintento no reusa la transacción

- **GIVEN** un turno que ejecuta la consulta y luego reintenta
- **WHEN** se observan las transacciones abiertas
- **THEN** son dos transacciones distintas

#### Scenario: Una escritura dentro de la transacción es rechazada

- **WHEN** se intenta ejecutar una sentencia de escritura dentro de la transacción del carril
- **THEN** el motor la rechaza

### Requirement: El actor se fija transaction-local

El identificador del actor MUST fijarse con alcance de transacción, de modo que el ajuste muera al confirmar y no sobreviva a la devolución de la conexión al pool.

#### Scenario: El ajuste no sobrevive a la conexión

- **GIVEN** una ejecución que fijó un actor y terminó
- **WHEN** se reutiliza la misma conexión física del pool y se lee el ajuste
- **THEN** está vacío

#### Scenario: Un turno no hereda el actor del anterior

- **GIVEN** dos turnos consecutivos con actores distintos sobre el mismo pool
- **WHEN** el segundo ejecuta su consulta
- **THEN** el alcance aplicado es el del segundo actor

### Requirement: La identidad se resuelve del usuario autenticado

El valor del actor MUST ser el identificador de `identity.users` del usuario autenticado del sistema.

MUST NOT ser el identificador del proveedor de identidad externo. Ningún dato enviado por el cliente MAY determinarlo.

#### Scenario: El cliente no puede elegir el actor

- **GIVEN** una solicitud que incluye un identificador de actor en su cuerpo
- **WHEN** se ejecuta el turno
- **THEN** el actor aplicado es el del usuario autenticado y no el enviado

#### Scenario: Un identificador del proveedor externo no se acepta en silencio

- **GIVEN** el identificador del directorio externo de un usuario existente
- **WHEN** se lo intenta usar como actor
- **THEN** la ejecución falla de forma visible, en lugar de devolver un resultado vacío

### Requirement: Límite con fila sonda y detección de truncado

La consulta generada MUST envolverse en una consulta externa que pida **una fila más** que el tope de filas.

La fila sonda MUST descartarse antes de que el resultado salga del ejecutor. El indicador de truncado MUST llegar a la redacción.

El resultado MUST NOT llevar el total de filas que quedaron fuera del alcance.

#### Scenario: Un resultado por debajo del tope no se marca truncado

- **GIVEN** una consulta cuyo resultado tiene menos filas que el tope
- **WHEN** se ejecuta
- **THEN** el indicador de truncado está en falso y se devuelven todas las filas

#### Scenario: Un resultado por encima del tope se marca y se recorta

- **GIVEN** una consulta cuyo resultado supera el tope
- **WHEN** se ejecuta
- **THEN** se devuelven exactamente las filas del tope y el indicador de truncado está en verdadero

#### Scenario: Un resultado exactamente igual al tope no se marca

- **GIVEN** una consulta cuyo resultado tiene exactamente el número de filas del tope
- **WHEN** se ejecuta
- **THEN** el indicador de truncado está en falso

#### Scenario: La fila sonda no sale del ejecutor

- **GIVEN** un resultado truncado
- **WHEN** se cuentan las filas devueltas
- **THEN** son las del tope, nunca una más

#### Scenario: El resultado no expone cuántas filas quedaron afuera

- **WHEN** se inspecciona el resultado de una ejecución truncada
- **THEN** no contiene ningún conteo del total sin recortar

### Requirement: Timeouts de sentencia y de comando

El ejecutor MUST configurar un timeout de sentencia en la base y un timeout de comando en el cliente.

#### Scenario: Una consulta larga se corta

- **GIVEN** una consulta que excede el timeout configurado
- **WHEN** se ejecuta
- **THEN** se aborta y el turno resuelve por abstención en lugar de colgarse

#### Scenario: El timeout de sentencia está fijado en la transacción

- **WHEN** se inspecciona el ajuste de timeout de sentencia dentro de la transacción del carril
- **THEN** tiene el valor configurado y no el valor por omisión del servidor
