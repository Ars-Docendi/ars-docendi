## Purpose

Lets a user name a subject («@») or a teacher («#») unambiguously while writing a
question, choosing from entities within their own profile's reach, so the SQL lane
filters by that exact entity instead of guessing from free text.

## ADDED Requirements

### Requirement: A scoped search returns subjects and teachers within the actor's reach

The system SHALL expose a search endpoint, gated by the assistant usage permission, that
returns subjects or teachers matching a term of at least 2 characters. Subjects SHALL be
limited to those the actor can see according to the database's own scope function for
the assistant actor; teachers SHALL be limited to people with at least one designation
visible to the actor under the database's row-level security. The search SHALL run on the
assistant's basic read-only role with the actor fixed for the transaction, SHALL return
at most 6 results plus a flag stating whether more exist, and MUST NOT return any column
classified as personal data. A term shorter than 2 characters SHALL be rejected with
`400`.

#### Scenario: A career-scoped actor only finds subjects of their careers

- **GIVEN** an actor whose scope is the career Ingeniería Informática, and a subject "Álgebra" that exists only in Ingeniería Industrial
- **WHEN** they search subjects for "alg"
- **THEN** "Álgebra" of Ingeniería Industrial is not among the results

#### Scenario: A teacher outside the actor's reach is never returned

- **GIVEN** a teacher designated only in subjects outside the actor's scope
- **WHEN** the actor searches teachers by that teacher's surname
- **THEN** the teacher is not among the results, and the response is indistinguishable from one for a surname nobody has

#### Scenario: An actor without designation visibility finds no teachers

- **GIVEN** an actor whose permissions do not include seeing designations
- **WHEN** they search teachers for any term
- **THEN** the results are empty

#### Scenario: More matches than shown are flagged, never counted

- **GIVEN** 9 subjects in scope matching "an"
- **WHEN** the actor searches subjects for "an"
- **THEN** 6 results are returned with the "more exist" flag set, and no total count is returned

#### Scenario: No personal-data column leaves the search

- **GIVEN** any search result
- **WHEN** its fields are inspected
- **THEN** none of them is a column the sensitivity manifest classifies as `sensible-valor` or `sensible-texto`

### Requirement: A turn may carry structured references, validated against the actor's reach

The system SHALL accept on the turn endpoint an optional list of at most 5 references,
each naming a type (`materia` or `docente`) and an entity identifier. Before resolving the
turn, the system SHALL re-validate every reference against the actor's current reach with
the same scoping as the search. A reference that is unknown or outside the actor's reach
SHALL make the request fail with the same `400` response in both cases, before any model
call, quota charge or history write. The system MUST NOT reveal whether a rejected
reference exists.

#### Scenario: An out-of-scope reference is rejected like an unknown one

- **GIVEN** one request referencing a teacher outside the actor's reach and another referencing an identifier that does not exist
- **WHEN** both are sent
- **THEN** both receive the same `400` response and neither calls the model or charges quota

#### Scenario: Too many references are rejected

- **GIVEN** a request with 6 references
- **WHEN** it is sent
- **THEN** it is rejected with `400` before resolving the turn

### Requirement: The SQL lane filters by each reference exactly, without sending internal identifiers to the model

The system SHALL tell the SQL generator, for each reference, which entity it is and that
it must be filtered by a reserved per-turn marker; the system SHALL bind each marker to the
entity's identifier as a query parameter only at execution time. The system MUST NOT
include an entity's internal identifier in any text sent to the model provider, including
in prior queries carried into follow-up turns. A generated query that does not use every
declared marker, or that uses an undeclared one, SHALL be rejected like any query the
validator rejects. A mentioned term SHALL NOT trigger a clarification menu for that
entity.

#### Scenario: The generated query filters by the referenced subject

- **GIVEN** a question "¿Qué docentes están designados en @Algoritmos y Estructuras de Datos?" with a reference to that subject of Ingeniería Informática
- **WHEN** the turn is answered
- **THEN** the executed query filters by that subject's identifier and returns only its designations

#### Scenario: The model never receives the identifier

- **GIVEN** a turn with a reference
- **WHEN** every request sent to the model provider during that turn and the next follow-up is inspected
- **THEN** none contains the referenced entity's internal identifier

#### Scenario: A query that ignores a reference is not executed

- **GIVEN** a turn with a reference whose generated query does not use its marker
- **WHEN** the query is validated
- **THEN** it is rejected and the turn ends as not answerable, without executing it

#### Scenario: A referenced subject with a homonym does not ask for clarification

- **GIVEN** two subjects named "Inglés Técnico I" in different careers, and a question with a reference to one of them
- **WHEN** the turn is resolved
- **THEN** no clarification menu is shown and the answer is about the referenced one

### Requirement: A persisted turn keeps its references so history can re-run it

The system SHALL persist, with each turn that used references, the marker-to-entity
bindings its query needs, and SHALL bind them again when that turn is re-run from history
or its conversation is resumed. The stored query text SHALL keep the markers, not the
identifiers.

#### Scenario: Re-running a turn with a reference binds it again

- **GIVEN** a persisted answered turn whose query used a subject reference
- **WHEN** the owner activates «Volver a consultar» on it
- **THEN** the query runs bound to the same subject under the owner's current reach

### Requirement: The composer offers mentions through an accessible popover

The system SHALL open a suggestions popover above the composer when the user types «@»
(subjects) or «#» (teachers) followed by at least 2 characters, and SHALL show the hint
«Escribí al menos 2 letras para buscar materias.» (or «…docentes.») while fewer are
typed. Each subject option SHALL show its name, its career and its code; each teacher
option SHALL show their name and a cargo. The popover SHALL show «Hay más coincidencias.
Seguí escribiendo para acotar.» when more exist, «Sin materias que coincidan en las
carreras a las que tenés acceso.» (or the teachers equivalent) when none match, and a
footer «Solo aparecen materias y docentes de las carreras a las que tu perfil tiene
acceso.» with «Enter elige · Esc cierra». The composer placeholder SHALL be «Preguntá algo
· @ materia · # docente».

#### Scenario: Too short a term shows the hint and does not search

- **GIVEN** the composer
- **WHEN** the user types "@a"
- **THEN** the hint «Escribí al menos 2 letras para buscar materias.» is shown and no search request is sent

#### Scenario: Matching subjects are listed with career and code

- **GIVEN** the composer
- **WHEN** the user types "@alg"
- **THEN** the popover lists matching subjects, each with its name, career and code, and the scope footer

#### Scenario: No match shows the scoped empty message

- **GIVEN** the composer
- **WHEN** the user types a subject term that matches nothing within reach
- **THEN** the popover reads «Sin materias que coincidan en las carreras a las que tenés acceso.»

### Requirement: Choosing a mention inserts a chip whose reference travels with the question

The system SHALL, on choosing an option, replace the typed trigger and term with the
entity's name preceded by its trigger, and SHALL list the chosen entity as a removable
chip in the composer. When the question is sent, each chip whose text is still present in
the question SHALL travel as a structured reference; removing a chip SHALL remove its text
and its reference. In the sent question, each mention SHALL be displayed as a chip.

#### Scenario: A chosen mention travels as a reference

- **GIVEN** the user chose "Algoritmos y Estructuras de Datos" from the popover
- **WHEN** they send the question
- **THEN** the request carries a `materia` reference with that subject's identifier, and the sent question shows the mention as a chip

#### Scenario: Deleting the mention text drops its reference

- **GIVEN** a chosen mention whose text the user then deleted from the field
- **WHEN** they send the question
- **THEN** the request carries no reference for it

### Requirement: The mention popover is an accessible combobox

The system SHALL expose the composer field as a combobox that controls the popover's
listbox while it is open, SHALL move the active option with the arrow keys and expose it
via `aria-activedescendant` without moving focus out of the field, SHALL choose the active
option with Enter, and SHALL close the popover with Escape without closing the modal and
without clearing the typed text. The number of results SHALL be announced through the
existing live region.

#### Scenario: Arrow keys and Enter choose without leaving the field

- **GIVEN** the popover open with three options
- **WHEN** the user presses Down twice and then Enter
- **THEN** the third option is chosen and focus stays in the composer field

#### Scenario: Escape closes only the popover

- **GIVEN** the popover open
- **WHEN** the user presses Escape
- **THEN** the popover closes, the modal stays open and the typed text is unchanged
