## ADDED Requirements

### Requirement: The user can see and open a list of their own past conversations

The system SHALL present, from the assistant's page and from its top-bar
launcher, a list of the actor's own past conversations, each showing its
title and when it was last active. The system SHALL let the actor open one
to resume it.

#### Scenario: The conversation list is reachable from both surfaces

- **GIVEN** the actor is on the assistant's dedicated page or has opened the top-bar launcher
- **WHEN** they look for their past conversations
- **THEN** the same list of their own conversations is reachable from either place

#### Scenario: Opening a listed conversation resumes it

- **GIVEN** a conversation in the list
- **WHEN** the actor opens it
- **THEN** the conversation resumes with its past turns visible and ready for a follow-up

### Requirement: A conversation can be renamed, searched for, and deleted from the interface

The system SHALL let the actor rename any of their own conversations, search
their own conversations by text, and delete one conversation or all of their
conversations, each action available directly from the conversation list.

#### Scenario: Renaming is available inline from the list

- **GIVEN** a conversation in the actor's list
- **WHEN** they choose to rename it
- **THEN** they can enter a new title without leaving the list

#### Scenario: Searching narrows the list as the actor types

- **GIVEN** the actor's list of conversations
- **WHEN** they enter search text
- **THEN** only conversations matching that text remain visible

#### Scenario: Deleting one conversation asks for confirmation first

- **GIVEN** a conversation in the list
- **WHEN** the actor chooses to delete it
- **THEN** they are asked to confirm before it is permanently removed

#### Scenario: Deleting all conversations asks for confirmation first

- **GIVEN** the actor's list of conversations
- **WHEN** they choose to delete all of them
- **THEN** they are asked to confirm before all of them are permanently removed

### Requirement: A past answered turn offers a "volver a consultar" action

The system SHALL offer, on a past answered turn viewed from the actor's own
history, an action to re-run its stored query and show the resulting table.

#### Scenario: The action is offered only on an answered turn

- **GIVEN** a past turn that ended answered
- **WHEN** the actor views it
- **THEN** a "volver a consultar" action is available

#### Scenario: The action is absent on a turn with nothing to re-run

- **GIVEN** a past turn that ended in clarification, rejection, or degraded service
- **WHEN** the actor views it
- **THEN** no "volver a consultar" action is offered

### Requirement: A permission-gated screen lets support read another user's history

The system SHALL present a support-history screen, reachable only by an
actor holding the dedicated support-history permission, that lets them
select a subject, supply a reason, and read that subject's conversations.
The system MUST NOT show this screen, or a way to reach it, to an actor
lacking that permission.

#### Scenario: The screen is unreachable without the permission

- **GIVEN** an actor without the support-history permission
- **WHEN** they navigate the application
- **THEN** no screen or link offers access to another user's history

#### Scenario: The screen requires a reason before showing another user's history

- **GIVEN** an actor with the support-history permission, on the support-history screen
- **WHEN** they select a subject but have not entered a reason
- **THEN** that subject's history is not shown until a reason is entered

#### Scenario: The screen offers no re-execution or export of another user's query results

- **GIVEN** an actor with the support-history permission viewing a subject's answered turn
- **WHEN** they look for a way to re-run it or see its result rows
- **THEN** no such action or data is present on the screen
