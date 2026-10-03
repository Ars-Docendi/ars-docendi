## Purpose

Permite ingresar a Ars Docendi con una cuenta Microsoft, personal o de cualquier organización, sólo a las personas que la administración dio de alta, y mantener una sesión propia del sistema que se revalida en cada solicitud.

## ADDED Requirements

### Requirement: Ingreso con cuenta Microsoft mediante el backend

El sistema SHALL permitir el ingreso con cuentas Microsoft personales y de cualquier organización. El backend SHALL conducir el intercambio con Microsoft como cliente confidencial y MUST NOT entregar al navegador tokens emitidos por Microsoft. El inicio y el retorno del ingreso SHALL ocurrir bajo rutas `/api` del mismo host que sirve el frontend.

#### Scenario: Ingreso aceptado

- **GIVEN** un usuario activo con un rol vigente cuyo UPN coincide con el mail verificado de su cuenta Microsoft
- **WHEN** inicia el ingreso desde la pantalla de login y se autentica en Microsoft
- **THEN** el sistema establece una sesión de Ars Docendi y lo redirige a la ruta que intentaba abrir o, si no había ninguna, a la ruta inicial

#### Scenario: El navegador no recibe tokens de Microsoft

- **WHEN** se completa un ingreso aceptado
- **THEN** el navegador recibe sólo la cookie de sesión de Ars Docendi y ninguna respuesta contiene tokens emitidos por Microsoft

#### Scenario: Ruta de retorno externa

- **GIVEN** una solicitud de ingreso cuya ruta de retorno apunta a otro sitio
- **WHEN** el ingreso se completa
- **THEN** el sistema redirige a la ruta inicial de Ars Docendi y nunca al sitio externo

#### Scenario: Error o cancelación en Microsoft

- **GIVEN** que Microsoft devuelve un error o la persona cancela el ingreso
- **WHEN** el flujo vuelve al sistema
- **THEN** el usuario llega a la pantalla de login en su estado de error de autenticación y sin sesión

### Requirement: El ingreso sólo verifica usuarios dados de alta

El ingreso MUST NOT crear usuarios, personas ni asignaciones de rol. El sistema SHALL aceptar el ingreso sólo cuando la cuenta corresponde a un usuario existente, activo y con al menos un rol vigente. Ante un rechazo, el sistema MUST NOT escribir en la base ni establecer sesión, y SHALL registrar en el log el motivo y si la cuenta es personal u organizacional, sin mail, nombre ni identificadores de la cuenta.

#### Scenario: Mail no registrado

- **GIVEN** una cuenta Microsoft con mail verificado que no corresponde a ningún usuario
- **WHEN** completa el ingreso
- **THEN** el sistema la lleva a la pantalla de login en su estado de acceso denegado, sin sesión y sin filas creadas o modificadas

#### Scenario: Usuario desactivado

- **GIVEN** un usuario desactivado cuyo UPN coincide con el mail verificado de la cuenta
- **WHEN** completa el ingreso
- **THEN** el sistema deniega el acceso de la misma forma

#### Scenario: Usuario sin rol vigente

- **GIVEN** un usuario activo sin asignaciones de rol vigentes
- **WHEN** completa el ingreso
- **THEN** el sistema deniega el acceso de la misma forma

#### Scenario: Rechazo registrado sin datos personales

- **WHEN** el sistema deniega un ingreso
- **THEN** el log registra el motivo y el tipo de cuenta, y no incluye mail, nombre ni identificadores de la cuenta

### Requirement: Sólo se confía en mails verificados

Para reconocer a un usuario por mail, el sistema MUST usar únicamente un mail que Microsoft informe como verificado: perteneciente a un dominio verificado por la organización de la cuenta, o de una cuenta personal de Microsoft. La comparación con el UPN SHALL ignorar mayúsculas y espacios en los extremos.

#### Scenario: Mail sin dominio verificado

- **GIVEN** una cuenta de una organización cuyo mail coincide con el UPN de un usuario pero no pertenece a un dominio verificado por esa organización
- **WHEN** completa el ingreso
- **THEN** el sistema deniega el acceso y no vincula la cuenta

#### Scenario: Diferencias de mayúsculas

- **GIVEN** un usuario con UPN `nombre@dominio.edu.ar`
- **WHEN** ingresa con una cuenta cuyo mail verificado es `Nombre@Dominio.edu.ar`
- **THEN** el sistema lo reconoce como ese usuario

