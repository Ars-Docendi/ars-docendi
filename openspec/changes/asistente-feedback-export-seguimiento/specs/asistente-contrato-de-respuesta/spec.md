## MODIFIED Requirements

### Requirement: Opciones y sugerencias son campos separados

El sistema SHALL exponer las opciones de una aclaración y las sugerencias de próximas preguntas en campos distintos, y MUST NOT usar un solo campo para las dos cosas. Las sugerencias SHALL poder estar presentes tanto en un turno respondido como en un rechazo, y SHALL estar siempre vacías en una aclaración.

#### Scenario: Una aclaración trae opciones y no sugerencias

- **GIVEN** un turno que terminó necesitando una aclaración
- **WHEN** el cliente lee la respuesta
- **THEN** trae opciones y el campo de sugerencias está vacío

#### Scenario: Un rechazo trae sugerencias y no opciones

- **GIVEN** un turno que terminó como no contestable
- **WHEN** el cliente lee la respuesta
- **THEN** trae sugerencias y el campo de opciones está vacío

#### Scenario: Un turno respondido puede traer sugerencias y no opciones

- **GIVEN** un turno que terminó respondido, con sugerencias de seguimiento relacionadas
- **WHEN** el cliente lee la respuesta
- **THEN** trae sugerencias y el campo de opciones está vacío

## ADDED Requirements

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

### Requirement: A successfully answered turn may suggest related, executable follow-up questions

The system SHALL, after a turn ends `respondida`, attempt to populate the
suggestions field with up to 3 questions from the verified example catalog
that share the answered turn's category and that the current actor can
execute. The system MUST NOT include a suggestion the actor cannot execute,
and MUST NOT populate the field with unrelated or filler content when no
catalog example qualifies.

#### Scenario: A successful answer with related, executable examples suggests them

- **GIVEN** a turn answered in a category for which the catalog has executable examples for this actor
- **WHEN** the client reads the response
- **THEN** the suggestions field contains up to 3 of those examples' questions

#### Scenario: No qualifying example means no suggestions, not a fallback

- **GIVEN** a turn answered in a category with no catalog example the actor can execute
- **WHEN** the client reads the response
- **THEN** the suggestions field is empty

#### Scenario: The just-answered question is never suggested back

- **GIVEN** a turn whose executed query is textually identical to a catalog example's query
- **WHEN** the client reads the response
- **THEN** that example is excluded from the suggestions

#### Scenario: A suggestion an actor's privileges could not execute is never offered

- **GIVEN** a catalog example in the answered turn's category that the current actor's privileges would reject
- **WHEN** the client reads the response
- **THEN** that example is not among the suggestions
