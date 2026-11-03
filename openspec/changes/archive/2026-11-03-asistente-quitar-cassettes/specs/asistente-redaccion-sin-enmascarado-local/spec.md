## MODIFIED Requirements

### Requirement: With the local provider the redaction prompt carries the real values

The system SHALL send the real values of `sensible-valor` columns in the redaction prompt when the option is `true` and the provider is `local`.

#### Scenario: The document reaches the redaction prompt

- **GIVEN** the option is `true`, the provider is `local`, and a result has a `sensible-valor` column with a known real value
- **WHEN** the redaction prompt is built
- **THEN** the prompt contains the real value and no marker for it

#### Scenario: Public columns are unaffected

- **GIVEN** the same configuration and a result with only public columns
- **WHEN** the redaction prompt is built
- **THEN** the prompt is identical to the one built with the option off

### Requirement: The effective state is declared at startup

The system SHALL log, once at startup, whether redaction masking of `sensible-valor` values is in effect.

#### Scenario: The operator can tell which mode is running

- **GIVEN** the option is `true` and the provider is `local`
- **WHEN** the application starts
- **THEN** a log entry states that redaction runs without masking `sensible-valor` values

## REMOVED Requirements

### Requirement: The option has no effect while cassettes are configured

**Reason**: The cassettes only recorded and replayed Anthropic's wire format, and Anthropic is no longer a production provider: the assistant runs only on the local model. The corpus could not protect the path production uses and had gone stale with every schema change.
**Migration**: None; the recorder, its two options (`DirectorioDeCassettes`, `RegrabarCassettes`), the corpus and its tests are removed (change `asistente-quitar-cassettes`). Parsing of real local-model output stays covered by the local adapter tests.
