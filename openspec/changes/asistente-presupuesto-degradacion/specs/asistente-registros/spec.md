## ADDED Requirements

### Requirement: Existen dos registros y ninguno contiene los campos del otro

El sistema SHALL registrar cada turno en un registro operativo y en un registro analítico separados.

El registro operativo SHALL guardar el actor, el momento, el carril, el estado del resultado, las llamadas al modelo, los tokens, la latencia, si hubo reintento y si hubo truncado.

El registro analítico SHALL guardar el texto de la pregunta, su categoría y el estado del resultado.

#### Scenario: El operativo no guarda el texto de la pregunta

- **GIVEN** un turno registrado
- **WHEN** se inspecciona el registro operativo
- **THEN** no contiene el texto de la pregunta

#### Scenario: El analítico no guarda el actor

- **GIVEN** un turno registrado
- **WHEN** se inspecciona el registro analítico
- **THEN** no contiene ningún identificador de actor

### Requirement: El registro analítico redondea la fecha al día

El sistema SHALL guardar en el registro analítico la fecha del turno con precisión de día, y MUST NOT guardar la hora.

#### Scenario: Dos turnos del mismo día son indistinguibles en el tiempo

- **GIVEN** dos turnos de actores distintos ocurridos con segundos de diferencia
- **WHEN** se inspeccionan sus filas analíticas
- **THEN** las dos tienen el mismo valor de fecha

#### Scenario: Cruzar los dos registros no reconstruye quién preguntó qué

- **GIVEN** varios turnos de varios actores en el mismo día
- **WHEN** se intenta asociar cada pregunta a su actor cruzando ambos registros
- **THEN** no hay ninguna columna compartida que lo permita

### Requirement: Ningún registro guarda filas ni consultas

El sistema MUST NOT guardar en ninguno de los dos registros las filas devueltas por una consulta ni la consulta generada.

#### Scenario: Un turno con filas sensibles no deja rastro de ellas

- **GIVEN** un turno que devolvió filas con columnas sensibles
- **WHEN** se inspeccionan los dos registros
- **THEN** ninguno contiene ningún valor de ninguna fila

#### Scenario: La consulta generada no queda persistida

- **GIVEN** un turno que generó y ejecutó una consulta
- **WHEN** se inspeccionan los dos registros
- **THEN** ninguno contiene el texto de la consulta

### Requirement: Los registros no se enganchan a la auditoría

La migración de los registros MUST NOT aplicarles el enganche de auditoría, y SHALL dejar el motivo declarado en el propio archivo.

#### Scenario: Ninguna de las dos tablas tiene disparador de auditoría

- **GIVEN** la base migrada
- **WHEN** se inspeccionan los disparadores de las dos tablas
- **THEN** ninguna tiene el disparador de auditoría

#### Scenario: Escribir un turno no deja fila en la bitácora

- **GIVEN** la base migrada
- **WHEN** se registra un turno
- **THEN** la bitácora de auditoría no crece

### Requirement: Los registros los escribe la conexión dueña

El sistema SHALL escribir los dos registros con la conexión dueña, y MUST NOT usar para eso ninguna conexión de solo lectura.

#### Scenario: El rol de solo lectura no puede escribirlos

- **GIVEN** la base migrada
- **WHEN** el rol de solo lectura intenta insertar en cualquiera de los dos registros
- **THEN** el motor lo rechaza

### Requirement: Un fallo al registrar no hace fallar el turno

El sistema SHALL responder el turno aunque el registro falle.

#### Scenario: Sin tablas de registro el turno responde igual

- **GIVEN** una base sin las tablas de registro
- **WHEN** un turno se resuelve
- **THEN** el usuario recibe su respuesta y el fallo del registro queda logueado

### Requirement: La purga borra lo que superó la ventana de retención

El sistema SHALL borrar automática y periódicamente las filas de los dos registros más viejas que la ventana de retención configurada.

#### Scenario: Lo viejo desaparece

- **GIVEN** filas en los dos registros más viejas que la ventana
- **WHEN** corre la purga
- **THEN** esas filas ya no están

#### Scenario: Lo reciente se conserva

- **GIVEN** filas dentro de la ventana
- **WHEN** corre la purga
- **THEN** esas filas siguen estando

#### Scenario: La purga es idempotente

- **GIVEN** dos registros sin filas que superen la ventana
- **WHEN** corre la purga dos veces seguidas
- **THEN** no falla ninguna de las dos veces
