## ADDED Requirements

### Requirement: Usable from 360 px

The Aulas pages and modals, the solicitud detail and the notifications panel SHALL be usable from 360 px width with no horizontal page scroll. Below 560 px paddings SHALL shrink like the portal's, dialogs SHALL fit the screen, interactive targets SHALL be at least 24 by 24 px (44 px for primary controls), wide tables SHALL scroll inside their own container and data lists SHALL stack label above value.

#### Scenario: Narrow screen

- **GIVEN** a 360 px wide viewport
- **WHEN** the user opens Reserva de aulas, a solicitud and the notifications panel
- **THEN** the page SHALL not scroll horizontally and every dialog SHALL fit inside the screen
