## ADDED Requirements

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

## REMOVED Requirements

### Requirement: Opciones y sugerencias son campos separados

**Reason**: The suggestions field is removed from the turn response (ARS-140, ARS-149); there is no second field left to keep separate.
**Migration**: See "Only a clarification carries options, and no turn carries suggestions". Clients read clarification options from `opciones`, unchanged.

### Requirement: Todo rechazo trae al menos una sugerencia accionable

**Reason**: Refusals no longer carry suggestion chips by product decision (ARS-140, ARS-149; ARS-139 adjusted accordingly). A cooperative refusal is carried by its text alone.
**Migration**: None for clients; the refusal text is unchanged. The evaluation rule that required suggestions is replaced in `asistente-eje-social`.

### Requirement: A successfully answered turn may suggest related, executable follow-up questions

**Reason**: Follow-up suggestions after an answer are removed (ARS-149).
**Migration**: None; the welcome screen keeps the executable examples from the capabilities catalog.
