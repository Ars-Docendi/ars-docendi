## MODIFIED Requirements

### Requirement: Columnas y datos de la tabla

La tabla SHALL mostrar por fila: Apellido y Nombre, Documento, Legajo, UPN/email, roles resumidos sin repetir, indicador de perfil docente, estado visual, estado de la cuenta Microsoft y acciones. SHALL conservar scroll horizontal cuando el contenido supere el ancho del viewport.

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

#### Scenario: Cuenta Microsoft vinculada

- **WHEN** un usuario ya ingresó con su cuenta Microsoft
- **THEN** la columna "Cuenta Microsoft" indica "Vinculada" junto con la fecha de su último ingreso

#### Scenario: Cuenta Microsoft pendiente

- **WHEN** un usuario todavía no ingresó
- **THEN** la columna "Cuenta Microsoft" indica "Pendiente de primer ingreso"

### Requirement: Acciones en la tabla con botones ghost

Cada fila SHALL tener botones ghost: "Editar" (siempre), "Desactivar" o "Activar" (según estado) y "Desvincular cuenta" sólo cuando la cuenta Microsoft está vinculada y el operador tiene permiso de administración de usuarios.

#### Scenario: Fila de usuario activo

- **WHEN** el usuario está activo
- **THEN** aparecen "Editar" (ghost) y "Desactivar" (ghost)

#### Scenario: Fila de usuario inactivo

- **WHEN** el usuario está inactivo
- **THEN** aparecen "Editar" (ghost) y "Activar" (ghost)

#### Scenario: Fila de usuario con cuenta vinculada

- **GIVEN** un operador con permiso de administración de usuarios
- **WHEN** el usuario tiene la cuenta Microsoft vinculada
- **THEN** además aparece "Desvincular cuenta" (ghost), que abre un modal de confirmación con el nombre del usuario y el efecto de la acción

#### Scenario: Fila de usuario pendiente de primer ingreso

- **WHEN** el usuario no tiene la cuenta Microsoft vinculada
- **THEN** no aparece "Desvincular cuenta"
