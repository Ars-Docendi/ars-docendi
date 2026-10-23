## ADDED Requirements

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

## MODIFIED Requirements

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
