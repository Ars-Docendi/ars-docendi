## Purpose

Replaces the in-memory, redeploy-losing per-actor quota with a Postgres-backed budget that survives redeploys and multiple instances, so administrative cost control holds under real concurrent usage instead of resetting on every deploy.

## ADDED Requirements

### Requirement: Per-user daily quota in turns, with role defaults and per-user overrides

The system SHALL enforce a per-user daily quota measured in **turns** (not model calls, not requests). Each role SHALL have a configurable default daily quota; an administrator MAY set a per-user override that takes precedence over the role default for that user. The quota SHALL be evaluated against a rolling or calendar day boundary configured once for the whole module.

#### Scenario: Role default applies with no override

- **GIVEN** a user whose role has a daily quota of 20 turns and no per-user override
- **WHEN** the user has completed 20 turns today
- **THEN** the user's 21st turn is blocked for the rest of that day

#### Scenario: Per-user override takes precedence

- **GIVEN** a user whose role default is 20 turns/day and who has an active per-user override of 40 turns/day
- **WHEN** the user has completed 25 turns today
- **THEN** the user is not blocked, because the override of 40 applies instead of the role default

#### Scenario: Quota resets on schedule

- **GIVEN** a user who exhausted their daily quota yesterday
- **WHEN** the day boundary passes
- **THEN** the user's quota is available again without any manual admin action

### Requirement: Org-wide monthly cap in estimated USD

The system SHALL enforce an org-wide monthly spending cap expressed in estimated USD, computed the same way `asistente-panel-de-uso` computes estimated cost (tokens × the versioned price table). When the org-wide cap is reached, every user's turns SHALL block for the remainder of the calendar month, regardless of their individual daily quota.

#### Scenario: Org cap blocks even a user under their own daily quota

- **GIVEN** the org-wide monthly estimated cost has reached the configured cap
- **AND** a specific user still has quota remaining under their own daily limit
- **WHEN** that user submits a turn
- **THEN** the turn is blocked with the org-cap-exceeded reason, not the per-user-quota reason

#### Scenario: Org cap resets at the start of the next calendar month

- **GIVEN** the org-wide cap was reached during the current month
- **WHEN** a new calendar month starts
- **THEN** turns are no longer blocked by the org cap until the new month's accumulated estimated cost reaches the cap again

### Requirement: Visible thresholds at 50/80/100%

The system SHALL make each user's and the org-wide budget's consumption visible to an administrator at 50%, 80%, and 100% of the applicable limit, through the usage dashboard and a structured log warning emitted the moment a threshold is first crossed within its period. No email or other notification channel SHALL be added (TD-013 stays closed as written).

#### Scenario: Crossing 80% logs once

- **GIVEN** a user at 79% of their daily quota
- **WHEN** a turn brings them to 82%
- **THEN** the system emits exactly one structured log warning naming the 80% threshold for that user and that day, and does not emit it again for subsequent turns still under 100% that same day

#### Scenario: Dashboard shows the exact percentage, not just the threshold band

- **GIVEN** a user at 63% of their daily quota
- **WHEN** an administrator opens the usage dashboard for that user
- **THEN** the dashboard shows the user's exact consumption and remaining quota, not merely "past 50%"

### Requirement: Exhaustion resolves as the existing degraded outcome, never a 500

The system SHALL resolve a turn blocked by the per-user daily quota, or by the org-wide monthly cap, as the existing `ServicioDegradado` (degraded service) outcome of the 4-state response contract, each with its own explicit, friendly, non-technical explanation distinguishing the two causes. Neither case SHALL ever surface as an HTTP 500 or an unhandled exception.

#### Scenario: User-quota exhaustion names when quota returns

- **GIVEN** a user whose daily quota is exhausted
- **WHEN** the user submits a new turn
- **THEN** the turn resolves as the degraded outcome with a message stating the quota is exhausted and naming when it resets

#### Scenario: Org-cap exhaustion does not promise a return time to an ordinary user

- **GIVEN** the org-wide monthly cap has been reached
- **WHEN** an ordinary user (without `asistente.administrar`) submits a turn
- **THEN** the turn resolves as the degraded outcome with a message stating the service is temporarily unavailable, without exposing the org's cost or cap value to that user

### Requirement: Quota is charged on every outcome, including failure

The system SHALL charge a turn against the daily quota exactly once per turn, regardless of whether the turn succeeded, was abstained, needed clarification, degraded, or ended in an unhandled exception — preserving the existing rule that a turn that falls over still counts, so failure is never a way to consult for free.

#### Scenario: A turn that throws still counts against quota

- **GIVEN** a user with 1 turn of daily quota remaining
- **WHEN** their next turn ends in an unhandled exception
- **THEN** their remaining daily quota is 0 afterward, the same as if the turn had answered successfully

### Requirement: Budget configuration changes require the admin permission

Creating or changing a role default, a per-user override, or the org-wide monthly cap SHALL require the `asistente.administrar` permission and SHALL be recorded by `asistente-auditoria-de-administracion`.

#### Scenario: An ordinary user cannot change their own quota

- **GIVEN** an authenticated user without `asistente.administrar`
- **WHEN** that user calls the budget-configuration endpoint
- **THEN** the system responds 403 and the budget is unchanged
