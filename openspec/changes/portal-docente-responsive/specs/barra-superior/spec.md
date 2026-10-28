# Spec Delta

## MODIFIED Requirements

### Requirement: La barra superior solo muestra elementos funcionales

La barra superior del shell SHALL mostrar únicamente el botón "Menú", la marca del sistema y el menú de usuario (`RoleBadge` con "Cerrar sesión"). MUST NOT mostrar controles sin funcionalidad implementada: en particular, MUST NOT mostrar el campo de búsqueda global ni los botones de notificaciones y de ayuda.

#### Scenario: La barra superior no muestra búsqueda, notificaciones ni ayuda

- **GIVEN** un usuario autenticado con cualquier rol
- **WHEN** se renderiza el shell de la aplicación
- **THEN** la barra superior MUST NOT contener un campo de búsqueda global
- **AND** MUST NOT contener botones con etiqueta accesible "Notificaciones" ni "Ayuda"

#### Scenario: El menú de usuario sigue disponible

- **GIVEN** un usuario autenticado
- **WHEN** abre el menú de usuario desde la barra superior
- **THEN** el sistema SHALL mostrar la opción "Cerrar sesión"

#### Scenario: Botón de menú en cualquier ancho

- **GIVEN** un usuario autenticado
- **WHEN** se renderiza el shell con cualquier ancho de viewport
- **THEN** la barra superior muestra el botón "Menú" a la izquierda de la marca

## ADDED Requirements

### Requirement: Barra lateral colapsable desde el botón de menú

Con un viewport de 768 px o más, el botón "Menú" SHALL alternar la barra lateral entre su versión completa y una versión de sólo íconos, en la que todas las rutas permitidas siguen alcanzables. La preferencia SHALL recordarse en el navegador entre visitas. Con menos de 768 px el botón abre el panel de navegación (ver `shell-pantallas-angostas`) y la preferencia no lo afecta.

#### Scenario: Colapsar y expandir en escritorio

- **GIVEN** un usuario autenticado con un viewport de 1280 px y la barra lateral completa
- **WHEN** toca "Menú"
- **THEN** la barra lateral pasa a mostrar sólo íconos y el contenido gana el ancho liberado
- **AND** al tocarlo de nuevo vuelve a la versión completa

#### Scenario: La preferencia se recuerda

- **GIVEN** un usuario que colapsó la barra lateral
- **WHEN** recarga la página o vuelve a entrar desde el mismo navegador
- **THEN** la barra lateral arranca colapsada

#### Scenario: En el teléfono el botón abre el panel

- **GIVEN** un usuario con la barra lateral colapsada y un viewport de 375 px
- **WHEN** toca "Menú"
- **THEN** se abre el panel de navegación con los nombres completos de cada ítem
