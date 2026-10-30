## MODIFIED Requirements

### Requirement: Edición de rol existente

La pantalla completa `/roles/:rolId` SHALL permitir editar el Nombre, la Descripción y los permisos de un rol personalizado activo, con un único "Guardar". El Nombre SHALL seguir siendo único y obligatorio tras la edición. En un rol de sistema, el nombre, la descripción, el código y el ámbito SHALL mostrarse sólo como lectura y sus permisos SHALL seguir siendo editables.

#### Scenario: Modal de edición se abre con datos pre-cargados

- **WHEN** el operador hace clic en una fila o en "Editar"
- **THEN** se abre la pantalla de edición con los datos y permisos del rol pre-cargados y con los metadatos de un rol de sistema bloqueados

#### Scenario: Edición exitosa actualiza la tabla

- **WHEN** el operador modifica los campos permitidos y guarda
- **THEN** vuelve a la grilla y la fila refleja los nuevos valores

#### Scenario: Edición exitosa actualiza la lista

- **WHEN** el operador modifica el nombre de un rol personalizado y guarda
- **THEN** la grilla refleja el nuevo nombre y el código del rol permanece estable

#### Scenario: Rol de sistema no puede renombrarse

- **WHEN** el operador abre la edición de un rol de sistema
- **THEN** no puede modificar nombre ni descripción, pero sí sus permisos

#### Scenario: Nombre vacío bloquea el envío

- **WHEN** el operador borra el campo Nombre e intenta guardar un rol personalizado
- **THEN** el sistema muestra un error de validación y no actualiza el rol

#### Scenario: Nombre duplicado con otro rol bloquea el envío

- **WHEN** el operador cambia el Nombre a uno que ya usa otro rol activo
- **THEN** el sistema muestra un error inline y no actualiza el rol

#### Scenario: El propio nombre no es considerado duplicado

- **WHEN** el operador guarda el rol sin cambiar el Nombre
- **THEN** la validación de unicidad no rechaza el nombre actual del propio rol
