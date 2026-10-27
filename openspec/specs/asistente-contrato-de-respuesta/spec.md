# asistente-contrato-de-respuesta Specification

## Purpose

Defines the response contract of an assistant turn: what the client may rely on (areas the data comes from, feedback token, conversation identifier, options only for clarifications) and what is never exposed (internal labels, and the generated query unless the actor has permission).

## Requirements

### Requirement: La consulta generada solo se expone con permiso

El sistema SHALL incluir la consulta generada en la respuesta únicamente cuando el actor tenga el permiso correspondiente, y MUST NOT incluirla en ningún otro caso.

#### Scenario: Sin el permiso, la consulta no viaja

- **GIVEN** un actor sin el permiso de ver la consulta
- **WHEN** hace una pregunta que se responde
- **THEN** la respuesta no contiene ninguna consulta

#### Scenario: Con el permiso, la consulta viaja

- **GIVEN** un actor con el permiso de ver la consulta
- **WHEN** hace una pregunta que se responde
- **THEN** la respuesta contiene la consulta que se ejecutó

#### Scenario: El permiso no se concede a ningún rol por omisión

- **GIVEN** una base recién migrada
- **WHEN** se consultan los roles que tienen el permiso de ver la consulta
- **THEN** no lo tiene ninguno

### Requirement: La respuesta no expone etiquetas internas

El sistema MUST NOT incluir en ningún campo visible al usuario identificadores internos, nombres de excepción, ni mensajes crudos del motor o del proveedor.

#### Scenario: Un fallo del motor se muestra como texto comprensible

- **GIVEN** una consulta que el motor rechaza
- **WHEN** el usuario lee la respuesta
- **THEN** no contiene el nombre de ninguna tabla, columna ni código de error

### Requirement: A turn that produced feedback-eligible output returns a feedback token

The system SHALL include, in the response of a turn that ended `respondida`,
a feedback token that identifies that turn's analytic record for the sole
purpose of submitting feedback (see `asistente-retroalimentacion`). The
system MUST NOT include a feedback token in a response for any other state,
and MUST NOT derive the token from, or expose alongside it, any actor
identifier.

#### Scenario: An answered turn's response carries a feedback token

- **GIVEN** a turn that ended as an answer
- **WHEN** the client reads the response
- **THEN** it contains a feedback token distinct from the thread identifier

#### Scenario: A non-answered turn's response carries no feedback token

- **GIVEN** a turn that ended needing clarification, not-answerable, or degraded
- **WHEN** the client reads the response
- **THEN** it contains no feedback token

### Requirement: La respuesta dice de qué áreas salen sus datos

El sistema SHALL acompañar cada respuesta que ejecutó una consulta con las áreas de datos que esa consulta leyó, derivadas de los schemas que cita la consulta validada.

El sistema MUST NOT incluir en ese dato la consulta, sus tablas ni la cantidad de filas.

#### Scenario: Una consulta sobre pedidos nombra Designaciones

- **GIVEN** una consulta validada que lee `designaciones.pedidos` e `identity.materias`
- **WHEN** se arma la respuesta
- **THEN** la respuesta nombra las áreas `designaciones` e `identity`, y nada más sobre la consulta

#### Scenario: Una respuesta sin consulta no nombra áreas

- **GIVEN** un turno que termina en aclaración, sin ejecutar consulta
- **WHEN** se arma la respuesta
- **THEN** la respuesta no nombra ninguna área

### Requirement: Only a clarification carries options, and no turn carries suggestions

The system SHALL expose the options of a clarification in their own field, empty for every
other outcome, and MUST NOT include a suggestions field or any list of suggested questions
in the response of any turn — answered, not answerable, clarification, degraded, social
or meta-question. The capabilities catalog SHALL remain the only source of clickable
example questions, for the welcome screen.

#### Scenario: A clarification carries options

- **GIVEN** a turn that ended needing a clarification
- **WHEN** the client reads the response
- **THEN** it carries the options and no suggestions field

#### Scenario: A refusal carries no suggestions

- **GIVEN** a turn that ended not answerable
- **WHEN** the client reads the response
- **THEN** it carries neither options nor any suggestions field

#### Scenario: An answer carries no follow-up suggestions

- **GIVEN** a turn that ended answered
- **WHEN** the client reads the response
- **THEN** it carries no suggestions field

#### Scenario: The meta-question lists examples in its text only

- **GIVEN** an actor asking what the assistant can do
- **WHEN** the client reads the response
- **THEN** the examples appear in the answer text and the response carries no suggestions field

### Requirement: A persisted turn's response names its conversation

The system SHALL include in a turn's response the identifier of the persisted
conversation the turn was recorded in, so the owner's interface can highlight and title
it, and SHALL omit it when the turn was not persisted. This identifier MUST NOT be
written next to the feedback token or the analytic record anywhere.

#### Scenario: An answered turn names its conversation

- **GIVEN** a first turn of a new conversation that was persisted to history
- **WHEN** the client reads the response
- **THEN** it carries the conversation identifier that the actor's history list shows for it

#### Scenario: The identifier is never stored with the analytic record

- **GIVEN** a persisted answered turn
- **WHEN** the analytic record and the feedback table are inspected
- **THEN** neither contains the conversation identifier
