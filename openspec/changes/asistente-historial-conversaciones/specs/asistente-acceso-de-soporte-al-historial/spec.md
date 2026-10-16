## Purpose

Lets a permissioned support admin read another user's conversation history —
question, SQL, outcome, and timestamps only, never result rows and never a
re-execution — for incident support, with a mandatory justification and a
permanent, append-only audit trail of every such read.

## ADDED Requirements

### Requirement: Reading another user's history requires a dedicated, nobody-by-default permission

The system SHALL require a dedicated permission, granted to no role by
default, before any actor may read another user's conversation history. The
system MUST NOT let holding the assistant's ordinary usage permission alone
authorize reading someone else's history.

#### Scenario: An actor without the permission cannot read another user's history

- **GIVEN** an authenticated actor without the support-history permission
- **WHEN** they attempt to read another user's conversation history
- **THEN** the request is rejected

#### Scenario: The assistant's ordinary usage permission alone does not grant this access

- **GIVEN** an actor who holds only the assistant's ordinary usage permission
- **WHEN** they attempt to read another user's conversation history
- **THEN** the request is rejected

#### Scenario: The permission starts granted to no role, including default administrative roles

- **GIVEN** the system immediately after this permission is seeded
- **WHEN** every role's permissions are inspected
- **THEN** no role, including any system-administrator role, holds it until explicitly granted

### Requirement: Every read of another user's history requires a mandatory reason

The system SHALL require a non-empty, free-text reason on every request to
list or read another user's conversation history. The system MUST NOT
perform such a read when no reason, or only whitespace, is supplied.

#### Scenario: A read without a reason is rejected

- **GIVEN** a permissioned support actor
- **WHEN** they request another user's history without supplying a reason
- **THEN** the request is rejected and no data is returned

#### Scenario: A whitespace-only reason is rejected

- **GIVEN** a permissioned support actor
- **WHEN** they supply only whitespace as the reason
- **THEN** the request is rejected

#### Scenario: A read with a genuine reason succeeds

- **GIVEN** a permissioned support actor
- **WHEN** they supply a non-empty reason describing why they need to look
- **THEN** the read is performed

### Requirement: Support access exposes text and timestamps only, never result rows, and never re-execution

The system SHALL let a permissioned support reader see, of another user's
history, only the question text, the stored SQL, the turn's outcome, and its
timestamps. The system MUST NOT expose any result row through this access,
and MUST NOT let a support reader re-run a subject's stored SQL, under their
own identity or the subject's.

#### Scenario: A support reader sees text fields only

- **GIVEN** a permissioned support actor reading another user's answered turn
- **WHEN** they view it
- **THEN** they see the question, the SQL, the outcome, and the timestamps, and nothing resembling a result row

#### Scenario: A support reader cannot re-execute a subject's query

- **GIVEN** a permissioned support actor viewing another user's answered turn
- **WHEN** they look for a way to re-run its SQL
- **THEN** no such action is available to them for another user's history

#### Scenario: A support reader cannot impersonate the subject to re-execute their query

- **GIVEN** a permissioned support actor who has read another user's history
- **WHEN** they attempt, through any means available to them, to run that subject's stored SQL as if they were that subject
- **THEN** the attempt is rejected

### Requirement: Every read of another user's history writes a permanent, append-only audit record

The system SHALL write one audit record for every request that lists or
reads another user's history, recording the reader, the subject, which
conversation was read (if a specific one, otherwise that a listing occurred),
when, and the supplied reason. The system MUST write this record before
returning any data, and MUST NOT allow the record to be edited or deleted
once written.

#### Scenario: Reading one conversation is audited with that conversation identified

- **GIVEN** a permissioned support actor reading one specific conversation of another user
- **WHEN** the read completes
- **THEN** an audit record exists naming the reader, the subject, that specific conversation, the time, and the reason

#### Scenario: Listing a subject's conversations is audited as a listing

- **GIVEN** a permissioned support actor listing another user's conversations without opening one
- **WHEN** the listing completes
- **THEN** an audit record exists naming the reader, the subject, the time, and the reason, without naming any specific conversation

#### Scenario: A failure to write the audit record blocks the read

- **GIVEN** a permissioned support actor requesting another user's history
- **WHEN** the system cannot write the audit record for that request
- **THEN** no history data is returned for that request

#### Scenario: An existing audit record cannot be altered or removed

- **GIVEN** an existing audit record
- **WHEN** any actor, including one with the support-history permission, attempts to edit or delete it
- **THEN** no interface or endpoint in the system offers that action

### Requirement: The subject is not shown who accessed their own history

The system MUST NOT expose to a subject, through any endpoint or screen,
whether, when, or by whom their history was read by a support actor.

#### Scenario: A subject's own history view shows no access log

- **GIVEN** a subject whose history has been read by a support actor
- **WHEN** the subject views their own history
- **THEN** nothing indicates that it was accessed by anyone else

#### Scenario: No endpoint answers "who accessed my history"

- **GIVEN** the system's full set of endpoints available to an ordinary actor
- **WHEN** they are enumerated
- **THEN** none of them return another actor's access to the requesting actor's own history

### Requirement: Support audit records are retained on their own schedule, independent of the history they describe

The system SHALL retain support-access audit records for a configurable
period, defaulting to 365 days, independent of the retention or deletion of
the conversation each record describes. The system MUST NOT delete an audit
record solely because the conversation it references was purged or deleted
by its owner.

#### Scenario: A deleted conversation's audit trail survives the deletion

- **GIVEN** an audit record referencing a conversation that its owner later deletes
- **WHEN** the audit records are inspected afterward
- **THEN** that record still exists, still naming the reader, the subject, the time, and the reason

#### Scenario: Audit records older than their retention window are purged

- **GIVEN** an audit record older than the configured audit retention period
- **WHEN** the scheduled purge next runs
- **THEN** that audit record is removed

#### Scenario: Audit retention is independently configurable from history retention

- **GIVEN** the system's configured retention periods
- **WHEN** they are inspected
- **THEN** the audit retention period is a distinct setting from the conversation history retention period
