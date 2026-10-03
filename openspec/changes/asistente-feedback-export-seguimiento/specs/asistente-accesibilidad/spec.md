## ADDED Requirements

### Requirement: The thumbs control is keyboard-operable and states its pressed state

The system SHALL make the thumbs-up and thumbs-down controls reachable and
operable by keyboard alone, and SHALL expose each control's pressed state
via `aria-pressed`.

#### Scenario: A thumbs control is reachable by Tab

- **GIVEN** an answered turn with a rating control
- **WHEN** the user tabs through the page
- **THEN** both thumbs controls receive focus in sequence

#### Scenario: A thumbs control activates with the keyboard

- **GIVEN** a focused thumbs control
- **WHEN** the user presses Enter or Space
- **THEN** the vote is submitted the same way a pointer click would submit it

#### Scenario: The active vote is exposed to assistive technology

- **GIVEN** a turn rated thumbs-up
- **WHEN** the thumbs-up control is inspected
- **THEN** its `aria-pressed` is `true`, and the thumbs-down control's `aria-pressed` is `false`

### Requirement: A submitted vote is announced without moving focus

The system SHALL announce a submitted vote's confirmation through a live
region, and MUST NOT move keyboard focus away from the thumbs control when
announcing it.

#### Scenario: Voting announces confirmation

- **GIVEN** a user who submits a vote
- **WHEN** the vote is recorded
- **THEN** a live region announces that the vote was recorded

#### Scenario: Voting keeps focus on the thumbs control

- **GIVEN** a user who submits a vote via keyboard
- **WHEN** the vote is recorded
- **THEN** focus remains on the thumbs control that was activated

### Requirement: The export action is keyboard-operable and announces its outcome

The system SHALL make the CSV export action reachable and operable by
keyboard alone, and SHALL announce, through a live region, whether the
export succeeded.

#### Scenario: The export action activates with the keyboard

- **GIVEN** a focused export action
- **WHEN** the user presses Enter or Space
- **THEN** the export runs the same way a pointer click would trigger it

#### Scenario: A completed export is announced

- **GIVEN** a user who triggers the export
- **WHEN** the file has been generated
- **THEN** a live region announces that the export is ready
