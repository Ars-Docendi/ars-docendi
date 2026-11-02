# Spec Delta

## MODIFIED Requirements

### Requirement: Estructura uniforme del encabezado de página

Toda pantalla autenticada SHALL encabezarse con un breadcrumb seguido de un `PageHeader` con título, y opcionalmente meta y acciones. El encabezado MUST NOT mostrar un pretitle (línea en mayúsculas sobre el título). El meta SHALL mostrar solo información que no aparece en otro lugar de la misma pantalla, y MUST NOT repetir el rol del usuario, que ya se muestra en la tarjeta de usuario del shell.

#### Scenario: Ninguna pantalla muestra pretitle

- **GIVEN** un usuario con permisos para todas las pantallas
- **WHEN** navega a cualquiera de ellas
- **THEN** el encabezado MUST NOT mostrar un pretitle (por ejemplo "Designaciones", "Configuración" o "Cuatrimestre 2026 · 1C")

#### Scenario: El meta de Revisión no repite el rol

- **GIVEN** un usuario con `designaciones.revisar`
- **WHEN** abre `/designaciones/revision`
- **THEN** el meta MUST NOT incluir el nombre del rol del usuario
