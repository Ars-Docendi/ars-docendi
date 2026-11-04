## MODIFIED Requirements

### Requirement: La barra superior solo muestra elementos funcionales

La barra superior del shell SHALL mostrar únicamente la marca del sistema y el menú de usuario (`RoleBadge` con "Cerrar sesión"). MUST NOT mostrar controles sin funcionalidad implementada: en particular, MUST NOT mostrar el campo de búsqueda global ni los botones de notificaciones y de ayuda. El acceso a la ayuda vive en el pie de la sidebar (ver `centro-de-ayuda`), no en la barra superior.

#### Scenario: La barra superior no muestra búsqueda, notificaciones ni ayuda

- **GIVEN** un usuario autenticado con cualquier rol
- **WHEN** se renderiza el shell de la aplicación
- **THEN** la barra superior MUST NOT contener un campo de búsqueda global
- **AND** MUST NOT contener botones con etiqueta accesible "Notificaciones" ni "Ayuda"

#### Scenario: El menú de usuario sigue disponible

- **GIVEN** un usuario autenticado
- **WHEN** abre el menú de usuario desde la barra superior
- **THEN** el sistema SHALL mostrar la opción "Cerrar sesión"

#### Scenario: La ayuda se alcanza desde la sidebar

- **GIVEN** un usuario autenticado
- **WHEN** se renderiza el shell
- **THEN** el acceso a la ayuda es el ítem "Centro de ayuda" de la sidebar y la barra superior no contiene un botón equivalente
