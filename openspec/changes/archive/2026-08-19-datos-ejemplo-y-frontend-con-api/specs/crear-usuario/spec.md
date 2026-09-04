## ADDED Requirements

### Requirement: Alta de usuario confirmada por backend

El alta MUST enviarse a la API de administración y el usuario SHALL considerarse creado sólo después de que el backend confirme la transacción. Las validaciones locales MAY anticipar errores, pero no MUST sustituir las validaciones autoritativas.

#### Scenario: Persistencia exitosa

- **GIVEN** un formulario válido y un operador autorizado
- **WHEN** la API confirma el alta
- **THEN** el modal se cierra y una nueva consulta incluye al usuario activo con sus roles y ámbitos

#### Scenario: Backend rechaza el alta

- **GIVEN** un formulario que supera la validación local pero entra en conflicto con datos persistidos
- **WHEN** la API rechaza la solicitud
- **THEN** el modal permanece abierto, muestra el error correspondiente y no agrega una fila optimista como confirmada
