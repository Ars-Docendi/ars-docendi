## ADDED Requirements

### Requirement: Visualización de permisos por rol

Cuando un rol está seleccionado en `/membresia-roles`, el panel derecho SHALL mostrar todos los permisos existentes del sistema, cada uno con un checkbox que refleja si el rol actualmente tiene ese permiso asignado.

#### Scenario: Panel derecho muestra todos los permisos al seleccionar un rol

- **WHEN** el operador selecciona un rol
- **THEN** el panel derecho lista todos los permisos del sistema con sus nombres

#### Scenario: Checkboxes reflejan el estado actual de membresía

- **WHEN** se muestran los permisos de un rol
- **THEN** los permisos que el rol tiene asignados aparecen con el checkbox marcado, y los no asignados con el checkbox desmarcado

### Requirement: Modificación de permisos de un rol

El operador SHALL poder marcar y desmarcar los checkboxes de permisos del rol seleccionado. Los cambios SHALL aplicarse únicamente al confirmar con el botón "Guardar cambios".

#### Scenario: Marcar un permiso lo agrega visualmente

- **WHEN** el operador marca el checkbox de un permiso no asignado
- **THEN** el checkbox queda marcado sin aplicarse todavía al store

#### Scenario: Desmarcar un permiso lo quita visualmente

- **WHEN** el operador desmarca el checkbox de un permiso asignado
- **THEN** el checkbox queda desmarcado sin aplicarse todavía al store

#### Scenario: Guardar cambios persiste la membresía

- **WHEN** el operador hace clic en "Guardar cambios"
- **THEN** el store se actualiza con los permisos resultantes del rol seleccionado y el panel refleja el nuevo estado

#### Scenario: Cambiar de rol sin guardar descarta los cambios pendientes

- **WHEN** el operador selecciona un rol diferente sin haber guardado cambios en el rol actual
- **THEN** los cambios pendientes se descartan y el nuevo rol se muestra con su estado actual guardado
