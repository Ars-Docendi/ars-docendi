## Purpose

Lets an operator buy reasoning only on the path of a turn that already failed once, by defining when the second generation of a turn is requested with high effort, with which token ceiling, and what stays untouched.

## ADDED Requirements

### Requirement: Reasoning in the second generation is off by default

The system SHALL request the second generation of a turn with the same effort and token ceiling as the first when `RazonamientoEnSegundaGeneracion` is absent or `false`. With the default, the request sent to any provider MUST be identical to the one produced before this option existed.

#### Scenario: Defaults leave the request unchanged

- **GIVEN** the options at their default values
- **WHEN** a turn ends in an empty-result retry
- **THEN** the retry is requested with the configured generation effort and `MaximoDeTokensDeGeneracion`

#### Scenario: A leftover ceiling has no effect while the option is off

- **GIVEN** `RazonamientoEnSegundaGeneracion` is `false` and `MaximoDeTokensDeSegundaGeneracion` is greater than zero
- **WHEN** a second generation is requested
- **THEN** its token ceiling is `MaximoDeTokensDeGeneracion`

### Requirement: The second generation of a turn asks for high effort

With `RazonamientoEnSegundaGeneracion` on, the system SHALL request every second generation of a turn with the highest standard effort (`alto`): the retry after an empty result, with or without the previous query, and the repair after an engine rejection.

#### Scenario: Retry after an empty result

- **GIVEN** the option on and a first generation whose query returned no rows
- **WHEN** the retry is requested
- **THEN** it asks for high effort

#### Scenario: Repair after an engine rejection

- **GIVEN** the option on and a query PostgreSQL rejected for a repairable reason
- **WHEN** the repair is requested
- **THEN** it asks for high effort

### Requirement: Nothing else changes effort

With `RazonamientoEnSegundaGeneracion` on, the system MUST NOT change the effort of the first generation, of the conversation rewrite or of the redaction.

#### Scenario: The first generation does not ask for high effort

- **GIVEN** the option on
- **WHEN** a turn is generated for the first time
- **THEN** the request carries the configured generation effort, not `alto`

### Requirement: The raised ceiling applies only to the second generation

With the option on and `MaximoDeTokensDeSegundaGeneracion` greater than zero, the second generation SHALL use that value as its token ceiling. With zero it SHALL use `MaximoDeTokensDeGeneracion`. The first generation MUST always use `MaximoDeTokensDeGeneracion`. A negative value MUST be rejected at startup.

#### Scenario: Raised ceiling

- **GIVEN** the option on, `MaximoDeTokensDeGeneracion` 600 and `MaximoDeTokensDeSegundaGeneracion` 2000
- **WHEN** the first and the second generation are requested
- **THEN** the first uses 600 and the second uses 2000

#### Scenario: Negative ceiling

- **GIVEN** `MaximoDeTokensDeSegundaGeneracion` is negative
- **WHEN** the options are validated
- **THEN** startup fails with a message naming the option

### Requirement: A failed second generation never replaces a good first result

A second generation that is truncated, not answerable, without a query or whose query does not validate SHALL leave the turn as it would have been without it: the retry keeps the original result and the repair rethrows the original rejection.

#### Scenario: Truncated retry

- **GIVEN** the option on and a retry that runs out of tokens before closing its answer
- **WHEN** the turn is resolved
- **THEN** the original empty result is kept
