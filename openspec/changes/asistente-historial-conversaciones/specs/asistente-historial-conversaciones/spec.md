## Purpose

Lets a user keep, find, and return to their own past conversations with the
Asistente — persisting only the question, the SQL that answered it, the
turn's outcome, and timestamps, and never the rows a query returned or the
model's drafted answer text — with a 180-day retention and no
per-conversation opt-out, so "what did user X ask" stays answerable through
the user's own history and through audited support access.

## ADDED Requirements

### Requirement: Every conversation is persisted to history, with only question, resolved SQL, outcome, and timestamps — never rows, never drafted answer text

The system SHALL persist every conversation's turns to history; there is no
per-conversation or per-turn opt-out. For each turn, the system SHALL persist
the question text, the SQL that produced its answer (when one exists), the
turn's outcome state, and its timestamp. The system MUST NOT persist any row
a query returned, and MUST NOT persist the model's drafted answer text.

#### Scenario: A history row never carries result rows

- **GIVEN** a turn that answered with a result table
- **WHEN** that turn's history row is inspected in the database
- **THEN** it has no column holding any returned row value

#### Scenario: A history row never carries the drafted answer text

- **GIVEN** a turn that answered with a drafted, human-readable response
- **WHEN** that turn's history row is inspected in the database
- **THEN** it has no column holding that drafted text — only the question, the SQL, the outcome state, and timestamps

#### Scenario: A turn that failed with an unhandled exception is never persisted to history

- **GIVEN** a turn that ended in an unhandled exception (never produced an HTTP response)
- **WHEN** the conversation's history is inspected
- **THEN** no row was written for that turn

#### Scenario: There is no way to start a conversation that skips history

- **GIVEN** any turn request that starts a new conversation
- **WHEN** the request is inspected for a way to exclude that conversation from history
- **THEN** no such option exists, and the conversation is recorded like any other

### Requirement: A user can list, search, and rename their own conversations

The system SHALL let an authenticated actor list their own persisted
conversations, each auto-titled from its first question, and rename any of
their own conversations to a title they choose. The system SHALL let the
actor search their own conversations by matching text against their own
questions. The system MUST NOT return, in this list or search, any
conversation belonging to another actor.

#### Scenario: A new conversation is auto-titled from its first question

- **GIVEN** a conversation whose first question is "¿Cuántos pedidos tiene pendientes la materia Álgebra?"
- **WHEN** the actor lists their conversations
- **THEN** that conversation's title is derived from that first question

#### Scenario: Renaming overrides the auto-generated title

- **GIVEN** an existing conversation with its auto-generated title
- **WHEN** the actor renames it
- **THEN** the list shows the new title, and it is not overwritten by any later turn

#### Scenario: Searching matches the actor's own past questions

- **GIVEN** the actor has one conversation containing the question "¿Cuántas designaciones vencen este cuatrimestre?"
- **WHEN** the actor searches for "designaciones"
- **THEN** that conversation appears in the results

#### Scenario: A user never sees another user's conversations, listed or searched

- **GIVEN** two different actors, each with their own conversations
- **WHEN** one of them lists or searches their conversations
- **THEN** only their own conversations appear, never the other actor's

### Requirement: A user can delete one conversation or all of their conversations, permanently

The system SHALL let an actor permanently delete one of their own
conversations, or all of their own conversations at once. Deletion MUST
remove the conversation and all its turns; the system MUST NOT retain a
soft-deleted or recoverable copy.

#### Scenario: Deleting one conversation removes only that one

- **GIVEN** an actor with two conversations
- **WHEN** they delete one of them
- **THEN** that conversation and all its turns are gone, and the other conversation is unaffected

#### Scenario: Deleting all conversations leaves none

- **GIVEN** an actor with several conversations
- **WHEN** they delete all of their conversations
- **THEN** listing their conversations afterward returns none

#### Scenario: A deleted conversation is not recoverable

- **GIVEN** a conversation an actor just deleted
- **WHEN** anyone queries for that conversation's id afterward, including the actor
- **THEN** it is not found, in any endpoint

### Requirement: Resuming a past conversation restores follow-up context

The system SHALL let an actor resume one of their own past conversations,
restoring enough of its question/answer history that a follow-up question
relying on coreference (e.g. "¿y el de Pérez?") resolves against that past
context the same way it would have at the time. Resuming MUST NOT re-fetch
or display any result row from a past turn; it restores conversational
context, not result data.

#### Scenario: A follow-up after resuming resolves against the restored context

- **GIVEN** a past conversation whose last question was about a specific person
- **WHEN** the actor resumes it and asks a follow-up that refers back anaphorically
- **THEN** the follow-up resolves against that person, the same way it would have before the conversation ended

#### Scenario: Resuming does not show old result rows

- **GIVEN** a past conversation with an answered turn that once returned a result table
- **WHEN** the actor resumes that conversation
- **THEN** no result rows from that past turn are shown until the actor explicitly asks again

#### Scenario: Continuing after resuming extends the same conversation

- **GIVEN** an actor resumes a past conversation and asks a new question
- **WHEN** they list their conversations afterward
- **THEN** the new turn belongs to the same conversation, not a new one

### Requirement: A resumed or listed turn shows its SQL only with the query-visibility permission

