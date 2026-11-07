# asistente-retroalimentacion Specification

## Purpose

Lets a user record a thumbs up/down (with an optional reason) on an answered
turn, linked only to that turn's anonymous analytics row — never to the
actor and never to the operational usage log — so feedback can be reviewed
in aggregate without becoming a second channel that re-identifies who asked
what.

## Requirements

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

### Requirement: A negative vote may carry zero or more reasons from a fixed set

The system SHALL accept, optionally, any number of reasons with a thumbs-down vote, each
drawn from a fixed set: incorrect data (`datos_incorrectos`), didn't understand the
question (`no_entendio_la_pregunta`), missing data (`faltan_datos`), other (`otro`), with
no duplicates. The system MUST NOT accept a reason outside that set, and MUST NOT accept
the retired reason `lento`.

**Reason/Migration for removing the "one reason, no free text" rule:** the previous
version of this requirement (asistente-rediseno-v3, author draft) closed the reason set to
exactly one value and forbade free text entirely, to avoid a bespoke re-identification
channel next to an otherwise-anonymous row (TD-012). The product owner confirmed
(2026-09-26) that the mock's actual behavior — several reasons plus a bounded, hinted
free-text comment — is what ships instead (see the added requirement below for the
comment). No feedback row has ever reached `develop`, so there is no existing data this
change needs to migrate; the schema migration is purely additive (new columns) and is
covered by `MigracionDelAsistenteTests`, not by a spec scenario.

#### Scenario: A thumbs-down with a listed reason is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reason `faltan_datos`
- **THEN** the feedback is recorded with that reason

#### Scenario: A thumbs-down with several listed reasons is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reasons `datos_incorrectos` and `faltan_datos`
- **THEN** the feedback is recorded with both reasons

#### Scenario: The retired reason is rejected

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reason `lento`
- **THEN** the request is rejected with `400` and nothing is recorded

#### Scenario: A duplicated reason is rejected

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reason `otro` listed twice
- **THEN** the request is rejected with `400` and nothing is recorded

#### Scenario: A thumbs-down with no reason is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with no reason
- **THEN** the feedback is recorded with no reason

#### Scenario: A thumbs-up ignores any submitted reason

- **GIVEN** a thumbs-up vote submitted together with one or more reasons
- **WHEN** the feedback is recorded
- **THEN** the stored feedback has no reason

### Requirement: A replaced turn's feedback token stops being accepted

The system SHALL stop accepting the feedback token of a turn as soon as that turn is
replaced by an edited resend, with the same response it gives for an unknown or expired
token. A vote already recorded for the replaced turn SHALL stay as it is, anonymous and
subject to the existing retention.

#### Scenario: Voting on a replaced turn is rejected

- **GIVEN** an answered turn whose token was valid, then replaced by an edited resend
- **WHEN** feedback is submitted with the old token
- **THEN** the request is rejected with the same outcome as an unknown token

#### Scenario: The new turn's token is accepted

- **GIVEN** the same replacement
- **WHEN** feedback is submitted with the new turn's token
- **THEN** the feedback is recorded

### Requirement: A negative vote may carry a bounded free-text comment

The system SHALL accept, optionally, one free-text comment with a thumbs-down vote, up to
500 characters. The system SHALL trim leading and trailing whitespace before validating
and storing it, and SHALL treat a comment that is empty after trimming as absent. A
comment longer than 500 characters after trimming MUST be rejected with `400`. The
comment is never included in any log event, is never sent to the model provider, and has
no read surface in any screen (including the support history screen).

#### Scenario: A comment within the limit is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with a 120-character comment
- **THEN** the feedback is recorded with that comment, trimmed

#### Scenario: A comment over the limit is rejected

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with a comment longer than 500 characters after trimming
- **THEN** the request is rejected with `400` and nothing is recorded

#### Scenario: A whitespace-only comment is stored as absent

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with a comment made only of whitespace
- **THEN** the feedback is recorded with no comment

#### Scenario: A thumbs-up ignores any submitted comment

- **GIVEN** a thumbs-up vote submitted together with a comment
- **WHEN** the feedback is recorded
- **THEN** the stored feedback has no comment
