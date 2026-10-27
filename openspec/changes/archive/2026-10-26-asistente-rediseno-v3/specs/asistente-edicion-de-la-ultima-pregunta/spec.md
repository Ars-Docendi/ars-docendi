## Purpose

Lets a user fix the last question of a conversation and resend it, replacing that
question and its answer everywhere the conversation lives — on screen, in the in-memory
thread and in the persisted history — without keeping versions.

## ADDED Requirements

### Requirement: Only the last question can be edited and resent

The system SHALL offer «Editar y reenviar» only on the last question of the conversation
shown in the modal, and only while no turn is in flight and the composer is not blocked
by quota or maintenance. The system MUST NOT offer it on any earlier question. The
question's tools («Copiar pregunta» and, when applicable, «Editar y reenviar») SHALL be
revealed on pointer hover and on keyboard focus, and SHALL always be reachable by Tab.

#### Scenario: The last question offers editing

- **GIVEN** a conversation with three answered turns and nothing in flight
- **WHEN** the user hovers or focuses the third question
- **THEN** «Editar y reenviar» is available on it

#### Scenario: An earlier question does not offer editing

- **GIVEN** the same conversation
- **WHEN** the user hovers or focuses the second question
- **THEN** «Copiar pregunta» is available and «Editar y reenviar» is not

#### Scenario: Nothing is editable while a turn is in flight

- **GIVEN** a turn in flight
- **WHEN** the user inspects the last question
- **THEN** «Editar y reenviar» is not available

### Requirement: Editing happens inline and Escape cancels

The system SHALL replace the question bubble with a text field prefilled with the
question, and «Cancelar» and «Enviar» actions; the answer below SHALL stay visible but
de-emphasized while editing. Enter (without Shift) or «Enviar» SHALL resend; Escape or
«Cancelar» SHALL discard the edit, restore the question unchanged and return focus to
the «Editar y reenviar» control. An edit whose text is empty after trimming MUST NOT be
sent. The system MUST NOT show any version counter or version navigation.

#### Scenario: Escape cancels and returns focus

- **GIVEN** the last question being edited with changed text
- **WHEN** the user presses Escape
- **THEN** the original question is shown unchanged, nothing is sent, and focus is on «Editar y reenviar»

#### Scenario: An empty edit is not sent

- **GIVEN** the last question being edited
- **WHEN** the user clears the text and presses Enter
- **THEN** no request is sent and the field stays in edit mode

#### Scenario: There are no versions

- **GIVEN** a question that was edited and resent twice
- **WHEN** the user inspects the turn
- **THEN** only the latest question and its answer are shown, with no «N / M» counter and no way to reach earlier versions

### Requirement: Resending replaces the last question and its answer

The system SHALL send the edited question as a new turn that identifies the turn it
replaces, and on success SHALL show the new question and its answer in place of the old
ones, discarding the old answer's vote, sort state and expanded view. Focus SHALL move to
the composer when the new answer arrives, as for any turn.

#### Scenario: The replaced answer disappears

- **GIVEN** an answered last turn with a vote and a sorted table
- **WHEN** the user edits its question and resends it, and the new answer arrives
- **THEN** the turn shows the new question and the new answer, with no vote selected and the table in its original order

### Requirement: The backend replaces the last turn in the thread and in history

The system SHALL accept, on the turn endpoint, an optional identifier of the turn being
replaced, and SHALL honor it only when it names the last turn of the actor's live
conversation thread. On success the system SHALL resolve the new question with the same
conversational context the replaced question had, SHALL replace the thread's last turn
with the new one, and SHALL replace the corresponding persisted turn so that history keeps
only the final version. The system MUST reject with `409 Conflict`, without changing
anything, a replacement that names a turn that is not the thread's last turn or a thread
that no longer exists. The conversation's title SHALL NOT change because of a
replacement.

#### Scenario: History keeps only the final version

- **GIVEN** a persisted conversation whose last turn asked "¿Cuántos titulares hay en Informática?"
- **WHEN** the user replaces it with "¿Cuántos adjuntos hay en Informática?" and the turn succeeds
- **THEN** the conversation's history lists only the new question as its last turn, with the new outcome and SQL, and the old question appears nowhere in it

#### Scenario: A replacement naming an earlier turn is rejected

- **GIVEN** a thread with three turns
- **WHEN** a request asks to replace the second one
- **THEN** the response is `409 Conflict` and neither the thread nor the history changes

#### Scenario: A replacement on an expired thread is rejected

- **GIVEN** a thread that expired from memory
- **WHEN** a request asks to replace its last turn
- **THEN** the response is `409 Conflict` and nothing is written to history

#### Scenario: A follow-up after a replacement uses the replaced context

- **GIVEN** a conversation whose last turn was replaced
- **WHEN** the user asks an anaphoric follow-up
- **THEN** it resolves against the new last turn, not the replaced one

### Requirement: A resend is a new turn for quota, idempotency and the in-flight lock

The system SHALL treat a resend as a new turn: it SHALL carry its own idempotency key,
SHALL be charged against the actor's quota under the same rule as any turn, and SHALL be
subject to the per-actor in-flight lock. A resend rejected by the lock MUST NOT replace or
append anything, in the thread or in history. A resend that fails without producing a
response MUST leave the thread and history as they were, and retrying it SHALL reuse its
key and its replacement target.

#### Scenario: A resend is charged like any turn

- **GIVEN** an actor with 5 turns left today
- **WHEN** they resend an edited question that calls the model
- **THEN** their remaining quota becomes 4

#### Scenario: A resend blocked by the in-flight lock changes nothing

- **GIVEN** a turn of the same actor still running
- **WHEN** the actor resends an edited last question
- **THEN** the response is the concurrent-turn degraded outcome, and the thread and history still hold the original last turn

#### Scenario: A failed resend can be retried safely

- **GIVEN** a resend whose request failed in transport
- **WHEN** the user activates «Reintentar»
- **THEN** the request carries the same idempotency key and the same replacement target, and at most one replacement is applied