The system SHALL include a past turn's question, outcome, and timestamps
regardless of permission. The system SHALL include that turn's resolved SQL
only when the actor holds the permission that already gates SQL visibility
on a live turn. The system MUST NOT include the SQL for an actor lacking
that permission.

#### Scenario: An actor with the permission sees the stored SQL

- **GIVEN** an actor who holds the query-visibility permission
- **WHEN** they view one of their own past answered turns
- **THEN** the SQL that answered it is included

#### Scenario: An actor without the permission never sees the stored SQL

- **GIVEN** an actor who does not hold the query-visibility permission
- **WHEN** they view one of their own past answered turns
- **THEN** the question, outcome, and timestamp are included, and the SQL field is absent

### Requirement: A past answered turn can be re-run for its table, never re-drafted

The system SHALL let an actor re-run, on demand, the exact SQL stored for
one of their own past turns that ended answered, and return the resulting
table under the actor's current permissions. The system MUST NOT make a new
call to the language model as part of this action, and MUST NOT create a new
conversation turn or history row from it.

#### Scenario: Re-running a past turn returns a table without a new model call

- **GIVEN** one of the actor's own past turns that ended answered
- **WHEN** the actor asks to re-run it
- **THEN** a result table is returned, and no new model call, conversation turn, or history row is produced

#### Scenario: Re-running reflects the actor's current permissions, not their permissions at the time

- **GIVEN** a past turn whose stored SQL touched data the actor could see when they first asked, but can no longer see
- **WHEN** the actor re-runs that turn today
- **THEN** the result reflects what they can see today, not what they could see when they first asked

#### Scenario: A turn that cannot answer is excluded from re-execution

- **GIVEN** a past turn that ended in clarification, rejection, or degraded service
- **WHEN** the actor attempts to re-run it
- **THEN** the request is rejected, since no answered turn's SQL exists for it

#### Scenario: Re-running SQL that no longer runs fails gracefully

- **GIVEN** a past turn whose stored SQL no longer executes against the current schema or the actor's current privileges
- **WHEN** the actor attempts to re-run it
- **THEN** they receive a clear, non-technical explanation that the query could not be re-run, never a raw database error

#### Scenario: Re-running is only available on the actor's own history

- **GIVEN** an actor viewing their own past turn
- **WHEN** they compare it against a support reader viewing another actor's history (see `asistente-acceso-de-soporte-al-historial`)
- **THEN** only the owner ever has the re-run action available for that turn

### Requirement: History is retained for 180 days from last activity and purged automatically

The system SHALL retain a conversation, and all its turns, for a configurable
period defaulting to 180 days measured from the conversation's most recent
activity. The system SHALL purge conversations (and their turns) older than
that period automatically, on a recurring schedule, without manual
intervention.

#### Scenario: An inactive conversation is purged after the retention window

- **GIVEN** a conversation whose last activity is older than the configured retention period
- **WHEN** the scheduled purge next runs
- **THEN** that conversation and its turns are removed

#### Scenario: An active conversation is not purged just because it is old

- **GIVEN** a conversation created more than 180 days ago but with activity within the retention window
- **WHEN** the scheduled purge runs
- **THEN** that conversation is retained

#### Scenario: A purge failure does not stop the assistant from working

- **GIVEN** one scheduled purge run fails
- **WHEN** the assistant is used afterward
- **THEN** it continues answering turns normally, and the purge is retried on its next scheduled run

### Requirement: History and its tables are denied to the assistant's own read-only database roles

The system SHALL deny both of the assistant's own read-only database roles
any access to the history tables, the same way they are already denied every
table the assistant writes for its own bookkeeping. The system MUST NOT grant
either role any privilege on these tables, directly or by omission.

#### Scenario: The assistant's basic read-only role cannot read history

- **GIVEN** the assistant's read-only role without personal-data access
- **WHEN** it attempts to read the history tables
- **THEN** the read is denied

#### Scenario: The assistant's personal-data read-only role cannot read history either

- **GIVEN** the assistant's read-only role with personal-data access
- **WHEN** it attempts to read the history tables
- **THEN** the read is denied

#### Scenario: The assistant cannot answer a question about another user's history

- **GIVEN** an actor asking the assistant, in a live turn, what another user has asked it before
- **WHEN** the assistant tries to answer using its own database connections
- **THEN** it cannot reach the history tables to answer, the same way it cannot reach the existing anonymous registers

### Requirement: A user cannot access, modify, or resume another user's conversation through this capability

The system SHALL scope every own-history operation — listing, searching,
renaming, deleting, resuming, and re-executing — to the requesting actor's
own conversations. The system MUST reject an attempt to operate on a
conversation that does not belong to the requesting actor, through this
capability's endpoints.

#### Scenario: Renaming another user's conversation is rejected

- **GIVEN** a conversation belonging to a different actor
- **WHEN** the requesting actor attempts to rename it
- **THEN** the request is rejected

#### Scenario: Deleting another user's conversation is rejected

- **GIVEN** a conversation belonging to a different actor
- **WHEN** the requesting actor attempts to delete it
- **THEN** the request is rejected, and the conversation still exists for its owner

#### Scenario: Resuming another user's conversation is rejected

- **GIVEN** a conversation belonging to a different actor
- **WHEN** the requesting actor attempts to resume it
- **THEN** the request is rejected
