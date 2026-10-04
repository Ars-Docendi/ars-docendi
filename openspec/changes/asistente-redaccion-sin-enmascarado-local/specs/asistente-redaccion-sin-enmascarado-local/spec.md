## Purpose

Lets an on-premises deployment state real personal values in the narrated answer, by defining exactly when the redaction prompt may carry unmasked `sensible-valor` values and what remains masked in every configuration.

## ADDED Requirements

### Requirement: Masking stays on unless the option is explicitly enabled

The system SHALL mask every `sensible-valor` value in the redaction prompt when `Asistente__RedaccionSinEnmascarar` is absent or `false`.

With the default, the redaction prompt MUST be identical to the one produced before this option existed.

#### Scenario: Default configuration masks the document

- **GIVEN** the option is not set and a result has a `sensible-valor` column with a known real value
- **WHEN** the redaction prompt is built
- **THEN** the prompt contains the marker and does not contain the real value

### Requirement: With the local provider the redaction prompt carries the real values

The system SHALL send the real values of `sensible-valor` columns in the redaction prompt when the option is `true`, the provider is `local`, and no cassette directory is configured.

#### Scenario: The document reaches the redaction prompt

- **GIVEN** the option is `true`, the provider is `local`, no cassette directory is configured, and a result has a `sensible-valor` column with a known real value
- **WHEN** the redaction prompt is built
- **THEN** the prompt contains the real value and no marker for it

#### Scenario: Public columns are unaffected

- **GIVEN** the same configuration and a result with only public columns
- **WHEN** the redaction prompt is built
- **THEN** the prompt is identical to the one built with the option off

### Requirement: Free-text sensitive columns stay suppressed in every configuration

The system SHALL suppress every `sensible-texto` column from the redaction prompt, name and values, regardless of the option and of the provider.

#### Scenario: The history comment never reaches the model

- **GIVEN** the option is `true`, the provider is `local`, and a result has a `sensible-texto` column
- **WHEN** the redaction prompt is built
- **THEN** the prompt contains neither that column's name nor any of its values

### Requirement: The option has no effect with any other provider

The system SHALL keep masking `sensible-valor` values when the option is `true` and the provider is not `local`.

The system SHALL start normally in that configuration and MUST log a warning at startup stating that the option is set but not in effect.

#### Scenario: A remote provider never receives the real value

- **GIVEN** the option is `true` and the provider is `anthropic`
- **WHEN** the redaction prompt is built
- **THEN** the prompt does not contain the real value

#### Scenario: Rolling the provider back does not stop the service

- **GIVEN** the option is `true` and the provider is changed from `local` to `anthropic`
- **WHEN** the application starts
- **THEN** it starts, masking is in effect, and a warning names the option as not in effect

### Requirement: The option has no effect while cassettes are configured

The system SHALL keep masking `sensible-valor` values when the option is `true` and a cassette directory is configured, whatever the provider.

#### Scenario: A recorded response never contains a real value

- **GIVEN** the option is `true`, the provider is `local`, and a cassette directory is configured
- **WHEN** the redaction prompt is built
- **THEN** the prompt does not contain the real value

### Requirement: Access to personal data is unchanged

The option MUST NOT change which actors can read personal data.

An actor without the personal-data connection SHALL NOT receive `sensible-valor` values, in the rows or in the narrated text, whatever the option says.

#### Scenario: A scoped actor asking for phones still gets none

- **GIVEN** the option is `true`, the provider is `local`, and an actor with a scoped (non-global) reach
- **WHEN** the actor asks for the phone numbers of the staff
- **THEN** the turn returns no phone number, in the rows or in the text

### Requirement: The effective state is declared at startup

The system SHALL log, once at startup, whether redaction masking of `sensible-valor` values is in effect.

#### Scenario: The operator can tell which mode is running

- **GIVEN** the option is `true`, the provider is `local`, and no cassette directory is configured
- **WHEN** the application starts
- **THEN** a log entry states that redaction runs without masking `sensible-valor` values
