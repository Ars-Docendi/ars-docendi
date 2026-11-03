## ADDED Requirements

### Requirement: The generation declares a closed refusal reason

When the generation declares a question not answerable, the generation object SHALL carry a `motivo` from the closed set `fuera_de_tema`, `otro_sistema`, `muy_general`, `no_cubierto`. Any other value, a missing value or a non-string value MUST resolve to `no_cubierto`. The reason MUST NOT require an additional model call, and MUST be ignored when the generation is answerable.

#### Scenario: A valid reason is kept

- **GIVEN** a generation object with `es_contestable: false` and `motivo: "otro_sistema"`
- **WHEN** it is interpreted
- **THEN** the declared reason is `otro_sistema`

#### Scenario: An invalid reason becomes not covered

- **GIVEN** a generation object with `es_contestable: false` and `motivo: "clima"`
- **WHEN** it is interpreted
- **THEN** the declared reason is `no_cubierto`

#### Scenario: A missing reason becomes not covered

- **GIVEN** a generation object with `es_contestable: false` and no `motivo` key
- **WHEN** it is interpreted
- **THEN** the declared reason is `no_cubierto`

#### Scenario: Still exactly one call

- **GIVEN** a generation declaring the question not answerable with any reason
- **WHEN** the turn ends
- **THEN** exactly one model call was consumed

#### Scenario: An answerable generation carries no reason

- **GIVEN** a generation object with `es_contestable: true`, a query and `motivo: "fuera_de_tema"`
- **WHEN** it is interpreted
- **THEN** no refusal reason is attached to the generation

### Requirement: The generation may name the unanswerable term

A not-answerable generation object MAY carry a `termino`: the word or short phrase, copied from the user's question, that names what could not be answered. The value SHALL be treated as an untrusted candidate: it only reaches a user-facing text after the validation required by `asistente-abstencion`. A missing, empty or non-string `termino` SHALL be treated as absent.

#### Scenario: A term candidate is exposed for validation

- **GIVEN** a generation object with `es_contestable: false` and `termino: "python"`
- **WHEN** it is interpreted
- **THEN** the generation carries the candidate term «python»

#### Scenario: A non-string term is ignored

- **GIVEN** a generation object with `es_contestable: false` and `termino: 42`
- **WHEN** it is interpreted
- **THEN** the generation carries no term and the object is still interpreted as not answerable

### Requirement: The fixed generation instructions describe the reason and the term

The fixed generation instructions SHALL describe the four reasons and the term, including that the term must be copied literally from the question and must be `null` when there is none. Changing these instructions changes the fixed prefix, so it SHALL ship together with a re-recorded cassette corpus sealed with the new prefix.

#### Scenario: The instructions list the closed set

- **WHEN** the fixed generation instructions are inspected
- **THEN** they name exactly the four reasons and the `termino` key

#### Scenario: No cassette is sealed with a stale prefix

- **GIVEN** the versioned cassette corpus after the instructions change
- **WHEN** the prefix guard runs
- **THEN** every cassette declares a current prefix fingerprint
