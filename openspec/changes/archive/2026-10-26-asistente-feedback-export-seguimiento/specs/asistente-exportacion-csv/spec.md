## Purpose

Lets a user take a turn's result table out of the browser as a real,
spreadsheet-safe `.csv` file, built entirely from data the client already
has, without ever exposing an unmasked value or a value that could execute
as a formula in the opening spreadsheet application.

## ADDED Requirements

### Requirement: The export is generated entirely client-side from data already received

The system SHALL build the CSV file from the columns and rows already
rendered in the result table. The system MUST NOT issue a new request to
the backend to build the export.

#### Scenario: Exporting does not trigger a new turn or a new query

- **GIVEN** a rendered result table
- **WHEN** the user exports it to CSV
- **THEN** no new request is sent to the assistant's query endpoint

### Requirement: Exported values are exactly the values displayed, masked as displayed

The system SHALL export each cell exactly as it is rendered on screen,
including any masking already applied to a sensitive column. The system
MUST NOT export an unmasked value for a column marked sensitive.

#### Scenario: A masked column stays masked in the export

- **GIVEN** a result table with a column marked sensitive and displayed masked
- **WHEN** the table is exported
- **THEN** the exported column contains the same masked representation, never the underlying value

### Requirement: Cells that could be read as a spreadsheet formula are neutralized

The system SHALL prefix, with a single leading apostrophe, any cell whose
first character is `=`, `+`, `-`, `@`, a tab, or a carriage return, before
applying field quoting. The system MUST apply this prefix uniformly,
regardless of whether the cell's remaining content looks numeric or textual.

#### Scenario: A cell starting with an equals sign is neutralized

- **GIVEN** a cell whose value starts with `=`
- **WHEN** the table is exported
- **THEN** the exported cell starts with an apostrophe followed by the original value, and does not execute as a formula when the file is opened in a spreadsheet application

#### Scenario: A negative number is neutralized the same way as any other leading-dash cell

- **GIVEN** a cell whose value is a negative number such as `-42`
- **WHEN** the table is exported
- **THEN** the exported cell is prefixed the same way as a cell starting with `=`, `+`, `@`, tab, or carriage return

### Requirement: Fields are quoted per RFC 4180

The system SHALL wrap in double quotes any cell containing a comma, a double
quote, or a line break, doubling any internal double quote. The system SHALL
apply this quoting after the injection-neutralizing prefix, so the leading
apostrophe itself never requires escaping.

#### Scenario: A cell containing a comma round-trips correctly

- **GIVEN** a cell whose value contains a comma
- **WHEN** the exported file is reopened in a spreadsheet application
- **THEN** the comma appears inside a single cell, not as a column separator

### Requirement: The file is UTF-8 with a byte-order mark and CRLF line endings

The system SHALL encode the exported file as UTF-8 with a leading byte-order
mark and CRLF line endings.

#### Scenario: Spanish accented characters render correctly in Excel

- **GIVEN** a result table containing accented characters
- **WHEN** the exported file is opened in Microsoft Excel on Windows
- **THEN** the accented characters display correctly rather than as replacement characters

### Requirement: A truncated result states so inside the file, not only in its name

The system SHALL, when the result was truncated, append a final row to the
exported file stating in words that the result was truncated. The system
MUST NOT state or imply a specific count of rows omitted.

#### Scenario: A truncated export includes a trailing notice row

- **GIVEN** a result table flagged as truncated
- **WHEN** it is exported
- **THEN** the file's last row states in words that the result is truncated, and no row states or implies how many rows were omitted

#### Scenario: A non-truncated export has no notice row

- **GIVEN** a result table not flagged as truncated
- **WHEN** it is exported
- **THEN** the file contains exactly one row per result row plus the header, with no trailing notice

### Requirement: The file name is safe across operating systems and flags truncation

The system SHALL name the exported file using only ASCII characters safe on
Windows, macOS, and Linux file systems, and SHALL include a truncation
marker in the name when the result was truncated.

#### Scenario: A truncated export's file name signals truncation

- **GIVEN** a truncated result table
- **WHEN** it is exported
- **THEN** the file name includes a truncation marker
