## MODIFIED Requirements

### Requirement: Estimated cost from a versioned price table

The system SHALL compute an estimated cost per user, per role, and org-wide by multiplying each row's token counts by a versioned, admin-configurable price table keyed by provider and model, and SHALL provide an administrator write path that adds a new version by closing the current one. Every place the estimate is shown SHALL label it as an estimate and state that the provider's invoice is the source of truth for actual billing.

#### Scenario: Cost changes when the price table changes

- **GIVEN** two price-table versions with different per-token prices for the same provider/model
- **WHEN** the dashboard computes estimated cost for a period whose rows predate a price change
- **THEN** the system uses the price-table version that was in effect at the time each row was recorded, not the currently active version

#### Scenario: Unknown provider/model has no silent cost

- **GIVEN** a `registro_operativo` row whose `proveedor` value has no matching entry in the price table
- **WHEN** the dashboard computes estimated cost for a period including that row
- **THEN** the system excludes that row's cost from the total and reports it separately as "not priced", rather than defaulting to zero silently

#### Scenario: Pricing an unpriced model takes effect for later usage

- **GIVEN** a model with no price and rows reported as "not priced"
- **WHEN** an administrator writes a price effective from a given date
- **THEN** rows recorded on or after that date are priced with it
- **AND** rows recorded before that date remain "not priced"
