## ADDED Requirements

### Requirement: An answered turn shows a way to rate it

The system SHALL render a thumbs-up/thumbs-down control on a turn that
carries a feedback token, and MUST NOT render it on a turn that does not.

#### Scenario: An answered turn shows the rating control

- **GIVEN** a turn answered with a feedback token
- **WHEN** the user views it
- **THEN** the thumbs control is visible next to that turn

#### Scenario: A rejection, clarification, or degraded turn shows no rating control

- **GIVEN** a turn that ended without a feedback token
- **WHEN** the user views it
- **THEN** no thumbs control is shown for that turn

### Requirement: A thumbs-down vote offers a reason before submitting

The system SHALL, when the user picks thumbs-down, offer the fixed set of
reasons (incorrect data, didn't understand the question, slow, other)
before the vote is sent, and SHALL allow submitting thumbs-down with no
reason chosen.

#### Scenario: Picking thumbs-down surfaces the reason choices

- **GIVEN** a user about to rate a turn thumbs-down
- **WHEN** they pick thumbs-down
- **THEN** they see the four reason choices before the vote is sent

#### Scenario: Skipping a reason still submits the vote

- **GIVEN** the reason choices shown after picking thumbs-down
- **WHEN** the user submits without picking a reason
- **THEN** the thumbs-down vote is sent with no reason

### Requirement: A submitted vote is visibly confirmed and remains changeable

The system SHALL show the user which vote is currently recorded for a turn,
and SHALL allow changing it.

#### Scenario: The recorded vote is visibly marked

- **GIVEN** a turn already rated thumbs-up
- **WHEN** the user views it again
- **THEN** the thumbs-up control shows as the active choice

#### Scenario: The user can change their vote

- **GIVEN** a turn already rated thumbs-up
- **WHEN** the user picks thumbs-down instead
- **THEN** the turn now shows thumbs-down as the active choice

### Requirement: The result table offers a CSV export action

The system SHALL render an export action alongside a rendered result table
with at least one row, and MUST NOT render it for an empty result.

#### Scenario: A non-empty result offers export

- **GIVEN** a turn answered with at least one row
- **WHEN** the user views the result table
- **THEN** an export-to-CSV action is available

#### Scenario: An empty result offers no export

- **GIVEN** a turn answered with zero rows
- **WHEN** the user views it
- **THEN** no export action is shown

### Requirement: Follow-up suggestions after a successful answer are presented like other suggestions

The system SHALL render suggestions received on a successfully answered
turn using the same suggestions presentation already used for a rejection's
suggestions (non-blocking, presented as new questions to try).

#### Scenario: A successful answer's suggestions render as chips to try next

- **GIVEN** a successfully answered turn whose response includes suggestions
- **WHEN** the user views it
- **THEN** the suggestions render the same way a rejection's suggestions do, and choosing one starts a new question with that text

#### Scenario: No suggestions means no suggestions section

- **GIVEN** a successfully answered turn whose response includes no suggestions
- **WHEN** the user views it
- **THEN** no suggestions section is shown for that turn
