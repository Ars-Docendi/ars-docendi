## Purpose

Defines which real entity names found in a question (materias, carreras, docentes) the assistant hands to the model before it writes the query, so that a literal is copied exactly and a miscopied one does not turn into a false "there is none".

## ADDED Requirements

### Requirement: The option is off by default and changes nothing when off

The system SHALL perform the retrieval only when `Asistente__RecuperacionDeValores` is `true`. With the option absent or `false`, the generation request, including the user message, MUST be byte-identical to the one produced before this option existed, and no database read for this purpose MAY happen.

#### Scenario: Default configuration leaves the message untouched

- **GIVEN** the option is not set and a question that names an existing materia
- **WHEN** the generation message is built
- **THEN** it is identical to the one built before this option existed

### Requirement: A unique match is passed to the model

With the option on, the system SHALL add to the user message, after the question, one line per entity named in the question, stating the text as the user wrote it, the kind of entity (materia, carrera or docente) and the exact stored value, and SHALL tell the model to use that exact value in the filter.

#### Scenario: A materia written without accent or numeral

- **GIVEN** the option is on and exactly one materia visible to the actor is named «Análisis Matemático I»
- **WHEN** the actor asks «¿Quién dicta analisis matematico?»
- **THEN** the message contains the line `En la pregunta, "analisis matematico" corresponde a la materia "Análisis Matemático I".` after the question

#### Scenario: A typo in a materia name

- **GIVEN** the option is on and exactly one materia visible to the actor is named «Bases de Datos»
- **WHEN** the actor asks «¿Quiénes dictan Bases de Dattos?»
- **THEN** the message contains a line that maps "Bases de Dattos" to the materia "Bases de Datos"

#### Scenario: A carrera

- **GIVEN** the option is on and exactly one carrera visible to the actor is named «Ingeniería Industrial»
- **WHEN** the actor asks «¿Qué asignaturas se dictan en ingenieria industrial?»
- **THEN** the message contains a line that maps "ingenieria industrial" to the carrera "Ingeniería Industrial"

### Requirement: An ambiguous match is never chosen

The system SHALL NOT emit a line for a span of the question that matches more than one distinct entity, whether of the same kind or of two kinds. A span that is ambiguous MUST also prevent shorter spans inside it from producing lines. The existing clarification behavior MUST NOT change.

#### Scenario: A materia that exists in three carreras

- **GIVEN** the option is on and three materias visible to the actor are named «Análisis Matemático»
- **WHEN** the actor asks «¿Quién dicta Análisis Matemático?»
- **THEN** the message contains no line for that span

#### Scenario: A span that is a materia and a carrera

- **GIVEN** the option is on and a materia and a carrera visible to the actor share the same normalized name
- **WHEN** the actor names it in a question
- **THEN** the message contains no line for that span

#### Scenario: Homonymous docentes

- **GIVEN** the option is on and two docentes visible to the actor are named «Juan Gómez»
- **WHEN** the actor asks about «Juan Gómez»
- **THEN** the message contains no line for that span

### Requirement: Nothing outside the actor's scope reaches the prompt

The system SHALL look entities up as the asking actor, with the read-only role and the actor's scope enforced by the database, and MUST NOT include in the message, nor let influence which lines appear, any entity the actor cannot see.

#### Scenario: A materia outside the actor's visible set

- **GIVEN** the option is on and a materia named «Sistemas Operativos» that is not visible to the actor
- **WHEN** the actor asks «¿Quién dicta Sistemas Operativos?»
- **THEN** the message contains no line naming that materia

#### Scenario: A docente outside the actor's reach

- **GIVEN** the option is on and a docente whose designations the actor cannot see
- **WHEN** the actor asks about that docente by full name
- **THEN** the message contains no line naming that docente

### Requirement: Docentes are matched only by full name

The system SHALL emit a line for a docente only when the question contains the docente's whole given name and whole surname, adjacent, in either order, matching exactly after case and accent folding. A surname alone MUST NOT produce a line, and a docente name MUST NOT be matched approximately.

#### Scenario: Only a surname

- **GIVEN** the option is on and exactly one docente visible to the actor has surname «Suárez»
- **WHEN** the actor asks «¿Qué materias dicta Suárez?»
- **THEN** the message contains no line for that docente

#### Scenario: Full name in either order

- **GIVEN** the option is on and exactly one docente visible to the actor is «Marta Suárez»
- **WHEN** the actor asks about «suarez marta»
- **THEN** the message contains a line that maps that span to the docente "Marta Suárez"

### Requirement: Common words and weak matches do not produce lines

The system SHALL NOT match spans made only of common Spanish words, spans that start or end with one, or single words shorter than four letters. A single word SHALL NOT be matched approximately unless it has at least six letters, and an approximate match MUST tolerate at most one edit per word of five to ten letters and two per longer word, and none in shorter words.

#### Scenario: A common word

- **GIVEN** the option is on and a materia named «Mesa de Entradas»
- **WHEN** the actor asks «¿Cuántos pedidos hay en la mesa?»
- **THEN** the message contains no line for that materia

### Requirement: Bounded and deterministic output

The system SHALL emit at most five lines, ordered by the position of the span in the question, and the same question, actor scope and data MUST always produce the same lines.

#### Scenario: More than five matches

- **GIVEN** a question that names six different entities, each uniquely
- **WHEN** the message is built
- **THEN** it contains five lines, those of the first five spans in reading order

### Requirement: Explicit mentions take precedence

The system SHALL NOT emit a line for an entity that the turn already binds through an explicit mention, new or inherited from an earlier turn of the same segment.

#### Scenario: A mentioned materia

- **GIVEN** the option is on and the actor mentioned the materia «Análisis Matemático I» through the mention picker
- **WHEN** the generation message is built
- **THEN** it contains the mention block and no line for that materia

### Requirement: A turn with lines does not use the generated-query cache

When a turn has at least one line, the system SHALL NOT read the generated-query cache and SHALL NOT write the resulting generation to it.

#### Scenario: Two actors with different scope

- **GIVEN** the cache is on, the option is on, and the same question asked by an actor who gets a line and by one who gets none
- **WHEN** both turns run
- **THEN** the first turn generates a query without using the cache and stores nothing, and the second is unaffected by the first

### Requirement: Retrieval failure degrades to no lines

If the lookup fails or times out, the system SHALL answer the turn as if the option were off for that turn and MUST log the failure without any entity value.

#### Scenario: The database read fails

- **GIVEN** the option is on and the lookup raises a database error
- **WHEN** the actor asks a question
- **THEN** the turn proceeds with a message identical to the one built with the option off

### Requirement: The evaluator exercises the retrieval

The retrieval SHALL run inside the SQL lane, so that a turn answered by the production conversational layer and a turn answered directly by the evaluator's capacidad and robustez runs go through the same code.

#### Scenario: A direct call to the lane

- **GIVEN** the option is on and a question that names a unique visible materia
- **WHEN** the lane answers it without the conversational layer
- **THEN** the generation request carries the line for that materia
