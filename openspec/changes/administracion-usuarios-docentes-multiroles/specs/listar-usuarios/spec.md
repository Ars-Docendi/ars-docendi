## MODIFIED Requirements

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

### Requirement: Barra de filtros en dos filas

La página SHALL mostrar una barra con fila 1 fija y fila 2 condicional. Los filtros opcionales disponibles SHALL incluir Legajo, Mail/UPN, Rol, Perfil docente y Estado. Todos los filtros de texto SHALL ser insensibles a tildes.

#### Scenario: Filtrar sólo docentes

- **WHEN** el operador selecciona "Docentes" en el filtro de Perfil docente
- **THEN** la tabla muestra sólo usuarios con perfil docente

#### Scenario: Filtros fijos siempre visibles

- **WHEN** el operador abre la página
- **THEN** los campos "Filtrar por apellido", "Filtrar por nombre" y "Filtrar por documento" están siempre presentes como entradas separadas

#### Scenario: Añadir filtro opcional

- **WHEN** el operador selecciona una opción en "Añadir filtro…"
- **THEN** el control correspondiente aparece en la fila 2

#### Scenario: Filtrar por rol

- **WHEN** el operador selecciona un rol
- **THEN** la tabla muestra usuarios cuyo resumen de roles contiene ese rol, aunque tenga varios ámbitos

#### Scenario: Quitar filtro opcional

- **WHEN** el operador hace clic en × de un filtro activo
- **THEN** ese control desaparece y su valor se resetea

#### Scenario: Filtrar por apellido (fijo)

- **WHEN** el operador escribe texto en el campo "Filtrar por apellido"
- **THEN** la tabla muestra sólo filas cuyo apellido contenga ese texto, sin distinguir tildes ni mayúsculas

#### Scenario: Filtrar por nombre (fijo)

- **WHEN** el operador escribe texto en el campo "Filtrar por nombre"
- **THEN** la tabla muestra sólo filas cuyo nombre contenga ese texto, sin distinguir tildes ni mayúsculas

#### Scenario: Filtrar por documento (fijo)

- **WHEN** el operador escribe texto en el campo de Documento
- **THEN** la tabla muestra sólo filas cuyo documento contenga ese texto

#### Scenario: Filtrar por legajo (opcional)

- **WHEN** el filtro de Legajo está activo y el operador escribe texto
- **THEN** la tabla muestra sólo filas cuyo legajo contenga ese texto

#### Scenario: Filtrar por mail/UPN (opcional)

- **WHEN** el filtro de Mail/UPN está activo y el operador escribe texto
- **THEN** la tabla muestra sólo filas cuya UPN contenga ese texto

#### Scenario: Filtrar por rol (opcional)

- **WHEN** el operador selecciona un rol en el filtro de Rol
- **THEN** la tabla muestra sólo filas cuyo resumen de roles contenga ese rol, aunque tenga varios ámbitos

#### Scenario: Filtrar por estado (opcional)

- **WHEN** el operador selecciona "Activo" o "Inactivo"
- **THEN** la tabla muestra sólo filas con el estado correspondiente

#### Scenario: Ancho de selectores

- **WHEN** se muestra un selector de filtro de Rol, Estado o "Añadir filtro…"
- **THEN** el ancho del selector se determina por la opción más larga
