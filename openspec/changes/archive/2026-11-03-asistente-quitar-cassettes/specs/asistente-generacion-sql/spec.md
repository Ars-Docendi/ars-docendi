## ADDED Requirements

### Requirement: The fixed generation instructions list the reasons and the term

The fixed generation instructions SHALL describe the four reasons and the term, including that the term must be copied literally from the question and must be `null` when there is none.

#### Scenario: The instructions list the closed set

- **WHEN** the fixed generation instructions are inspected
- **THEN** they name exactly the four reasons and the `termino` key

## REMOVED Requirements

### Requirement: The fixed generation instructions describe the reason and the term

**Reason**: It tied every change of the instructions to re-recording the Anthropic cassette corpus, which no longer exists (change `asistente-quitar-cassettes`).
**Migration**: Replaced by "The fixed generation instructions list the reasons and the term", which keeps the same instructions without the corpus.
