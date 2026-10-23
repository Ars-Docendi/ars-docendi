## MODIFIED Requirements

### Requirement: Los siete casos de abstención

The system SHALL abstain in the seven defined cases: the schema does not cover the question, value collision, empty result with a non-global actor, truncated result, query rejected by the validator, provider down or quota exhausted, and existing data without permission. An abstention MUST NOT carry question suggestions: the refusal is explained by its text alone. When the refusal comes from the generation declaring the question not answerable, its text MUST be rendered from the per-reason templates defined in this capability; every other case keeps its own fixed text.

#### Scenario: El esquema no cubre la pregunta

- **GIVEN** a question about data the assistant cannot read
- **WHEN** the turn ends
- **THEN** it resolves as not answerable, with a per-reason template text and without suggestions

#### Scenario: La consulta rechazada no se reintenta a ciegas

- **GIVEN** a query rejected by the validator
- **WHEN** the turn ends
- **THEN** it resolves as not answerable with the validator's generic text, without generating again

#### Scenario: El proveedor caído resuelve degradado

- **GIVEN** a provider that fails in every attempt
- **WHEN** the turn ends
- **THEN** it resolves as degraded service

## ADDED Requirements

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
