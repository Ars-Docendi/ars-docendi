## Purpose

Lets an end user see their own remaining daily quota and whether they are currently blocked, so a rejection is never a surprise and the assistant's cost limits are legible rather than silent.

## ADDED Requirements

### Requirement: Remaining quota exposed in capabilities

`GET /api/asistente/capacidades` SHALL include the requesting actor's remaining daily quota (in turns) and, when the actor is blocked, the reason (own quota, org cap, or maintenance — reusing the same reasons `asistente-presupuesto-persistente`/`asistente-modo-mantenimiento` define) and, when known, when it will reset. This SHALL cost no model call.

#### Scenario: Capabilities shows remaining quota when not blocked

- **GIVEN** a user with 7 of 20 daily turns used
- **WHEN** the user requests capabilities
- **THEN** the response reports 13 turns remaining and no blocked state

#### Scenario: Capabilities shows the blocked reason and reset time

- **GIVEN** a user whose daily quota is exhausted
- **WHEN** the user requests capabilities
- **THEN** the response reports zero turns remaining, the blocked reason, and when the quota resets

### Requirement: Turn outcome carries the authoritative remaining-quota value at submit time

The response to a submitted turn SHALL include the actor's remaining daily quota as of immediately after that turn was charged, so the value the user sees after acting is never staler than the capabilities value they last saw before acting.

#### Scenario: Remaining quota decreases after a turn

- **GIVEN** a user with 5 turns remaining before submitting a turn
- **WHEN** that turn completes (in any of the four outcomes)
- **THEN** the turn's response reports 4 turns remaining

### Requirement: Remaining quota is rendered unobtrusively and accessibly

The frontend SHALL render the remaining-quota indicator in the existing status strip, outside the live-region used for turn announcements, and SHALL make it programmatically accessible without requiring the user to notice a small visual detail. It SHALL follow the accessibility rules already established for the assistant's other chrome (keyboard reachability where interactive, no color-only signal for the blocked state).

#### Scenario: Blocked state is not conveyed by color alone

- **GIVEN** a user whose quota is exhausted
- **WHEN** the status strip renders the blocked state
- **THEN** the blocked state is conveyed by text, not only by a color change

#### Scenario: The indicator does not steal focus or interrupt the live region

- **GIVEN** a user actively reading a turn's answer via a screen reader
- **WHEN** the remaining-quota indicator updates after that turn
- **THEN** the update does not move focus and is not announced as part of the turn's live-region announcement
