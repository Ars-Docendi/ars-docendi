## Purpose

Gives an administrator visibility into how the assistant is actually being used — per user, per role, and org-wide, over a selectable period — sourced only from the anonymized-by-design operational log, so cost and adoption can be managed without ever exposing what anyone asked.

## ADDED Requirements

### Requirement: Admin-only usage dashboard endpoint

The system SHALL expose a read endpoint, gated by the `asistente.administrar` permission, that aggregates `asistente.registro_operativo` rows over an admin-selected period (day/week/month or explicit date range) into: per-user totals, per-role totals, and an organization-wide total. Each aggregation SHALL report: turn count, outcome breakdown (answered, abstained/no-contestable, needs-clarification, degraded — including the sub-reasons this change introduces), model-call count, tokens in/out/cache, average and p95 latency, and the provider/model string.

#### Scenario: Actor without the permission is rejected

- **GIVEN** an authenticated actor who lacks `asistente.administrar`
- **WHEN** the actor requests the usage dashboard endpoint
- **THEN** the system responds 403 and returns no aggregated data

#### Scenario: Per-user aggregation never exposes question text

- **GIVEN** an administrator with `asistente.administrar`
- **WHEN** the administrator requests per-user usage for a period
- **THEN** the response contains only counters, timings, and identifiers derived from `asistente.registro_operativo`, and never a question, an SQL string, or any field sourced from `asistente.registro_analitico`

#### Scenario: Org-wide total does not require naming any user

- **GIVEN** an administrator with `asistente.administrar`
- **WHEN** the administrator requests the org-wide total for a period
- **THEN** the system returns aggregate counters for the whole organization without listing or requiring the identity of any individual actor

### Requirement: Estimated cost from a versioned price table

The system SHALL compute an estimated cost per user, per role, and org-wide by multiplying each row's token counts by a versioned, admin-configurable price table keyed by provider and model. Every place the estimate is shown SHALL label it as an estimate and state that the provider's invoice is the source of truth for actual billing.

#### Scenario: Cost changes when the price table changes

- **GIVEN** two price-table versions with different per-token prices for the same provider/model
- **WHEN** the dashboard computes estimated cost for a period whose rows predate a price change
- **THEN** the system uses the price-table version that was in effect at the time each row was recorded, not the currently active version

#### Scenario: Unknown provider/model has no silent cost

- **GIVEN** a `registro_operativo` row whose `proveedor` value has no matching entry in the price table
- **WHEN** the dashboard computes estimated cost for a period including that row
- **THEN** the system excludes that row's cost from the total and reports it separately as "not priced", rather than defaulting to zero silently

### Requirement: Per-user display name resolution without a new permission dependency

The dashboard SHALL resolve each `actor_id` to a human-readable display name using the module's existing identity-read seam, without requiring the administrator to also hold any permission other than `asistente.administrar`.

#### Scenario: Dashboard works for an admin who lacks usuarios.ver

- **GIVEN** an administrator who holds `asistente.administrar` but not `usuarios.ver`
- **WHEN** the administrator opens the per-user usage view
- **THEN** every row shows the corresponding user's display name, and the request succeeds without needing `usuarios.ver`
