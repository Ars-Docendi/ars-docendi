## Purpose

Listado y filtrado de los usuarios del sistema en la página `/usuarios`, accesible solo para los roles `Secretaría` y `Administración`. Presenta una tabla con los datos de persona, roles y estado de cada usuario, con una barra de filtros en dos filas y entrada de navegación en el sidebar.

## Requirements

### Requirement: Listado desde la identidad persistida

La página de usuarios MUST obtener el listado desde la API de administración y MUST mostrar el estado canónico persistido, los roles resumidos sin duplicados, las membresías con sus ámbitos y el perfil docente, sin recurrir a un listado local ante respuestas vacías o fallidas.

#### Scenario: Consulta exitosa

- **GIVEN** usuarios persistidos en `identity`
- **WHEN** un operador autorizado abre `/usuarios`
- **THEN** la tabla muestra los usuarios devueltos por la API con sus personas, roles únicos, ámbitos, perfil docente y estado

#### Scenario: Cambios de otra sesión

- **GIVEN** un usuario modificado por otra sesión
- **WHEN** el operador refresca el listado
- **THEN** la tabla refleja el nuevo estado persistido

### Requirement: Tabla de usuarios visible para Secretaría y Administración

La página `/usuarios` SHALL mostrar una tabla con todos los usuarios del sistema. El acceso SHALL requerir el permiso efectivo de consulta de usuarios, no un nombre o conjunto fijo de roles; cualquier usuario sin ese permiso SHALL ser redirigido a `/`.

#### Scenario: Usuario con permiso accede a la página

- **WHEN** un usuario autenticado con el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** se muestra la tabla con columnas: Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Estado, Acciones

#### Scenario: Secretaría accede a la página

- **WHEN** un usuario Secretaría con el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** se muestra la tabla con columnas: Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Estado, Acciones

#### Scenario: Administración accede a la página

- **WHEN** un usuario Administración con el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** se muestra la tabla con las mismas columnas

#### Scenario: Usuario sin permiso intenta acceder directamente

- **WHEN** un usuario autenticado sin el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** es redirigido a `/` sin ver el contenido

#### Scenario: Otro rol intenta acceder directamente

- **WHEN** un usuario autenticado sin el permiso de consulta de usuarios navega a `/usuarios`
- **THEN** es redirigido a `/` sin ver el contenido

### Requirement: Columnas y datos de la tabla

La tabla SHALL mostrar por fila: Apellido y Nombre, Documento, Legajo, UPN/email, roles resumidos sin repetir, indicador de perfil docente, estado visual y acciones. SHALL conservar scroll horizontal cuando el contenido supere el ancho del viewport.

#### Scenario: Formato del nombre

- **WHEN** se muestra la columna "Apellido y Nombre"
- **THEN** el texto es "Apellido, Nombre"

#### Scenario: Usuario con un rol en varias materias

- **WHEN** un usuario tiene el mismo rol en varias materias
- **THEN** la tabla muestra un solo badge de ese rol y su cantidad o resumen de ámbitos

#### Scenario: Usuario con roles mixtos

- **WHEN** un usuario es `docente` en una materia y `jefe_catedra` en otra
- **THEN** la tabla muestra ambos roles como badges únicos y marca el perfil docente

#### Scenario: Usuario con múltiples roles

- **WHEN** un usuario tiene más de un rol asignado
- **THEN** su fila muestra todos los roles resumidos como badges individuales sin repetirlos por ámbito

#### Scenario: Usuario activo

- **WHEN** un usuario tiene `is_active = true`
- **THEN** su fila muestra un `StatusBadge` verde con label "Activo"

#### Scenario: Usuario inactivo

- **WHEN** un usuario tiene `is_active = false`
- **THEN** su fila muestra un `StatusBadge` rojo con label "Inactivo"

### Requirement: Acciones en la tabla con botones ghost

Cada fila SHALL tener botones ghost: "Editar" (siempre) y "Desactivar" o "Activar" (según estado).

#### Scenario: Fila de usuario activo

- **WHEN** el usuario está activo
- **THEN** aparecen "Editar" (ghost) y "Desactivar" (ghost)

#### Scenario: Fila de usuario inactivo

- **WHEN** el usuario está inactivo
- **THEN** aparecen "Editar" (ghost) y "Activar" (ghost)

### Requirement: Filtros por encabezado en la tabla de usuarios

La página SHALL ofrecer un control de filtro accesible en cada encabezado filtrable de la tabla de Usuarios. Los encabezados filtrables SHALL ser Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Perfil docente y Estado. La columna Acciones MUST NOT ofrecer filtro.

El menú de una columna textual SHALL permitir buscar por coincidencia parcial sin distinguir mayúsculas ni tildes. El menú de una columna categórica SHALL permitir seleccionar uno o más valores presentes en los datos cargados, incluyendo la opción de valores sin dato cuando corresponda. Los filtros de distintas columnas SHALL combinarse con lógica AND.

#### Scenario: Abrir el filtro de una columna

- **WHEN** el operador activa el control de filtro del encabezado "Roles"
- **THEN** se muestra un menú asociado a ese encabezado con los roles disponibles y una acción para limpiar el filtro

