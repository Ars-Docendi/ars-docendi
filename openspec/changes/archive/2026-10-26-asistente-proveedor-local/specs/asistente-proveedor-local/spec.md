## ADDED Requirements

### Requirement: Adaptador para un servidor local OpenAI-compatible

El sistema SHALL poder usar como proveedor del modelo un servidor OpenAI-compatible (vLLM, llama-server, SGLang) configurado por URL. El prefijo estable SHALL viajar como mensaje de sistema antes del mensaje variable. El razonamiento del modelo SHALL quedar apagado salvo con esfuerzo alto o máximo, y el texto devuelto MUST NOT incluir bloques de razonamiento.

#### Scenario: El prefijo estable va primero y es idéntico entre turnos

- **GIVEN** el proveedor `local`
- **WHEN** se hacen dos llamadas con el mismo prefijo y distintas preguntas
- **THEN** el primer mensaje de las dos es el de sistema con el mismo contenido, y la pregunta viaja sólo en el mensaje de usuario

#### Scenario: El razonamiento se apaga con esfuerzo medio

- **GIVEN** una llamada con esfuerzo `medio`
- **WHEN** el adaptador arma el request
- **THEN** pide `enable_thinking: false`

#### Scenario: Los tokens de caché se informan aparte

- **GIVEN** un servidor que informa `cached_tokens` en el uso
- **WHEN** responde
- **THEN** la respuesta del puerto trae esos tokens como tokens de caché, incluidos en los de entrada

### Requirement: Salida estructurada en la generación de SQL

La generación de SQL SHALL declarar la forma JSON de su respuesta, y el adaptador local SHALL pedirla al servidor como salida restringida por esquema. El adaptador de Anthropic MUST NOT cambiar su request por esa declaración.

#### Scenario: El adaptador local pide salida con esquema

- **GIVEN** una solicitud con esquema de salida
- **WHEN** el adaptador local arma el request
- **THEN** el request lleva `response_format` de tipo `json_schema` con ese esquema

### Requirement: Concurrencia acotada hacia el modelo

El sistema SHALL poder limitar las llamadas simultáneas al modelo y encolar las demás con una espera máxima. La espera en cola MUST NOT contar contra el timeout de la llamada ni como fallo del proveedor. Una llamada que no consigue lugar a tiempo SHALL resolver el turno como degradación por saturación, con un texto distinto del de proveedor caído.

#### Scenario: La cola no abre el breaker

- **GIVEN** un límite de 1 llamada concurrente y una llamada en curso
- **WHEN** otras llamadas esperan más que la espera máxima
- **THEN** se resuelven como saturación y el breaker sigue cerrado

#### Scenario: Un turno ya empezado tiene prioridad

- **GIVEN** la compuerta llena, con una llamada de un turno nuevo y otra de un turno que ya hizo una llamada esperando
- **WHEN** se libera un lugar
- **THEN** lo toma la llamada del turno ya empezado

### Requirement: La falla del reescritor no tira el turno

Si la llamada que reescribe un seguimiento falla por el proveedor (no disponible, saturado, timeout o transporte), el turno SHALL continuar con la pregunta tal como la escribió el usuario.

#### Scenario: Reescritura con el proveedor saturado

- **GIVEN** un seguimiento con historial y el proveedor saturado para la reescritura
- **WHEN** el usuario envía el turno
- **THEN** el turno sigue con la pregunta cruda en lugar de terminar en error
