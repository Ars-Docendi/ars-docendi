## Purpose

Gives an administrator a persisted, audited way to turn the assistant off for maintenance, with the frontend showing a real banner and disabling input instead of letting turns fail unexplained.

## ADDED Requirements

### Requirement: Persisted, toggleable maintenance flag

The system SHALL persist a single maintenance flag (on/off) in the database, toggleable only by an actor holding `asistente.administrar`, together with a mandatory free-text reason for turning it on. The flag SHALL survive redeploys and be visible identically to every backend instance.

#### Scenario: Toggling requires the admin permission

- **GIVEN** an authenticated actor without `asistente.administrar`
- **WHEN** that actor calls the maintenance-toggle endpoint
- **THEN** the system responds 403 and the flag is unchanged

#### Scenario: Turning maintenance on requires a reason

- **GIVEN** an administrator turning maintenance mode on
- **WHEN** the administrator omits the reason
- **THEN** the system rejects the request and the flag stays off

#### Scenario: The flag survives a redeploy

- **GIVEN** maintenance mode is on
- **WHEN** the backend process restarts
- **THEN** maintenance mode is still on afterward, without any admin action

### Requirement: Every toggle is audited

Every transition of the maintenance flag (on or off) SHALL be recorded by `asistente-auditoria-de-administracion` with who toggled it, when, the new state, and the reason given.

#### Scenario: Turning it off is audited too

- **GIVEN** maintenance mode is currently on
- **WHEN** an administrator turns it off
- **THEN** an audit row is written naming that administrator, the timestamp, and the new state "off"

### Requirement: Capabilities endpoint reflects maintenance state

`GET /api/asistente/capacidades` SHALL report whether maintenance mode is active and, when active, the reason text, so the frontend can render a visible banner and disable the input control. This SHALL cost no model call, consistent with the endpoint's existing zero-token guarantee.

#### Scenario: Ordinary user sees the banner

- **GIVEN** maintenance mode is on with reason "Mantenimiento programado"
- **WHEN** an ordinary user requests capabilities
- **THEN** the response indicates maintenance is active and includes that reason text

### Requirement: New turns are blocked while maintenance is active, except for administrators

While maintenance mode is active, a turn submitted by an actor without `asistente.administrar` SHALL resolve as the existing `ServicioDegradado` outcome with a message stating the assistant is under maintenance. An actor holding `asistente.administrar` SHALL be allowed to submit turns while maintenance is active, so recovery can be verified before reopening to everyone.

#### Scenario: Ordinary user is blocked during maintenance

- **GIVEN** maintenance mode is on
- **WHEN** a user without `asistente.administrar` submits a turn
- **THEN** the turn resolves as the degraded outcome naming maintenance as the cause

#### Scenario: Administrator can still submit a turn during maintenance

- **GIVEN** maintenance mode is on
- **WHEN** an actor holding `asistente.administrar` submits a turn
- **THEN** the turn is processed normally, not blocked by maintenance

### Requirement: The maintenance check is behind a swappable abstraction

The maintenance-state check SHALL be resolved through an abstraction that does not name Postgres in its public shape, so a future implementation backed by a feature-flag service (e.g. Azure App Configuration) can replace the Postgres-backed default without changing any caller. This change SHALL NOT add any Azure dependency.

#### Scenario: No Azure dependency is introduced

- **GIVEN** this change as merged
- **WHEN** the module's dependencies are inspected
- **THEN** no Azure App Configuration package or client is present anywhere in `Modules.Asistente`
