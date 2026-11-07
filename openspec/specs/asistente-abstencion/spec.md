# asistente-abstencion Specification

## Purpose

Defines when the assistant abstains instead of answering: the closed set of abstention cases, the difference between an empty result and a missing permission, and the guard and prompt rules that stop it from inventing data or revealing how many rows were left out.

## Requirements

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

### Requirement: El servicio degradado se decide antes del pipeline

El sistema SHALL determinar la disponibilidad del modelo antes de iniciar el turno, y SHALL seguir ejecutando los pasos que no necesitan proveedor aunque el modelo no esté disponible.

El sistema MUST NOT abortar el turno completo por falta de modelo.

#### Scenario: El veredicto se toma una sola vez por turno

- **GIVEN** un turno que empieza con el modelo disponible
- **WHEN** el proveedor se cae a mitad del turno
- **THEN** el turno resuelve como servicio degradado y no reintenta indefinidamente

#### Scenario: Los pasos deterministas corren igual

- **GIVEN** un turno que empieza sin modelo disponible
- **WHEN** el mensaje se resuelve por un paso que no necesita proveedor
- **THEN** el turno responde normalmente y no como servicio degradado

#### Scenario: El estado degradado se distingue del no contestable

- **GIVEN** un turno resuelto como servicio degradado
- **WHEN** el cliente lee el estado
- **THEN** es distinguible de «no contestable», que significa que la pregunta no se puede responder nunca

### Requirement: Model-declared refusals are rendered from per-reason templates

When the generation declares a question not answerable, the refusal text SHALL be built by a template selected by the declared reason (`fuera_de_tema`, `otro_sistema`, `muy_general`, `no_cubierto`). The text MUST NOT contain any text produced by the model other than a validated term (see "The user's term is echoed only when it is the user's own words"); in particular the model's reasoning MUST NOT be part of the refusal body. A reason outside the closed set, a missing reason, an unparseable generation and a generation cut by the token ceiling SHALL all render the `no_cubierto` template. The first `no_cubierto` variant without a term SHALL be the pre-existing generic text «No puedo responder eso con la información que tengo disponible.».

#### Scenario: Each reason has its own text

- **GIVEN** four generations declaring the question not answerable, one per reason, with no term
- **WHEN** each turn ends
- **THEN** the four refusal texts are pairwise different

#### Scenario: An unknown reason falls back to not covered

- **GIVEN** a generation declaring the question not answerable with the reason `desconocido`
- **WHEN** the turn ends
- **THEN** the refusal text is the `no_cubierto` template

#### Scenario: The model's reasoning never reaches the refusal body

- **GIVEN** a generation declaring the question not answerable with a reasoning sentence
- **WHEN** the turn ends
- **THEN** the refusal text does not contain that sentence

#### Scenario: The generic text is preserved for the plain case

- **GIVEN** a first refusal in a conversation, reason `no_cubierto`, no valid term
- **WHEN** the turn ends
- **THEN** the refusal text is «No puedo responder eso con la información que tengo disponible.»

### Requirement: The user's term is echoed only when it is the user's own words

A template MAY include the term that names what could not be answered. The term SHALL be shown only if it is a span of the message the user typed in this turn — not of the rewritten question — matched case-insensitively and accent-insensitively, starting and ending on word boundaries, between 2 and 40 characters, at most 4 words, without line breaks, guillemets or double quotes, and not made only of demonstratives or stopwords. The characters shown MUST be the user's, copied from the message, not the model's. When any condition fails the template SHALL render its variant without the term. The term MUST NOT be logged nor persisted.

#### Scenario: A verbatim term is echoed with the user's spelling

- **GIVEN** the user typed «python» and the generation returned the term «Python»
- **WHEN** the refusal is rendered
- **THEN** the text contains «python» exactly as the user typed it

#### Scenario: An accent-insensitive match is accepted

- **GIVEN** the user typed «calificacion» and the generation returned the term «calificación»
- **WHEN** the refusal is rendered
- **THEN** the text contains «calificacion»

#### Scenario: A term the user never typed is dropped

- **GIVEN** the user typed «¿cuánto cobran?» and the generation returned the term «salarios docentes»
- **WHEN** the refusal is rendered
- **THEN** the text uses the variant without a term

#### Scenario: A term taken from the rewritten question is dropped

- **GIVEN** a follow-up whose rewritten question contains a word the user did not type in this turn, and a generation that returns that word as the term
- **WHEN** the refusal is rendered
- **THEN** the text uses the variant without a term

#### Scenario: An over-long term is dropped

