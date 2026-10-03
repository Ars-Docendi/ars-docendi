## ADDED Requirements

### Requirement: The conversation list is fully keyboard-operable

The system SHALL make every action in the conversation list — opening,
renaming, searching, deleting one, and deleting all — reachable and operable
using only the keyboard, in a logical tab order.

#### Scenario: Every list action is reachable by Tab

- **GIVEN** the conversation list open via the keyboard
- **WHEN** the actor tabs through it
- **THEN** opening, renaming, searching, and both delete actions are all reachable, in a logical order

#### Scenario: An action activates with Enter or Space

- **GIVEN** a focused action in the conversation list
- **WHEN** the actor presses Enter or Space
- **THEN** the action activates the same way a click would

### Requirement: Destructive actions confirm and announce their outcome without disorienting focus

The system SHALL require an explicit confirmation step, reachable by
keyboard, before deleting one conversation or all conversations. The system
SHALL announce, through the existing live-region mechanism, when a delete
completes, without moving focus away from where the actor was.

#### Scenario: A delete confirmation is keyboard-reachable

- **GIVEN** the actor triggers a delete action
- **WHEN** the confirmation step appears
- **THEN** it is reachable and dismissible or confirmable by keyboard alone

#### Scenario: A completed deletion is announced without moving focus

- **GIVEN** the actor confirms a deletion
- **WHEN** it completes
- **THEN** the outcome is announced via the live region, and focus stays where the actor was

### Requirement: Renaming and resuming are keyboard-operable and announce their result

The system SHALL let the actor rename a conversation and resume a
conversation entirely by keyboard, and SHALL announce, via the live-region
mechanism, when a rename is saved and when a resume completes.

#### Scenario: Renaming by keyboard announces the saved title

- **GIVEN** the actor renames a conversation using only the keyboard
- **WHEN** the new title is saved
- **THEN** the save is announced via the live region

#### Scenario: Resuming by keyboard announces that the conversation is ready

- **GIVEN** the actor resumes a conversation using only the keyboard
- **WHEN** its past turns finish loading
- **THEN** readiness is announced via the live region, without moving focus unexpectedly

### Requirement: The re-run action is keyboard-operable and announces its outcome

The system SHALL make the "volver a consultar" action on a past turn
reachable and operable by keyboard, announcing its outcome via the live
region.

#### Scenario: The re-run action is keyboard-operable and announces completion

- **GIVEN** a past answered turn's "volver a consultar" action, reached by keyboard
- **WHEN** the actor activates it and the table finishes loading
- **THEN** completion is announced via the live region
