# navegacion-por-permisos Specification

## Purpose

Hace que la sesión, las rutas y la navegación del frontend se resuelvan por permisos efectivos del backend, sin depender de un catálogo cerrado de nombres de roles.

## Requirements

### Requirement: Sesión con rol y permisos efectivos

El frontend SHALL construir la sesión a partir del rol activo y sus permisos efectivos entregados por el backend. MUST conservar el código del rol para identificarlo sin convertir nombres desconocidos en `null`, y SHALL aceptar roles personalizados e institucionales con cualquier nombre válido.

#### Scenario: Login con rol personalizado

- **GIVEN** un usuario activo con un rol personalizado activo y permisos asignados
- **WHEN** inicia sesión o se valida la sesión
- **THEN** el frontend conserva la sesión, muestra el nombre del rol y recibe sus permisos sin exigir que el rol pertenezca a una unión fija

#### Scenario: Rol institucional con permisos modificados

- **GIVEN** un rol institucional al que se le quitó o agregó un permiso
- **WHEN** el usuario vuelve a validar su sesión
- **THEN** la sesión contiene el conjunto persistido actualizado

### Requirement: Navegación derivada de permisos

La sidebar SHALL calcular sus grupos y enlaces desde los permisos efectivos de la sesión mediante un registro de rutas propiedad del código. MUST ocultar los enlaces sin permiso, no mostrar grupos vacíos y conservar una etiqueta accesible para cada enlace. La selección de rutas NO SHALL depender del nombre ni del tipo institucional del rol.

#### Scenario: Permiso habilita una pantalla

- **GIVEN** una sesión con el permiso requerido por una pantalla
- **WHEN** se renderiza la sidebar
- **THEN** aparece el enlace correspondiente dentro de su grupo

#### Scenario: Permiso revocado oculta una pantalla

- **GIVEN** una sesión sin el permiso requerido por una pantalla
- **WHEN** se renderiza la sidebar
- **THEN** el enlace no aparece y no queda un grupo vacío

#### Scenario: Rol personalizado con permisos de revisión

- **GIVEN** un rol personalizado con `designaciones.revisar`
- **WHEN** se renderiza la sidebar
- **THEN** aparece Revisión aunque el nombre del rol no sea institucional

### Requirement: Guardas de ruta por permiso

Las rutas protegidas del frontend SHALL verificar el permiso requerido, no el nombre del rol. Una ruta sin permiso MUST redirigir sin renderizar su contenido. La autorización del backend SHALL seguir siendo la autoridad final para cada API y acción.

#### Scenario: Acceso directo con permiso

- **GIVEN** un usuario autenticado con el permiso de una ruta
- **WHEN** navega directamente a su URL
- **THEN** la ruta se renderiza y sus consultas usan la sesión vigente

#### Scenario: Acceso directo sin permiso

- **GIVEN** un usuario autenticado sin el permiso de una ruta
- **WHEN** navega directamente a su URL
- **THEN** el frontend lo redirige a `/` sin mostrar la pantalla

### Requirement: Actualización de permisos visible en el frontend

Después de modificar la membresía de cualquier rol, las consultas de roles, la sesión de desarrollo y la navegación SHALL poder reflejar el nuevo conjunto de permisos sin depender de un nombre de rol hardcodeado. Las mutaciones MUST invalidar o renovar los datos necesarios para que un usuario afectado no conserve una navegación obsoleta después de validar nuevamente su sesión.

#### Scenario: Agregar permiso a un rol

- **GIVEN** un operador agrega un permiso a un rol
- **WHEN** un usuario de ese rol vuelve a validar su sesión
- **THEN** la pantalla habilitada aparece en la sidebar y su ruta deja de ser rechazada por el guard del frontend

#### Scenario: Quitar permiso a un rol

- **GIVEN** un operador quita un permiso a un rol
- **WHEN** un usuario de ese rol vuelve a validar su sesión
- **THEN** la pantalla desaparece de la sidebar y el acceso directo queda bloqueado por el frontend y el backend
