## MODIFIED Requirements

### Requirement: La barra superior solo muestra elementos funcionales

The shell top bar SHALL show only elements with implemented behavior: the system brand, the menu button and, on narrow screens, the notifications bell. It MUST NOT show controls without implemented behavior: in particular it MUST NOT show the global search field nor the help button. The bell MUST open the notifications panel and MUST show the number of unread notifications.

#### Scenario: La barra superior no muestra búsqueda, notificaciones ni ayuda

- **GIVEN** an authenticated user with any role
- **WHEN** the application shell renders
- **THEN** the top bar MUST NOT contain a global search field
- **AND** MUST NOT contain a button with the accessible name "Ayuda"
- **AND** MUST NOT contain a notifications button without behavior (the functional bell is covered by the next scenario)

#### Scenario: La campana de la barra superior es funcional

- **GIVEN** an authenticated user on a narrow screen
- **WHEN** the application shell renders
- **THEN** the top bar SHALL contain a button with the accessible name "Notificaciones" that opens the notifications panel

#### Scenario: El menú de usuario sigue disponible

- **GIVEN** un usuario autenticado
- **WHEN** abre el menú de usuario desde la barra superior
- **THEN** el sistema SHALL mostrar la opción "Cerrar sesión"
