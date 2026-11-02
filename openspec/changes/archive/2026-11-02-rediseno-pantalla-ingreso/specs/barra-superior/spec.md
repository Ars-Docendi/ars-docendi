# Spec Delta

## MODIFIED Requirements

### Requirement: La barra superior solo muestra elementos funcionales

La barra superior del shell SHALL mostrar únicamente la marca del sistema y el menú de usuario (`RoleBadge` con "Cerrar sesión"). La marca SHALL mostrar el isotipo de Ars Docendi y llevar a Mi portal, el inicio de la aplicación. MUST NOT mostrar controles sin funcionalidad implementada: en particular, MUST NOT mostrar el campo de búsqueda global ni los botones de notificaciones y de ayuda.

#### Scenario: La barra superior no muestra búsqueda, notificaciones ni ayuda

- **GIVEN** un usuario autenticado con cualquier rol
- **WHEN** se renderiza el shell de la aplicación
- **THEN** la barra superior MUST NOT contener un campo de búsqueda global
- **AND** MUST NOT contener botones con etiqueta accesible "Notificaciones" ni "Ayuda"

#### Scenario: El menú de usuario sigue disponible

- **GIVEN** un usuario autenticado
- **WHEN** abre el menú de usuario desde la barra superior
- **THEN** el sistema SHALL mostrar la opción "Cerrar sesión"

#### Scenario: La marca lleva a Mi portal

- **GIVEN** un usuario autenticado en cualquier pantalla
- **WHEN** hace clic en la marca de la barra superior
- **THEN** ve el isotipo de Ars Docendi y el sistema lo lleva a Mi portal
