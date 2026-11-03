## ADDED Requirements

### Requirement: Redacción en español sobre las filas devueltas

El sistema SHALL redactar la respuesta en español en una segunda llamada al modelo, sobre las filas efectivamente devueltas.

La llamada MUST usar temperatura baja pero no cero. MUST NOT usar el prefijo cacheado: el prompt de redacción es distinto en cada turno por definición.

La redacción MUST NOT introducir valores que no estén en las filas.

#### Scenario: La redacción sale en español

- **GIVEN** un resultado con filas
- **WHEN** termina el turno
- **THEN** la respuesta está redactada en español

#### Scenario: La temperatura de redacción no es cero

- **WHEN** se inspecciona la solicitud de redacción
- **THEN** su temperatura es mayor que cero y menor que la unidad

#### Scenario: Las filas llegan a la redacción

- **GIVEN** un resultado con filas
- **WHEN** se inspecciona el prompt de redacción
- **THEN** contiene los valores de esas filas

### Requirement: La redacción respeta las reglas de abstención

La redacción MUST NOT afirmar inexistencia cuando el actor no es global.

La redacción MUST NOT afirmar conteos cuando el resultado está truncado.

#### Scenario: Con actor acotado no se afirma inexistencia

- **GIVEN** un turno con actor no global y resultado vacío
- **WHEN** se inspecciona el resultado del turno
- **THEN** la respuesta encuadra el vacío en el alcance del actor

#### Scenario: Con truncado no se afirma un total

- **GIVEN** un turno con resultado truncado
- **WHEN** se inspecciona el resultado del turno
- **THEN** la respuesta no afirma un total

### Requirement: La pregunta interpretada se devuelve cuando difiere

Cuando la pregunta que el sistema interpretó difiere del mensaje del usuario, MUST devolverse para que el usuario la vea.

#### Scenario: Difiere y se devuelve

- **GIVEN** un turno cuya pregunta interpretada difiere del mensaje original
- **WHEN** termina el turno
- **THEN** el resultado incluye la pregunta interpretada

#### Scenario: Coincide y no se repite

- **GIVEN** un turno cuya pregunta interpretada coincide con el mensaje original
- **WHEN** termina el turno
- **THEN** el resultado no incluye una pregunta interpretada redundante

### Requirement: Transparencia media

El razonamiento MUST exponerse tal como lo devolvió la generación.

El sistema MUST NOT generar ninguna explicación adicional del razonamiento.

#### Scenario: El razonamiento se expone sin agregados

- **GIVEN** una generación con razonamiento
- **WHEN** termina el turno
- **THEN** el resultado expone ese razonamiento y no consume una llamada extra al modelo para explicarlo

### Requirement: Los rechazos no hablan de esquema ni de SQL

El texto de una respuesta de abstención o de rechazo MUST NOT mencionar esquemas, tablas, columnas ni SQL, ni exponer errores crudos del motor o del proveedor.

#### Scenario: El rechazo del validador no nombra la SQL

- **GIVEN** un turno cortado por el validador
- **WHEN** se inspecciona el texto de la respuesta
- **THEN** no contiene la consulta ni nombres de tablas o columnas

#### Scenario: El error del motor no se filtra

- **GIVEN** un turno en el que la ejecución falla con un error del motor
- **WHEN** se inspecciona el texto de la respuesta
- **THEN** no contiene el mensaje crudo del motor

#### Scenario: La pregunta no contestable no enumera el esquema

- **GIVEN** un turno cortado por pregunta no contestable
- **WHEN** se inspecciona el texto de la respuesta
- **THEN** no enumera qué tablas o columnas existen
