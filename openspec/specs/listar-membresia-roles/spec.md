# listar-membresia-roles Specification

## Purpose

Permite seleccionar roles institucionales para consultar y gestionar sus membresías.

## Requirements

### Requirement: Redirección de la ruta de membresía legacy

La ruta `/membresia-roles` SHALL redirigir a `/roles` conservando la sesión y sin renderizar una segunda pantalla de gestión. La sidebar SHALL ofrecer sólo la entrada canónica "Roles".

#### Scenario: Navegar a la ruta legacy

- **WHEN** un usuario navega a `/membresia-roles`
- **THEN** el router lo redirige a `/roles` y la pantalla unificada queda disponible

#### Scenario: No duplicar entradas de navegación

- **WHEN** un usuario con `roles.ver` visualiza la configuración
- **THEN** ve una sola entrada para Roles y no ve una entrada separada para Membresía de Roles
