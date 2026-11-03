## ADDED Requirements

### Requirement: The export follows the order currently displayed

The system SHALL write the exported rows in the order the table currently displays —
the user's chosen sort, or the original order when none was chosen — and SHALL export the
same rows and columns in the inline table and in the expanded view.

#### Scenario: A sorted table exports sorted

- **GIVEN** a result table sorted by «Docente» descending
- **WHEN** the user exports it to CSV
- **THEN** the file's rows are ordered by «Docente» descending

#### Scenario: An unsorted table exports in the original order

- **GIVEN** a result table the user never sorted
- **WHEN** the user exports it to CSV
- **THEN** the file's rows are in the order the answer returned them
