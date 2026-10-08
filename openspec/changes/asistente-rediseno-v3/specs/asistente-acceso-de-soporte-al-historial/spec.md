## ADDED Requirements

### Requirement: Support sees archived and pending-deletion conversations, marked, until deletion is final

The system SHALL include, in a permissioned support read of another user's history,
that user's archived conversations and conversations whose deletion is still inside its
undo window, each marked as such — «Archivada» or «Pendiente de borrado» — on the support
screen. Once a deletion's window expires the conversation SHALL be absent from support
access too. These reads SHALL require the same permission and reason, and SHALL write the
same audit record, as any other support read.

#### Scenario: An archived conversation is visible to support, marked

- **GIVEN** a subject with one archived conversation
- **WHEN** a permissioned support actor lists the subject's history with a reason
- **THEN** the archived conversation is listed and marked «Archivada»

#### Scenario: A conversation inside its undo window is visible to support, marked

- **GIVEN** a subject who deleted a conversation 3 seconds ago
- **WHEN** a permissioned support actor lists the subject's history with a reason
- **THEN** the conversation is listed and marked «Pendiente de borrado»

#### Scenario: A final deletion is invisible to support

- **GIVEN** a subject's conversation whose deletion window expired
- **WHEN** a permissioned support actor lists or reads the subject's history
- **THEN** that conversation is not returned

#### Scenario: Reading a marked conversation is audited like any other

- **GIVEN** a permissioned support actor opening a conversation marked «Pendiente de borrado»
- **WHEN** the read completes
- **THEN** an audit record with the reader, the subject, the reason and the time is written
