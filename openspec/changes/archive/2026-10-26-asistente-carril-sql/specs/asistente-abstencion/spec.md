## ADDED Requirements

### Requirement: Los siete casos de abstención

El sistema SHALL abstenerse en los siete casos definidos: esquema que no cubre la pregunta, choque de valores, resultado vacío con actor no global, resultado truncado, consulta rechazada por el validador, proveedor caído o cuota agotada, y dato existente sin permiso.

#### Scenario: El esquema no cubre la pregunta

- **GIVEN** una pregunta sobre datos que el asistente no puede leer
- **WHEN** termina el turno
- **THEN** resuelve como no contestable y acompaña sugerencias

#### Scenario: La consulta rechazada no se reintenta a ciegas

- **GIVEN** una consulta rechazada por el validador
- **WHEN** termina el turno
- **THEN** resuelve como no contestable sin volver a generar

#### Scenario: El proveedor caído resuelve degradado

- **GIVEN** un proveedor que falla en todos sus intentos
- **WHEN** termina el turno
- **THEN** resuelve como servicio degradado

### Requirement: Distinción entre resultado vacío y falta de permiso

Antes de gastar el reintento de generación, el sistema MUST consultar si el alcance del actor es global.

Si el actor **no** es global, un resultado vacío MUST NOT gastar el reintento.

Si el actor es global, el comportamiento del reintento MUST ser el mismo que en el caso base.

#### Scenario: Un actor acotado no gasta el reintento

- **GIVEN** un actor de alcance de carrera y una consulta que devuelve cero filas
- **WHEN** termina el turno
- **THEN** se consumió una sola llamada de generación

#### Scenario: Un actor global conserva el reintento

- **GIVEN** un actor de alcance global y una consulta que devuelve cero filas
- **WHEN** termina el turno
- **THEN** el reintento se comporta igual que en el caso base

#### Scenario: Falta de permiso no se narra como inexistencia

- **GIVEN** un dato que existe pero que el actor no puede ver
- **WHEN** termina el turno
- **THEN** la respuesta dice que no hay acceso y no que no hay datos

### Requirement: El guard reconoce las dos formas del vacío

El guard de resultado MUST reconocer como vacío tanto un resultado de cero filas como un resultado de una única fila con todos sus valores nulos.

> Una agregación sobre cero filas devuelve una fila con nulos, no cero filas.

#### Scenario: Cero filas es vacío

- **GIVEN** una consulta que devuelve cero filas
- **WHEN** se evalúa el guard
- **THEN** el resultado se considera vacío

#### Scenario: Una fila de nulos es vacío

- **GIVEN** una agregación sobre un conjunto vacío que devuelve una fila con todos los valores nulos
- **WHEN** se evalúa el guard
- **THEN** el resultado se considera vacío

#### Scenario: Una fila con un cero no es vacío

- **GIVEN** un conteo sobre un conjunto vacío que devuelve una fila con el valor cero
- **WHEN** se evalúa el guard
- **THEN** el resultado no se considera vacío

#### Scenario: Una fila con algún valor no nulo no es vacío

- **GIVEN** un resultado de una fila con al menos un valor no nulo
- **WHEN** se evalúa el guard
- **THEN** el resultado no se considera vacío

### Requirement: Nunca se declara cuántas filas quedaron afuera

Ninguna respuesta MUST mencionar cuántas filas quedaron fuera del alcance del actor ni fuera del recorte. El indicador de truncado MUST ser un booleano y no un número.

#### Scenario: La respuesta truncada no da un total

- **GIVEN** un resultado truncado
- **WHEN** se inspecciona la respuesta
- **THEN** no afirma ningún conteo total

#### Scenario: La respuesta acotada no revela el tamaño del conjunto

- **GIVEN** un actor de alcance acotado
- **WHEN** se inspecciona la respuesta
- **THEN** no menciona cuántas filas existen fuera de su alcance

### Requirement: Las reglas de abstención están en el prompt de redacción

El prompt de redacción MUST prohibir afirmar inexistencia cuando el actor no es global, y MUST agregar el marco de alcance.

El prompt de redacción MUST prohibir afirmar conteos cuando el resultado está truncado.

#### Scenario: El marco de alcance aparece

- **GIVEN** un turno con actor no global
- **WHEN** se inspecciona el prompt de redacción
- **THEN** contiene la prohibición de afirmar inexistencia y el marco de alcance

#### Scenario: La prohibición de conteo aparece con truncado

- **GIVEN** un turno con resultado truncado
- **WHEN** se inspecciona el prompt de redacción
- **THEN** contiene la prohibición de afirmar conteos
