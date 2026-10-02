## ADDED Requirements

### Requirement: Tipografía administrativa consistente en Roles

La pantalla `/roles` SHALL usar la misma familia tipográfica institucional y el mismo tamaño base
de texto que las pantallas `/usuarios` y `/docentes` para el buscador, la lista de roles, el panel
de permisos y sus controles equivalentes. La jerarquía de títulos y textos secundarios SHALL
mantener la escala visual del sistema sin tamaños arbitrarios propios de la pantalla.

#### Scenario: Texto de la pantalla de Roles

- **GIVEN** un operador autorizado visualiza `/roles`
- **WHEN** observa el buscador, los roles y los permisos
- **THEN** el texto de controles y contenido usa la misma tipografía y tamaño base perceptible que
  los controles y tablas equivalentes de `/usuarios` y `/docentes`

#### Scenario: Controles nativos de Roles

- **GIVEN** el operador interactúa con el buscador o con un control del panel de permisos
- **WHEN** el control recibe foco o cambia de estado
- **THEN** conserva la tipografía y el tamaño de texto de la interfaz administrativa general
