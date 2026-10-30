## MODIFIED Requirements

### Requirement: Eliminación lógica de roles personalizados

La API SHALL exponer `POST /api/administracion/roles/{id}/desactivar` y `POST /api/administracion/roles/{id}/activar` para desactivar y reactivar un rol personalizado. Ambas operaciones MUST exigir control de concurrencia y conservar el registro, sus permisos y sus asignaciones. Mientras un rol está inactivo sus asignaciones MUST NOT aportar permisos y el rol MUST NOT poder asignarse. Reactivar MUST rechazarse si otro rol activo usa el mismo nombre. Los roles de sistema MUST permanecer activos y no desactivables.

#### Scenario: Eliminar un rol personalizado

- **GIVEN** un rol personalizado activo, un actor con `roles.administrar` y su versión vigente
- **WHEN** confirma "Desactivar" desde `/roles`
- **THEN** la API marca el rol como inactivo, conserva sus permisos y asignaciones, y la grilla lo muestra como Inactivo con la acción "Activar"

#### Scenario: Reactivar un rol personalizado

- **GIVEN** un rol personalizado inactivo cuyo nombre no usa ningún rol activo
- **WHEN** el actor confirma "Activar"
- **THEN** el rol vuelve a estar activo y sus asignaciones vuelven a aportar sus permisos

#### Scenario: Reactivar con nombre ocupado

- **GIVEN** un rol inactivo y otro rol activo con el mismo nombre
- **WHEN** el actor confirma "Activar"
- **THEN** la API responde conflicto de nombre y el rol sigue inactivo

#### Scenario: Impedir eliminación de un rol de sistema

- **GIVEN** un rol con `es_sistema = true`
- **WHEN** se intenta desactivarlo por la API o desde la pantalla
- **THEN** la operación es rechazada sin modificar el rol y la grilla no ofrece "Desactivar"

#### Scenario: Rol inactivo no puede asignarse

- **GIVEN** un rol personalizado desactivado
- **WHEN** se intenta asignarlo a un usuario
- **THEN** la API rechaza la referencia y no aplica cambios parciales

#### Scenario: Eliminación con versión desactualizada

- **GIVEN** un rol personalizado modificado por otra sesión
- **WHEN** se confirma su desactivación con una versión anterior
- **THEN** la API responde conflicto y conserva el estado vigente del rol
