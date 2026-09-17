# gestionar-permisos-rol Specification

## Purpose

Permite consultar y actualizar los permisos asignados a cada rol institucional.

## Requirements

### Requirement: Visualización de permisos por rol

Cuando un rol está seleccionado en `/roles`, el panel derecho SHALL mostrar todos los permisos existentes del sistema, cada uno con un checkbox que refleja si el rol actualmente tiene ese permiso asignado. Esto SHALL aplicar tanto a roles de sistema como personalizados.

#### Scenario: Panel derecho muestra todos los permisos al seleccionar un rol

- **WHEN** el operador selecciona un rol en el panel lateral
- **THEN** el panel derecho lista todos los permisos del sistema con sus nombres y estados actuales

#### Scenario: Checkboxes reflejan el estado actual de membresía

- **WHEN** se muestran los permisos de un rol
- **THEN** los permisos asignados aparecen marcados y los no asignados desmarcados, sin inferir el estado a partir del nombre del rol

### Requirement: Modificación de permisos de un rol

El operador con `roles.gestionar_membresia` SHALL poder marcar y desmarcar los checkboxes de permisos del rol seleccionado, incluidos los roles de sistema. Los cambios SHALL aplicarse únicamente al confirmar con el botón "Guardar cambios" y SHALL conservar el control de concurrencia del rol.

#### Scenario: Marcar un permiso lo agrega visualmente

- **WHEN** el operador marca el checkbox de un permiso no asignado
- **THEN** el checkbox queda marcado sin persistirse todavía

#### Scenario: Desmarcar un permiso lo quita visualmente

- **WHEN** el operador desmarca el checkbox de un permiso asignado
- **THEN** el checkbox queda desmarcado sin persistirse todavía

#### Scenario: Guardar cambios persiste la membresía

- **WHEN** el operador hace clic en "Guardar cambios"
- **THEN** la API reemplaza el conjunto del rol seleccionado, la lista y el panel se actualizan y una nueva sesión puede obtener esos permisos

#### Scenario: Cambiar de rol sin guardar descarta los cambios pendientes

- **WHEN** el operador selecciona un rol diferente sin haber guardado cambios en el rol actual
- **THEN** los cambios pendientes se descartan y el nuevo rol se muestra con su estado guardado

#### Scenario: Conflicto de concurrencia conserva los cambios pendientes

- **WHEN** otro operador modifica el rol antes de guardar
- **THEN** la pantalla muestra el conflicto, no confirma la membresía y permite recargar el estado antes de reintentar