### Requirement: Vínculo de la cuenta Microsoft desde el primer ingreso

En el primer ingreso aceptado, el sistema SHALL vincular al usuario el identificador de objeto y el tenant de la cuenta Microsoft. Desde entonces SHALL reconocerlo por ese vínculo y no por el mail, y MUST rechazar el ingreso de otra cuenta cuyo mail coincida con un usuario ya vinculado. Cada ingreso aceptado SHALL actualizar la fecha de último ingreso del usuario con el propio usuario como actor de la auditoría.

#### Scenario: Primer ingreso vincula la cuenta

- **GIVEN** un usuario activo sin cuenta Microsoft vinculada
- **WHEN** ingresa por primera vez con una cuenta cuyo mail verificado coincide con su UPN
- **THEN** el sistema registra el vínculo y la fecha de ingreso en ese usuario, sin crear filas nuevas

#### Scenario: Ingreso posterior por vínculo

- **GIVEN** un usuario con cuenta Microsoft vinculada
- **WHEN** cambian el mail de esa cuenta o el UPN del usuario y vuelve a ingresar con la misma cuenta
- **THEN** el sistema lo reconoce por el vínculo

#### Scenario: Otra cuenta con el mismo mail

- **GIVEN** un usuario vinculado a una cuenta Microsoft
- **WHEN** alguien ingresa con otra cuenta cuyo mail verificado coincide con el UPN de ese usuario
- **THEN** el sistema deniega el acceso y conserva el vínculo existente

#### Scenario: Último ingreso auditado

- **WHEN** un ingreso es aceptado
- **THEN** la fecha de último ingreso del usuario se actualiza y `audit.change_log` registra el cambio con ese usuario como actor

### Requirement: Sesión propia revalidada en cada solicitud

La sesión SHALL mantenerse en una cookie `HttpOnly`, `Secure` y `SameSite=Lax`, limitada al host que la emitió, que identifica al usuario sin contener su rol ni sus permisos. En cada solicitud autenticada, el sistema SHALL verificar que el usuario siga activo y resolver su rol y permisos desde la persistencia. Las solicitudes a la API sin sesión válida SHALL recibir `401` y nunca una redirección.

#### Scenario: Usuario desactivado con sesión abierta

- **GIVEN** un usuario con sesión iniciada
- **WHEN** un administrador lo desactiva y el usuario hace una nueva solicitud
- **THEN** la solicitud recibe `401` y la sesión deja de ser válida

#### Scenario: Cambio de rol con sesión abierta

- **GIVEN** un usuario con sesión iniciada
- **WHEN** un administrador cambia su rol o los permisos de su rol
- **THEN** la siguiente solicitud opera con el rol y los permisos vigentes, sin volver a ingresar

#### Scenario: API sin sesión

- **WHEN** el frontend consulta una ruta protegida sin cookie o con una cookie inválida
- **THEN** recibe `401` y el frontend lleva al usuario a la pantalla de login

#### Scenario: Consulta de la sesión vigente

- **GIVEN** una sesión válida
- **WHEN** el frontend consulta la sesión vigente
- **THEN** recibe el nombre para mostrar, el UPN, el código y el nombre del rol, y los permisos efectivos

### Requirement: Vencimiento y cierre de sesión

La sesión SHALL vencer tras un período de inactividad y al alcanzar una duración máxima desde el ingreso, ambos configurables. Cerrar sesión SHALL quitar la cookie de Ars Docendi del navegador sin modificar la sesión de la persona en Microsoft.

#### Scenario: Inactividad

- **GIVEN** una sesión sin solicitudes durante el período de inactividad configurado
- **WHEN** el usuario vuelve a operar
- **THEN** recibe `401` y debe ingresar nuevamente

#### Scenario: Duración máxima

- **GIVEN** una sesión con actividad continua que alcanzó la duración máxima configurada
- **WHEN** el usuario hace una nueva solicitud
- **THEN** recibe `401` y debe ingresar nuevamente

#### Scenario: Cierre de sesión

- **WHEN** el usuario cierra sesión
- **THEN** el navegador deja de tener la cookie de sesión, las solicitudes posteriores reciben `401` y la sesión de Microsoft queda como estaba

