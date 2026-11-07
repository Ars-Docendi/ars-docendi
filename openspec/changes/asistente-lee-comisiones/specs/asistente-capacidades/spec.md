## ADDED Requirements

### Requirement: The catalog announces comisiones questions only to actors who can ask them

The system SHALL derive, from the actor's live permissions, whether the capability presentation announces questions about comisiones (docentes of a comisión, schedules, aulas, turnos, counts per docente).

The system MUST NOT announce comisiones questions to an actor who does not hold `comisiones.ver`.

The presentation MUST NOT offer questions about free aulas or about the import log.

#### Scenario: With the permission comisiones are offered

- **GIVEN** an actor who holds `comisiones.ver`
- **WHEN** the actor asks for the capability catalog
- **THEN** the presentation offers questions about comisiones, docentes, schedules and aulas

#### Scenario: Without the permission they are not offered

- **GIVEN** an actor who does not hold `comisiones.ver`
- **WHEN** the actor asks for the capability catalog
- **THEN** the presentation does not mention comisiones

#### Scenario: Free aulas are never promised

- **GIVEN** any actor
- **WHEN** the presentation is read
- **THEN** it does not offer to find free aulas or to read the import log
