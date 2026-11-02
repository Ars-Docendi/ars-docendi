## MODIFIED Requirements

### Requirement: Sesión propia revalidada en cada solicitud

La sesión SHALL mantenerse en una cookie `HttpOnly`, `Secure` y `SameSite=Lax`, limitada al host que la emitió, que identifica al usuario y, cuando ya se eligió, la asignación de rol activa, sin contener permisos. En cada solicitud autenticada, el sistema SHALL verificar que el usuario siga activo y resolver el rol, el ámbito y los permisos de la asignación activa desde la persistencia. Las solicitudes a la API sin sesión válida SHALL recibir `401` y nunca una redirección.

#### Scenario: Usuario desactivado con sesión abierta

- **GIVEN** un usuario con sesión iniciada
- **WHEN** un administrador lo desactiva y el usuario hace una nueva solicitud
- **THEN** la solicitud recibe `401` y la sesión deja de ser válida

#### Scenario: Cambio de rol con sesión abierta

- **GIVEN** un usuario con sesión iniciada
- **WHEN** un administrador cambia los permisos del rol de su asignación activa
- **THEN** la siguiente solicitud opera con los permisos vigentes, sin volver a ingresar

#### Scenario: API sin sesión

- **WHEN** el frontend consulta una ruta protegida sin cookie o con una cookie inválida
- **THEN** recibe `401` y el frontend lleva al usuario a la pantalla de login

#### Scenario: Consulta de la sesión vigente

- **GIVEN** una sesión válida
- **WHEN** el frontend consulta la sesión vigente
- **THEN** recibe el nombre para mostrar, el UPN, la asignación activa con el código y nombre del rol y su ámbito, los permisos efectivos, las asignaciones vigentes y si la asignación todavía no fue elegida

### Requirement: Rol de la sesión

La sesión SHALL operar con una única asignación de rol, elegida o por defecto según `seleccion-rol-activo`, y con los permisos del rol de esa asignación. Las demás asignaciones de la persona MUST NOT aportar permisos ni ámbito.

#### Scenario: Usuario con un rol

- **GIVEN** un usuario con una única asignación vigente
- **WHEN** opera con una sesión válida
- **THEN** la sesión expone ese rol, su ámbito y sus permisos

#### Scenario: Usuario con varios roles

- **GIVEN** un usuario con dos asignaciones vigentes
- **WHEN** opera con una sesión válida
- **THEN** la sesión expone sólo el rol, el ámbito y los permisos de la asignación con la que opera, e informa si ya la eligió
