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

### Requirement: A negative vote may carry one reason from a fixed set

The system SHALL accept, optionally, exactly one reason with a thumbs-down vote, drawn
from a fixed set: incorrect data (`datos_incorrectos`), didn't understand the question
(`no_entendio_la_pregunta`), missing data (`faltan_datos`), other (`otro`). The system
MUST NOT accept free-text reasons and MUST NOT accept the retired reason `lento` in any
new submission. Rows recorded with `lento` before this change SHALL be kept unchanged
until the existing retention removes them; changing the vote on such a row SHALL replace
its reason like any other change.

#### Scenario: A thumbs-down with a listed reason is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reason `faltan_datos`
- **THEN** the feedback is recorded with that reason

#### Scenario: The retired reason is rejected

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with the reason `lento`
- **THEN** the request is rejected with `400` and nothing is recorded

#### Scenario: A thumbs-down with no reason is accepted

- **GIVEN** a thumbs-down vote
- **WHEN** it is submitted with no reason
- **THEN** the feedback is recorded with no reason

#### Scenario: A thumbs-up ignores any submitted reason

- **GIVEN** a thumbs-up vote submitted together with a reason
- **WHEN** the feedback is recorded
- **THEN** the stored feedback has no reason

#### Scenario: Legacy rows survive the migration

- **GIVEN** a feedback row recorded with `lento` before this change
- **WHEN** the migration runs
- **THEN** the row is still present with its reason unchanged, and it is removed only when its analytic record ages out
