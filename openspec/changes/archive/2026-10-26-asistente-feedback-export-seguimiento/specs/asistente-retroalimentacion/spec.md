## Purpose

Lets a user record a thumbs up/down (with an optional reason) on an answered
turn, linked only to that turn's anonymous analytics row — never to the
actor and never to the operational usage log — so feedback can be reviewed
in aggregate without becoming a second channel that re-identifies who asked
what.

## ADDED Requirements

### Requirement: Feedback links only to the analytic record, never to the actor or the operational record

The system SHALL persist feedback keyed only by the answered turn's analytic
identifier (`asistente.registro_analitico.id`). The system MUST NOT persist,
log, or derive any column that identifies the actor, and MUST NOT persist
any column that could join the feedback row back to `asistente.registro_operativo`.

#### Scenario: The feedback row carries no actor

- **GIVEN** a user submits feedback for an answered turn
- **WHEN** the feedback row is inspected in the database
- **THEN** it has no actor identifier, no session identifier, and no column present in `asistente.registro_operativo`

#### Scenario: Submitting feedback does not write to the operational log

- **GIVEN** a user submits feedback for an answered turn
- **WHEN** the operational log for that request window is inspected
- **THEN** it contains no new row and no modified row correlated to the feedback submission

#### Scenario: The feedback table is denied to the assistant's own read-only roles

- **GIVEN** the assistant's two read-only database roles
- **WHEN** either role attempts to read the feedback table
- **THEN** the read is denied, the same way both roles are already denied every table in the `asistente` schema

### Requirement: Only the holder of the turn's feedback token may rate it

The system SHALL accept feedback for a turn only when the request presents
the feedback token minted for that specific turn. The system MUST NOT accept
feedback identified only by an analytic row id guessed or enumerated without
that token, and MUST NOT accept feedback keyed by actor identity in place of
the token.

#### Scenario: A valid, unexpired token is accepted

- **GIVEN** a token returned by an answered turn less than 2 hours ago
- **WHEN** feedback is submitted with that token
- **THEN** the feedback is recorded

#### Scenario: An unknown or fabricated token is rejected indistinguishably from an expired one

- **GIVEN** a token that was never issued, or one issued more than 2 hours ago
- **WHEN** feedback is submitted with that token
- **THEN** the request is rejected with the same outcome in both cases, so a caller cannot tell "expired" apart from "never existed"

#### Scenario: A caller without the assistant's usage permission is rejected regardless of token validity

- **GIVEN** an authenticated actor without `asistente.consultar`
- **WHEN** they submit feedback with an otherwise valid token
- **THEN** the request is rejected

### Requirement: Only an answered turn produces a feedback token

The system SHALL include a feedback token in the response of a turn that
ended `respondida`. The system MUST NOT include a feedback token in the
response of a turn that ended `necesita_aclaracion`, `no_contestable`, or
`servicio_degradado`.

#### Scenario: An answered turn can be rated

- **GIVEN** a turn that ended as an answer
- **WHEN** the client reads the response
- **THEN** it contains a feedback token

#### Scenario: A pending clarification cannot be rated

- **GIVEN** a turn that ended needing clarification
- **WHEN** the client reads the response
- **THEN** it contains no feedback token

#### Scenario: A rejection cannot be rated

- **GIVEN** a turn that ended not-answerable
- **WHEN** the client reads the response
- **THEN** it contains no feedback token

### Requirement: A vote can be changed, and only the latest vote is kept

The system SHALL accept a new feedback submission for a turn that already
has one, replacing the previous vote and reason. The system MUST NOT persist
a history of prior votes for the same turn.

#### Scenario: Changing a vote overwrites the previous one

- **GIVEN** a turn already rated thumbs-down with a reason
- **WHEN** the same token is used to submit thumbs-up
- **THEN** the stored feedback reflects only thumbs-up, with no trace of the prior vote

#### Scenario: Resubmitting the same vote is a no-op, not an error

- **GIVEN** a turn already rated thumbs-up
- **WHEN** the same token is used to submit thumbs-up again
- **THEN** the request succeeds and exactly one feedback row exists for that turn

### Requirement: A negative vote may carry one reason from a fixed set

The system SHALL accept, optionally, exactly one reason with a thumbs-down
vote, drawn from a fixed set: incorrect data, didn't understand the
question, slow, other. The system MUST NOT accept free-text reasons.

#### Scenario: A thumbs-down with a listed reason is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reason "didn't understand the question"
- **THEN** the feedback is recorded with that reason

#### Scenario: A thumbs-down with no reason is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with no reason
- **THEN** the feedback is recorded with no reason

#### Scenario: A thumbs-up ignores any submitted reason

- **GIVEN** a thumbs-up vote submitted together with a reason
- **WHEN** the feedback is recorded
- **THEN** the stored feedback has no reason
