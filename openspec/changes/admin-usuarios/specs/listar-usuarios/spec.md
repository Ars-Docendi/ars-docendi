## ADDED Requirements

### Requirement: Tabla de usuarios visible para Secretaría y Administración

La página `/usuarios` SHALL mostrar una tabla con todos los usuarios del sistema. Solo los roles `Secretaría` y `Administración` SHALL poder acceder a esta ruta; cualquier otro rol SHALL ser redirigido a `/`.

#### Scenario: Secretaría accede a la página

- **WHEN** un usuario con rol `Secretaría` navega a `/usuarios`
- **THEN** se muestra la tabla con columnas: Apellido y Nombre, Documento, Legajo, UPN/Email, Roles, Estado, Acciones

#### Scenario: Administración accede a la página

- **WHEN** un usuario con rol `Administración` navega a `/usuarios`
- **THEN** se muestra la tabla con las mismas columnas

#### Scenario: Otro rol intenta acceder directamente

- **WHEN** un usuario con rol distinto navega a `/usuarios`
- **THEN** es redirigido a `/` sin ver el contenido

### Requirement: Columnas y datos de la tabla

La tabla SHALL mostrar por fila: Apellido y Nombre (formato "Apellido, Nombre"), Documento (DNI), Legajo, UPN/email, lista de roles asignados (múltiples), e indicador visual de estado con `StatusBadge`. La tabla SHALL tener scroll horizontal cuando el contenido supere el ancho del viewport.

#### Scenario: Formato del nombre

- **WHEN** se muestra la columna "Apellido y Nombre"
- **THEN** el texto es "Apellido, Nombre" usando el helper `nombreCompleto(u)`

#### Scenario: Usuario activo

- **WHEN** un usuario tiene `is_active = true`
- **THEN** su fila muestra un `StatusBadge` verde con label "Activo" (`kind="aprobado"`)

#### Scenario: Usuario inactivo

- **WHEN** un usuario tiene `is_active = false`
- **THEN** su fila muestra un `StatusBadge` rojo con label "Inactivo" (`kind="rechazado"`)

#### Scenario: Usuario con múltiples roles

- **WHEN** un usuario tiene más de un rol asignado
- **THEN** su fila muestra todos los roles como badges individuales

### Requirement: Acciones en la tabla con botones ghost

Cada fila SHALL tener botones ghost: "Editar" (siempre) y "Desactivar" o "Activar" (según estado).

#### Scenario: Fila de usuario activo

- **WHEN** el usuario está activo
- **THEN** aparecen "Editar" (ghost) y "Desactivar" (ghost)

#### Scenario: Fila de usuario inactivo

- **WHEN** el usuario está inactivo
- **THEN** aparecen "Editar" (ghost) y "Activar" (ghost)

### Requirement: Barra de filtros en dos filas

La página SHALL mostrar una barra con **fila 1** fija (Apellido, Nombre, Documento, selector "Añadir filtro…") y **fila 2** condicional (filtros opcionales activos). Los filtros opcionales disponibles son: Legajo, Mail/UPN, Rol, Estado.

Todos los filtros de texto SHALL ser insensibles a tildes: buscar "Lopez" encuentra "López".

#### Scenario: Filtros fijos siempre visibles

- **WHEN** el operador abre la página
- **THEN** los campos "Filtrar por apellido", "Filtrar por nombre" y "Filtrar por documento" están siempre presentes como entradas separadas

#### Scenario: Añadir filtro opcional

- **WHEN** el operador selecciona una opción en "Añadir filtro…"
- **THEN** el control correspondiente aparece en la fila 2

#### Scenario: Quitar filtro opcional

- **WHEN** el operador hace clic en × de un filtro activo
- **THEN** ese control desaparece y su valor se resetea

#### Scenario: Filtrar por apellido (fijo)

- **WHEN** el operador escribe texto en el campo "Filtrar por apellido"
- **THEN** la tabla muestra solo las filas cuyo `apellido` contenga ese texto (insensible a tildes y mayúsculas)

#### Scenario: Filtrar por nombre (fijo)

- **WHEN** el operador escribe texto en el campo "Filtrar por nombre"
- **THEN** la tabla muestra solo las filas cuyo `nombre` contenga ese texto (insensible a tildes y mayúsculas)

#### Scenario: Filtrar por documento (fijo)

- **WHEN** el operador escribe texto en el campo de Documento
- **THEN** la tabla muestra solo filas cuyo `documento` contenga ese texto

#### Scenario: Filtrar por legajo (opcional)

- **WHEN** el filtro de Legajo está activo y el operador escribe texto
- **THEN** la tabla muestra solo filas cuyo `legajo` contenga ese texto

#### Scenario: Filtrar por mail/UPN (opcional)

- **WHEN** el filtro de Mail/UPN está activo y el operador escribe texto
- **THEN** la tabla muestra solo filas cuya `upn` contenga ese texto

#### Scenario: Filtrar por rol (opcional)

- **WHEN** el operador selecciona un rol en el filtro de Rol
- **THEN** la tabla muestra solo filas cuyo array `roles` contenga ese rol

#### Scenario: Filtrar por estado (opcional)

- **WHEN** el operador selecciona "Activo" o "Inactivo"
- **THEN** la tabla muestra solo filas con el `is_active` correspondiente

#### Scenario: Ancho de selectores

- **WHEN** se muestra un selector de filtro (Rol, Estado, "Añadir filtro…")
- **THEN** el ancho del selector está determinado por la opción más larga (`width: auto`)

### Requirement: Sidebar muestra entrada "Usuarios" para Secretaría y Administración

El sidebar SHALL mostrar el ítem de navegación "Usuarios" (dentro del grupo "Configuración") únicamente a los usuarios con rol `Secretaría` o `Administración`. Para cualquier otro rol el ítem SHALL estar oculto.

#### Scenario: Secretaría ve el ítem

- **WHEN** el usuario logueado tiene rol `Secretaría`
- **THEN** el sidebar muestra "Usuarios" en el grupo "Configuración"

#### Scenario: Otro rol no ve el ítem

- **WHEN** el usuario logueado tiene cualquier otro rol
- **THEN** el ítem "Usuarios" no aparece en el sidebar
