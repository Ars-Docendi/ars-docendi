## MODIFIED Requirements

### Requirement: Every budget edit and maintenance toggle is recorded

The system SHALL append one row per administrative action — creating or changing a role default, a per-user override, the org-wide monthly cap, toggling maintenance mode, selecting or reverting the active model, saving or replacing a provider key, or writing a price version — recording: the administrator's actor id, the moment, the action type, the before value, the after value, and (for maintenance) the reason given. For key actions the before and after values SHALL be the company and the last-4 hint only, and the key itself MUST NOT be recorded.

#### Scenario: A budget edit is recorded with before and after

- **GIVEN** a user's daily quota override is currently 20
- **WHEN** an administrator changes it to 40
- **THEN** an audit row is written naming that administrator, the timestamp, the action type, before=20, after=40

#### Scenario: A model selection is recorded

- **GIVEN** the active model is company A with model X
- **WHEN** an administrator applies company B with model Y
- **THEN** an audit row is written naming that administrator, the timestamp, the action type, before=A/X, after=B/Y

#### Scenario: A key replacement records only hints

- **GIVEN** a stored key for a company with hint ending 1234
- **WHEN** an administrator replaces it with a key ending 5678
- **THEN** the audit row records the company with before hint 1234 and after hint 5678
- **AND** no key text is present in the row
