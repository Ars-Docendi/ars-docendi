## ADDED Requirements

### Requirement: Activación durable y autorizada

La activación y desactivación de un usuario MUST ejecutarse mediante la API y MUST afectar su acceso en solicitudes posteriores. La pantalla SHALL actualizarse sólo después de una confirmación exitosa.

#### Scenario: Desactivación persistida

- **GIVEN** un usuario activo y una confirmación explícita del operador
- **WHEN** el backend confirma la desactivación
- **THEN** una nueva consulta lo muestra inactivo y sus solicitudes autenticadas posteriores MUST ser denegadas

#### Scenario: Fallo al cambiar el estado

- **GIVEN** una activación o desactivación rechazada por la API
- **WHEN** la pantalla recibe el error
- **THEN** conserva el estado confirmado previamente y comunica el fallo al operador
