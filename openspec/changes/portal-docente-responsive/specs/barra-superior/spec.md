# Spec Delta

## MODIFIED Requirements

### Requirement: La barra superior solo muestra elementos funcionales

Con un viewport de 768 px o más, el shell MUST NOT mostrar una barra superior: el contenido ocupa todo el alto de la pantalla. La barra lateral SHALL mostrar arriba el botón "Menú" y la marca del sistema, y abajo, en un pie fijo, el acceso "Centro de ayuda" (ver `centro-de-ayuda`) y la tarjeta de usuario con iniciales, nombre y rol. Al tocar la tarjeta SHALL abrirse hacia arriba el menú de usuario con "Cerrar sesión". Con menos de 768 px SHALL mostrarse una barra superior fina con únicamente el botón "Menú" y la marca, y la tarjeta de usuario pasa al pie del panel de navegación (ver `shell-pantallas-angostas`). La marca SHALL mostrar el isotipo de Ars Docendi y llevar a Mi portal, el inicio de la aplicación. El shell MUST NOT mostrar controles sin funcionalidad implementada: en particular, MUST NOT mostrar el campo de búsqueda global ni los botones de notificaciones y de ayuda.

#### Scenario: La barra superior no muestra búsqueda, notificaciones ni ayuda

- **GIVEN** un usuario autenticado con cualquier rol
- **WHEN** se renderiza el shell de la aplicación
- **THEN** el shell MUST NOT contener un campo de búsqueda global
- **AND** MUST NOT contener botones con etiqueta accesible "Notificaciones" ni "Ayuda": la ayuda es el ítem "Centro de ayuda" del pie de la barra lateral

#### Scenario: Sin barra superior en escritorio

- **GIVEN** un usuario autenticado con un viewport de 1280 px
- **WHEN** se renderiza el shell
- **THEN** no hay barra superior y el contenido empieza en el borde superior de la pantalla
- **AND** el botón "Menú" y la marca están arriba de la barra lateral

#### Scenario: El menú de usuario sigue disponible

- **GIVEN** un usuario autenticado
- **WHEN** toca la tarjeta de usuario al pie de la barra lateral
- **THEN** se abre hacia arriba el menú de usuario con la opción "Cerrar sesión"
- **AND** al elegirla el sistema cierra la sesión y lleva al ingreso

#### Scenario: La ayuda se alcanza desde la sidebar

- **GIVEN** un usuario autenticado
- **WHEN** se renderiza el shell
- **THEN** el acceso a la ayuda es el ítem "Centro de ayuda" del pie de la sidebar y el shell no contiene un botón equivalente fuera de ella

#### Scenario: La tarjeta de usuario muestra el nombre corto

- **GIVEN** un usuario autenticado cuyo nombre es "Fernández Castellanos, María Guadalupe"
- **WHEN** se renderiza el pie de la barra lateral
- **THEN** la tarjeta muestra las iniciales, "María Fernández" y el rol
- **AND** el nombre completo queda disponible en el texto emergente y en la etiqueta accesible

#### Scenario: La marca lleva a Mi portal

- **GIVEN** un usuario autenticado en cualquier pantalla
- **WHEN** hace clic en la marca
- **THEN** ve el isotipo de Ars Docendi y el sistema lo lleva a Mi portal

#### Scenario: Barra superior fina en el teléfono

- **GIVEN** un usuario autenticado con un viewport de 375 px
- **WHEN** se renderiza el shell
- **THEN** se muestra una barra superior con sólo el botón "Menú" y la marca

## ADDED Requirements

### Requirement: Barra lateral colapsable desde el botón de menú

Con un viewport de 768 px o más, el botón "Menú" de la barra lateral SHALL alternar la barra entre su versión completa y una versión de 64 px de sólo íconos, en la que todas las rutas permitidas y el menú de usuario siguen alcanzables. La preferencia SHALL recordarse en el navegador entre visitas. En `/ayuda` la barra arranca en íconos y el botón la despliega sólo durante esa visita. Con menos de 768 px el botón de la barra superior abre el panel de navegación (ver `shell-pantallas-angostas`).

#### Scenario: Colapsar y expandir en escritorio

- **GIVEN** un usuario autenticado con un viewport de 1280 px y la barra lateral completa
- **WHEN** toca "Menú"
- **THEN** la barra lateral pasa a mostrar el botón, el isotipo, los íconos de navegación, el ícono de ayuda y el avatar, y el contenido gana el ancho liberado
- **AND** al tocarlo de nuevo vuelve a la versión completa

#### Scenario: Menú de usuario con la barra en íconos

- **GIVEN** un usuario con la barra lateral colapsada
- **WHEN** toca su avatar al pie de la barra
- **THEN** se abre el menú de usuario con "Cerrar sesión"

#### Scenario: La preferencia se recuerda

- **GIVEN** un usuario que colapsó la barra lateral
- **WHEN** recarga la página o vuelve a entrar desde el mismo navegador
- **THEN** la barra lateral arranca colapsada

#### Scenario: Desplegar la barra en el Centro de ayuda

- **GIVEN** un usuario en `/ayuda`, con la barra lateral en íconos
- **WHEN** toca "Menú"
- **THEN** la barra lateral se despliega
- **AND** al salir de `/ayuda` vuelve su preferencia guardada, y la próxima entrada a `/ayuda` arranca otra vez en íconos

#### Scenario: En el teléfono el botón abre el panel

- **GIVEN** un usuario con la barra lateral colapsada y un viewport de 375 px
- **WHEN** toca "Menú"
- **THEN** se abre el panel de navegación con los nombres completos de cada ítem
