## MODIFIED Requirements

### Requirement: Los siete casos de abstención

El sistema SHALL abstenerse en los siete casos definidos: esquema que no cubre la pregunta, choque de valores, resultado vacío con actor no global, resultado truncado, consulta rechazada por el validador, proveedor caído o cuota agotada, y dato existente sin permiso. Una abstención MUST NOT acompañarse de sugerencias de preguntas: el rechazo se explica con su texto.

#### Scenario: El esquema no cubre la pregunta

- **GIVEN** una pregunta sobre datos que el asistente no puede leer
- **WHEN** termina el turno
- **THEN** resuelve como no contestable, con su texto y sin sugerencias

#### Scenario: La consulta rechazada no se reintenta a ciegas

- **GIVEN** una consulta rechazada por el validador
- **WHEN** termina el turno
- **THEN** resuelve como no contestable sin volver a generar

#### Scenario: El proveedor caído resuelve degradado

- **GIVEN** un proveedor que falla en todos sus intentos
- **WHEN** termina el turno
- **THEN** resuelve como servicio degradado