#### Scenario: Buscar una columna textual

- **WHEN** el operador escribe "lopez" en el filtro de Apellido y Nombre
- **THEN** la tabla muestra sólo usuarios cuyo apellido o nombre normalizado contiene "lopez"

#### Scenario: Seleccionar valores de una columna categórica

- **WHEN** el operador selecciona "Con perfil docente" en el filtro de Perfil docente
- **THEN** la tabla muestra sólo usuarios con `perfilDocente.esDocente = true`

#### Scenario: Combinar filtros de encabezados

- **WHEN** el operador filtra Estado por "Activo" y Roles por "Docente"
- **THEN** la tabla muestra sólo usuarios que cumplen ambos criterios

#### Scenario: Limpiar un filtro de encabezado

- **WHEN** el operador activa "Limpiar filtro" en una columna filtrada
- **THEN** el criterio de esa columna se elimina y los resultados se recalculan conservando los filtros de las demás columnas

#### Scenario: Cerrar un menú sin perder filtros

- **WHEN** el operador cierra el menú de una columna que ya tiene un filtro aplicado
- **THEN** el filtro continúa visible mediante un indicador del encabezado y sigue afectando la tabla

### Requirement: Ordenamiento por encabezado en la tabla de usuarios

La tabla SHALL permitir ordenar desde los encabezados Apellido y Nombre, Documento, Legajo, UPN/Email y Estado. El orden SHALL aplicarse únicamente sobre las filas que cumplen los filtros activos. La columna Acciones MUST NOT ser ordenable.

Cada encabezado ordenable SHALL alternar entre orden ascendente, descendente y sin orden manual. Al quitar el orden manual, la tabla SHALL recuperar su orden predeterminado por Apellido y Nombre.

#### Scenario: Ordenar usuarios por apellido

- **WHEN** el operador activa el orden ascendente del encabezado Apellido y Nombre
- **THEN** las filas visibles se presentan por apellido y nombre en orden alfabético, sin distinguir tildes

#### Scenario: Ordenar legajos numéricos

- **WHEN** el operador activa el orden ascendente del encabezado Legajo
- **THEN** los legajos se ordenan por valor numérico cuando corresponda, y no por comparación lexicográfica

#### Scenario: Quitar el orden manual

- **WHEN** el operador activa por tercera vez el encabezado que está ordenado en sentido descendente
- **THEN** el indicador de orden desaparece y la tabla vuelve al orden predeterminado

#### Scenario: Ordenar después de filtrar

- **WHEN** existen filtros activos y el operador ordena una columna
- **THEN** sólo se reordenan las filas resultantes, sin recuperar filas excluidas por los filtros

### Requirement: Accesibilidad de los controles de encabezado

Los controles de filtro y ordenamiento SHALL poder operarse con teclado y SHALL comunicar su estado al lector de pantalla. Abrir o cerrar un menú de encabezado MUST NOT activar acciones de una fila.

#### Scenario: Estado de orden accesible

- **WHEN** una columna queda ordenada en sentido ascendente o descendente
- **THEN** el encabezado comunica su dirección de orden al lector de pantalla

#### Scenario: Operación por teclado

- **WHEN** el operador enfoca el control de filtro y presiona Enter o Espacio
- **THEN** se abre el menú de esa columna sin requerir el uso del mouse

### Requirement: Sidebar muestra entrada "Usuarios" para Secretaría y Administración

El sidebar SHALL mostrar el ítem de navegación "Usuarios" dentro del grupo "Configuración" únicamente a los usuarios cuya sesión tenga el permiso de consulta de usuarios. Para cualquier otro usuario el ítem SHALL estar oculto.

#### Scenario: Secretaría ve el ítem

- **WHEN** el usuario logueado tiene el permiso de consulta de usuarios
- **THEN** el sidebar muestra "Usuarios" en el grupo "Configuración"

#### Scenario: Otro rol no ve el ítem

- **WHEN** el usuario logueado no tiene el permiso de consulta de usuarios
- **THEN** el ítem "Usuarios" no aparece en el sidebar

### Requirement: La tabla de Usuarios no muestra ámbitos

La tabla visible de `/usuarios` MUST mostrar los datos administrativos definidos para la cuenta
sin renderizar una columna `Ámbitos`. Las membresías y sus ámbitos MUST conservarse disponibles
para el formulario de edición y no deben eliminarse del contrato ni del estado de la pantalla.

#### Scenario: Usuario con membresías

- **GIVEN** una cuenta con una o más membresías de rol en distintos ámbitos
- **WHEN** un operador autorizado consulta `/usuarios`
- **THEN** la tabla muestra roles, estado, perfil docente y acciones sin incluir el encabezado ni
  la celda `Ámbitos`

#### Scenario: Edición conserva las membresías

- **GIVEN** un usuario con membresías cargadas en la consulta
- **WHEN** el operador abre "Editar usuario"
- **THEN** el formulario conserva esas membresías para editarlas aunque no exista una columna
  `Ámbitos` en la tabla