### Requirement: Protección contra solicitudes cruzadas

Las solicitudes que modifican estado y se autentican con la cookie de sesión MUST incluir una prueba anti-falsificación emitida por el sistema; sin ella, el sistema SHALL rechazarlas sin aplicar cambios.

#### Scenario: Mutación sin prueba anti-falsificación

- **GIVEN** una sesión válida
- **WHEN** llega una solicitud `POST`, `PUT`, `PATCH` o `DELETE` autenticada por cookie sin una prueba anti-falsificación válida
- **THEN** el sistema la rechaza y no aplica cambios

#### Scenario: Mutación desde el frontend

- **GIVEN** una sesión válida en el frontend
- **WHEN** el usuario confirma una operación que modifica datos
- **THEN** la solicitud incluye la prueba anti-falsificación y se procesa normalmente

### Requirement: Rol de la sesión

La sesión SHALL operar con el rol asignado al usuario y con los permisos de ese rol. Si el usuario tuviera más de un rol distinto vigente, el sistema SHALL usar el primero por nombre de forma determinística y registrar una advertencia sin datos personales, hasta que exista una forma de elegirlo.

#### Scenario: Usuario con un rol

- **GIVEN** un usuario con un único rol vigente, aunque tenga varias asignaciones de ámbito de ese rol
- **WHEN** opera con una sesión válida
- **THEN** la sesión expone ese rol y sus permisos, y el backend aplica los ámbitos de todas sus asignaciones de ese rol

#### Scenario: Usuario con varios roles

- **GIVEN** un usuario con dos roles distintos vigentes
- **WHEN** ingresa y opera con una sesión válida
- **THEN** la sesión usa el rol cuyo nombre ordena primero, el ingreso registra una advertencia en el log y los demás roles no aportan permisos

### Requirement: Administrador inicial por ambiente

Para que un ambiente sin administradores sea operable, el despliegue SHALL poder declarar en su configuración un administrador inicial. El arranque de migraciones SHALL darlo de alta, activo y con el rol de administración del sistema, sólo si no existe un usuario con ese UPN. Los datos del administrador inicial MUST NOT versionarse en el repositorio.

#### Scenario: Primer despliegue de producción

- **GIVEN** una base de producción sin usuarios y un administrador inicial declarado en la configuración del despliegue
- **WHEN** se ejecuta el arranque de migraciones
- **THEN** existe un usuario activo con ese UPN, su persona y el rol de administración del sistema, y puede ingresar con su cuenta Microsoft

#### Scenario: Despliegues posteriores

- **GIVEN** un administrador inicial que ya existe, aunque haya sido modificado o desactivado desde la administración
- **WHEN** se vuelve a ejecutar el arranque de migraciones
- **THEN** el sistema no lo modifica ni lo reactiva

#### Scenario: Ambiente sin administrador inicial declarado

- **WHEN** se ejecuta el arranque de migraciones sin administrador inicial en la configuración
- **THEN** no se crea ningún usuario

### Requirement: Habilitación por ambiente

El ingreso con Microsoft SHALL habilitarse por configuración. En producción MUST estar habilitado y configurado; si no lo está, el backend MUST NOT empezar a atender solicitudes. En los ambientes `pr-N` SHALL permanecer deshabilitado; en staging SHALL convivir con el selector de identidades de desarrollo, y en desarrollo local SHALL ser opcional. Cuando está deshabilitado, sus rutas MUST NOT estar registradas y la pantalla de login MUST NOT ofrecerlo.

#### Scenario: Producción sin configuración

- **GIVEN** una instancia en ambiente de producción sin el ingreso con Microsoft habilitado o sin sus credenciales
- **WHEN** el backend arranca para atender solicitudes
- **THEN** el arranque falla con un error explícito que no expone secretos

#### Scenario: Ambiente de pull request

- **GIVEN** un ambiente `pr-N`
- **WHEN** el usuario abre la pantalla de login
- **THEN** no se ofrece el ingreso con Microsoft y sus rutas no existen en el backend

#### Scenario: Staging con ambos accesos

- **GIVEN** staging con el ingreso con Microsoft y el selector de desarrollo habilitados
- **WHEN** el usuario abre la pantalla de login
- **THEN** la acción principal inicia el ingreso con Microsoft y el selector se ofrece como acceso secundario
