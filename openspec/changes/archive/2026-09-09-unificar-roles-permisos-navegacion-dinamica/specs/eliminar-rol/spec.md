## Purpose

Permite retirar roles personalizados sin borrar referencias históricas ni afectar la integridad de las membresías existentes.

## ADDED Requirements

### Requirement: Eliminación lógica de roles personalizados

La API SHALL exponer `DELETE /api/administracion/roles/{id}` para desactivar lógicamente un rol personalizado. La operación MUST exigir control de concurrencia, conservar el registro y sus relaciones para auditoría, impedir nuevas asignaciones y excluir el rol inactivo de los catálogos operativos. Los roles de sistema MUST permanecer activos y no eliminables.

#### Scenario: Eliminar un rol personalizado

- **GIVEN** un rol personalizado activo, un actor con `roles.administrar` y su versión vigente
- **WHEN** confirma la eliminación desde `/roles`
- **THEN** la API marca el rol como inactivo, conserva sus referencias históricas y la pantalla lo quita del listado operativo

#### Scenario: Impedir eliminación de un rol de sistema

- **GIVEN** un rol con `es_sistema = true`
- **WHEN** se intenta eliminarlo por la API o desde la pantalla
- **THEN** la operación es rechazada sin modificar el rol y la UI no ofrece una acción eliminatoria efectiva

#### Scenario: Rol inactivo no puede asignarse

- **GIVEN** un rol personalizado eliminado lógicamente
- **WHEN** se intenta asignarlo a un usuario o usarlo como rol base
- **THEN** la API rechaza la referencia y no aplica cambios parciales

#### Scenario: Eliminación con versión desactualizada

- **GIVEN** un rol personalizado modificado por otra sesión
- **WHEN** se confirma su eliminación con una versión anterior
- **THEN** la API responde conflicto y conserva el estado vigente del rol
