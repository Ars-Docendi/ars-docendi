## ADDED Requirements

### Requirement: Known oscillating items are reported but do not fail the gate

The system SHALL read a versioned list of items known to oscillate in live runs, per axis and with a reason each, and SHALL NOT fail the gate when a listed item goes from passing to failing. It SHALL report that flip as a known oscillation, in the verdict, in the axis report and with a tolerated count in the run summary. Items not in the list MUST keep the strict lock. The list MUST NOT be part of the baseline seal. A listed id absent from the axis baseline MUST fail the run loudly.

#### Scenario: A listed item flips and the gate still passes

- **GIVEN** a baseline where a listed item passes
- **WHEN** a run makes that item fail
- **THEN** the gate passes, and the verdict, report and summary name it as a tolerated known oscillation

#### Scenario: A non-listed item flips

- **GIVEN** a listed item and a non-listed item that both pass in the baseline
- **WHEN** a run makes both fail
- **THEN** the gate fails naming only the non-listed item

#### Scenario: The list references an unknown id

- **GIVEN** a listed id that is not in the axis baseline
- **WHEN** the gate compares
- **THEN** the run fails with a usage error naming the id, not exit 4

#### Scenario: Listing an item does not invalidate the seal

- **GIVEN** frozen baselines
- **WHEN** an item is added to or removed from the list
- **THEN** the baselines' hashes are unchanged and the gate still compares
