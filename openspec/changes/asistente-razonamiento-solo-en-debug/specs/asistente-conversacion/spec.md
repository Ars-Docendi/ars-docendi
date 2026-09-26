## Purpose

Narrows visibility of the assistant's reasoning disclosure ("Cómo lo interpreté") to a
frontend debug mode, on top of the collapsed-disclosure behavior already defined for
`asistente-conversacion`.

## ADDED Requirements

### Requirement: The reasoning disclosure only renders in frontend debug mode

The system SHALL render the "Cómo lo interpreté" disclosure only when the frontend debug
flag is on, and MUST NOT render it — even when `respuesta.razonamiento` is present —
when the flag is off. The flag SHALL default to off, including in a development build
(`import.meta.env.DEV`); it MUST be explicitly opted into via the frontend environment
variable `VITE_ASISTENTE_DEBUG` set to `"true"`.

#### Scenario: Debug mode off hides the disclosure even with reasoning

- **GIVEN** a response with `razonamiento` and the frontend debug flag off
- **WHEN** the user views it
- **THEN** there is no "Cómo lo interpreté" disclosure anywhere in the message

#### Scenario: Debug mode on shows the collapsed disclosure

- **GIVEN** a response with `razonamiento` and the frontend debug flag on
- **WHEN** the user views it
- **THEN** they find a closed "Cómo lo interpreté" summary inside the message
- **AND** opening it reveals the reasoning

#### Scenario: A development build does not enable debug mode by itself

- **GIVEN** a development build (`import.meta.env.DEV` true) without the debug
  environment variable set
- **WHEN** a response with `razonamiento` is viewed
- **THEN** there is no "Cómo lo interpreté" disclosure

#### Scenario: The interpreted-question line is unaffected

- **GIVEN** a response with `preguntaInterpretada` and `razonamiento`, and the frontend
  debug flag off
- **WHEN** the user views it
- **THEN** they still read "Entendí: …" visibly, outside of any disclosure
