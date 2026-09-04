## ADDED Requirements

### Requirement: Edición persistente de identidad y roles

La edición completa del usuario MUST enviarse a la API y MUST guardar atómicamente los datos de persona, cuenta y asignaciones de rol con sus ámbitos. Si cualquier parte es inválida, ninguna modificación MUST quedar aplicada.

#### Scenario: Edición completa exitosa

- **GIVEN** datos válidos, roles con ámbitos compatibles y un operador autorizado
- **WHEN** el backend confirma la edición
- **THEN** una nueva consulta devuelve conjuntamente todos los valores actualizados

#### Scenario: Una asignación de rol es inválida

- **GIVEN** cambios de persona válidos y una asignación de rol con ámbito inválido
- **WHEN** se intenta guardar el formulario
- **THEN** la API rechaza la operación completa y conserva tanto los datos de persona como los roles anteriores
