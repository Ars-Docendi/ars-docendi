## ADDED Requirements

### Requirement: Todo schema de la base está clasificado en el manifiesto

El sistema SHALL verificar que cada schema de usuario presente en la base figure en el manifiesto de privilegios, clasificado como expuesto o denegado con su motivo.

La verificación MUST fallar nombrando el schema que no esté clasificado.

Un schema sin `GRANT` alguno MUST igualmente estar clasificado: la ausencia de privilegios no es una decisión registrada.

#### Scenario: Un schema sin clasificar hace fallar la verificación

- **GIVEN** un schema de usuario en la base que el manifiesto no nombra
- **WHEN** se verifica el manifiesto contra la base
- **THEN** la verificación falla nombrando ese schema

#### Scenario: Denegado con motivo es una respuesta válida

- **GIVEN** un schema clasificado como denegado con su motivo escrito
- **WHEN** se verifica el manifiesto contra la base
- **THEN** la verificación pasa
