## MODIFIED Requirements

### Requirement: Edición de rol existente

La pantalla `/roles` SHALL permitir editar el Nombre y la Descripción de un rol personalizado activo. El Nombre SHALL seguir siendo único y obligatorio tras la edición. En un rol de sistema, el nombre, la descripción, el código y el ámbito SHALL mostrarse sólo como lectura; sus permisos se gestionan en el panel de membresía.

#### Scenario: Modal de edición se abre con datos pre-cargados

- **WHEN** el operador hace clic en "Editar" en una fila de la lista
- **THEN** se abre un modal con los campos del rol pre-poblados y con los metadatos de un rol de sistema bloqueados

#### Scenario: Edición exitosa actualiza la tabla

- **WHEN** el operador modifica los campos permitidos de un rol personalizado y confirma
- **THEN** el modal se cierra y la tabla refleja los nuevos valores

#### Scenario: Edición exitosa actualiza la lista

- **WHEN** el operador modifica los campos permitidos de un rol personalizado y confirma
- **THEN** el modal se cierra, la lista refleja los nuevos valores y el código del rol permanece estable

#### Scenario: Rol de sistema no puede renombrarse

- **WHEN** el operador abre la edición de un rol de sistema
- **THEN** no puede modificar ni guardar cambios de nombre o descripción

#### Scenario: Nombre vacío bloquea el envío

- **WHEN** el operador borra el campo Nombre e intenta guardar un rol personalizado
- **THEN** el sistema muestra un error de validación y no actualiza el rol

#### Scenario: Nombre duplicado con otro rol bloquea el envío

- **WHEN** el operador cambia el Nombre a uno que ya usa otro rol activo
- **THEN** el sistema muestra un error inline y no actualiza el rol

#### Scenario: El propio nombre no es considerado duplicado

- **WHEN** el operador guarda el rol sin cambiar el Nombre
- **THEN** la validación de unicidad no rechaza el nombre actual del propio rol