- **GIVEN** a generation whose term is a verbatim span of the message but has more than 4 words
- **WHEN** the refusal is rendered
- **THEN** the text uses the variant without a term

### Requirement: Consultable areas come from the actor's capability catalog

When a template names what the assistant can consult, the list SHALL be derived from the same capability catalog that answers «¿qué podés hacer?» for that actor (areas derived from effective read privileges), rendered with human labels — never schema, table or column names. `otro_sistema` templates SHALL name only the external systems listed in the assistant's fixed limits (Guaraní, planillas). If the catalog cannot be read, the template SHALL render without the areas and the turn MUST NOT fail.

#### Scenario: Areas are human labels

- **GIVEN** an actor whose catalog covers designation, subject and career tables
- **WHEN** a refusal that names areas is rendered
- **THEN** the text names them with human labels and contains no schema, table or column name

#### Scenario: Another system is named only from the fixed list

- **GIVEN** a generation declaring `otro_sistema`
- **WHEN** the refusal is rendered
- **THEN** every system named in the text belongs to the assistant's fixed limits

#### Scenario: A catalog failure does not break the refusal

- **GIVEN** a capability catalog that fails when read
- **WHEN** a refusal that would name areas is rendered
- **THEN** the turn resolves as not answerable with a text without areas

### Requirement: A refusal never asserts absence nor a capability it lacks

No template SHALL assert that data does not exist or was not found, because no query ran. No template SHALL claim a capability beyond the capability catalog and the assistant's fixed limits. A template that points to more help SHALL point to the meta-question «¿qué podés hacer?», which the zero-token social lane answers.

#### Scenario: No template claims absence

- **WHEN** every template variant is rendered with and without term and areas
- **THEN** none contains «no hay», «no existe», «no encontré», «ningún» or «nadie»

#### Scenario: The help pointer is answerable

- **GIVEN** the meta-question quoted by the templates
- **WHEN** it is sent as a turn
- **THEN** it is answered by the capability catalog without calling the model

### Requirement: Refusal wording escalates within a conversation

The variant of a template SHALL depend on how many earlier turns of the same conversation ended not answerable: the first refusal uses variant 1; later refusals alternate between variants 2 and 3, which add the consultable areas and the pointer to «¿qué podés hacer?». The count SHALL come from the server-side conversation state owned by the actor — including turns seeded from the persisted history when a conversation is resumed — and MUST NOT be taken from the client request. Two consecutive not-answerable turns in the same conversation MUST NOT show the same text. A turn that replaces the last question does not count the turn it replaces.

#### Scenario: A repeated refusal changes its wording

- **GIVEN** a conversation whose previous turn was refused with reason `fuera_de_tema`
- **WHEN** the next question is refused with the same reason
- **THEN** its text differs from the previous one and names the consultable areas

#### Scenario: A long run of refusals never repeats back to back

- **GIVEN** five consecutive refused turns in one conversation with the same reason and term
- **WHEN** their texts are compared pairwise in order
- **THEN** no text equals the one right before it

#### Scenario: A resumed conversation keeps its refusal count

- **GIVEN** a persisted conversation whose last turn was not answerable, resumed from the history
- **WHEN** the next question is refused
- **THEN** its text is an escalated variant

#### Scenario: The client cannot reset the count

- **GIVEN** a conversation with an earlier refusal and a request that sends no history
- **WHEN** the next question is refused
- **THEN** its text is an escalated variant

#### Scenario: A fresh conversation starts at the first variant

- **GIVEN** a new conversation
- **WHEN** its first question is refused
- **THEN** its text is the first variant of the reason's template

### Requirement: Privacy-sensitive and non-model refusals keep their generic text

An engine permission rejection, an engine execution error, a validator rejection, an empty result, an unresolved follow-up reference and an exhausted clarification SHALL keep their existing fixed texts and MUST NOT use the per-reason templates, so that no refusal reveals whether data exists outside the actor's reach.

#### Scenario: A permission rejection stays generic

- **GIVEN** a query the engine rejects for lack of privilege
- **WHEN** the turn ends
- **THEN** the text is the existing no-access text and names no area or term

#### Scenario: An empty result is not a template refusal

- **GIVEN** a query that returns zero rows for a non-global actor
- **WHEN** the turn ends
- **THEN** the text is the existing empty-result text for that actor's scope

#### Scenario: An unresolved follow-up keeps its specific text

- **GIVEN** a refused follow-up whose rewritten question still contains an unresolved demonstrative
- **WHEN** the turn ends
- **THEN** the text is the existing unresolved-reference text
