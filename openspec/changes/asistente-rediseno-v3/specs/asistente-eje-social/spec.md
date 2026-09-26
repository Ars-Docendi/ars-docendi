## ADDED Requirements

### Requirement: Los ítems no contestables aprueban con la abstención

El sistema SHALL contar como correcto un ítem no contestable cuando el turno se abstiene —no contestable o necesita aclaración— y como incorrecto cuando responde. El runner MUST NOT exigir sugerencias, porque el contrato del turno ya no las tiene.

#### Scenario: Abstenerse alcanza

- **GIVEN** un ítem no contestable
- **WHEN** el turno se abstiene
- **THEN** el ítem se cuenta como correcto

#### Scenario: Responder un no contestable falla

- **GIVEN** un ítem no contestable
- **WHEN** el turno termina respondido
- **THEN** el ítem se cuenta como incorrecto

## REMOVED Requirements

### Requirement: Los ítems no contestables exigen sugerencias

**Reason**: Refusals no longer carry suggestions (ARS-140, ARS-149), so the criterion "abstention plus suggestions" can never be met.
**Migration**: See "Los ítems no contestables aprueban con la abstención"; the dataset description in `backend/eval/datasets/social.json` is updated to match.
