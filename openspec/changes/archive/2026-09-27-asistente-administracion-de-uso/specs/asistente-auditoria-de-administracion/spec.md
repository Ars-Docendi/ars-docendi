## Purpose

Provides an append-only, non-deletable record of every administrative configuration change to the assistant — budget edits and maintenance toggles — independent of the assistant's existing support-access audit and of the platform's generic `audit.change_log`, so administrative actions on this module are accountable on their own terms.

## ADDED Requirements

### Requirement: Every budget edit and maintenance toggle is recorded

The system SHALL append one row per administrative action — creating or changing a role default, a per-user override, the org-wide monthly cap, or toggling maintenance mode — recording: the administrator's actor id, the moment, the action type, the before value, the after value, and (for maintenance) the reason given.

#### Scenario: A budget edit is recorded with before and after

- **GIVEN** a user's daily quota override is currently 20
- **WHEN** an administrator changes it to 40
- **THEN** an audit row is written naming that administrator, the timestamp, the action type, before=20, after=40

### Requirement: The audit trail is append-only

No endpoint SHALL allow updating or deleting an existing row of this audit trail. It SHALL NOT be reachable through any own-configuration or own-usage endpoint available to a non-administrator.

#### Scenario: No delete path exists

- **GIVEN** the full set of routes this module exposes
- **WHEN** the module's routes are enumerated
- **THEN** none of them can delete or modify an existing row of this audit trail

### Requirement: The audit trail has its own retention window

This audit trail SHALL have its own configurable retention setting, independent of the retention windows of `registro_operativo`, `registro_analitico`, the conversation history, and the support-access audit, following the same reasoning already established for those: an audit record's usefulness is not bound to the lifetime of the thing it describes.

#### Scenario: Retention is swept by the existing purge mechanism

- **GIVEN** an audit row older than the configured retention window
- **WHEN** the scheduled purge runs
- **THEN** that row is deleted, and rows still within the window are untouched

### Requirement: The assistant's own read-only database roles cannot read this table

The table backing this audit trail lives in the `asistente` schema, which is denied wholesale to both `asistente_ro` and `asistente_ro_pii`. No `GRANT` SHALL be added for this table.

#### Scenario: The regression test still passes

- **GIVEN** the privilege manifest test suite
- **WHEN** it runs after this table is created
- **THEN** it confirms neither read-only role can select from this table, with no manifest entry required
