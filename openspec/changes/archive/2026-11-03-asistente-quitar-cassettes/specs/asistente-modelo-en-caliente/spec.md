## MODIFIED Requirements

### Requirement: A cloud selection never unmasks redaction

The system SHALL decide whether redaction runs without masking `sensible-valor` columns from the adapter resolved for the turn, and SHALL mask them whenever that adapter is a cloud company, regardless of the environment's unmasking option and of the environment's provider.

#### Scenario: Selecting a cloud company masks again

- **GIVEN** an environment on the shared inference service with the unmasking option on, so redaction carries real values
- **WHEN** an administrator selects a cloud company and a new turn redacts its answer
- **THEN** the redaction prompt carries masked markers and no real `sensible-valor` value

#### Scenario: Selecting own hardware keeps the environment decision

- **GIVEN** the unmasking option on
- **WHEN** an administrator selects the shared inference service or the own server
- **THEN** redaction follows the environment option as it does without a selection
