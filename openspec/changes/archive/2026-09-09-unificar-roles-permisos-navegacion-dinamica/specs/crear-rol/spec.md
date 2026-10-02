## MODIFIED Requirements

### Requirement: Creación de nuevo rol

La pantalla unificada `/roles` SHALL permitir crear nuevos roles personalizados con Nombre, Descripción y ámbito. El Nombre SHALL ser obligatorio y único. El nuevo rol SHALL comenzar activo y no SHALL adquirir identidad de sistema.

#### Scenario: Formulario de alta se abre al hacer clic en "Nuevo rol"

- **WHEN** un operador con `roles.administrar` hace clic en "Nuevo rol"
- **THEN** se abre un modal o formulario con campos Nombre, Descripción, ámbito y la opción de usar un rol base

#### Scenario: Creación exitosa agrega el rol a la tabla

- **WHEN** el operador completa los datos válidos y confirma
- **THEN** el modal se cierra, el nuevo rol aparece en la lista y sus permisos quedan disponibles para revisar en el panel derecho

#### Scenario: Nombre vacío bloquea el envío

- **WHEN** el operador intenta guardar con el campo Nombre vacío
- **THEN** el sistema muestra un error de validación y no crea el rol

#### Scenario: Nombre duplicado bloquea el envío

- **WHEN** el operador ingresa un Nombre que ya existe en otro rol activo
- **THEN** el sistema muestra un error inline y no crea el rol

### Requirement: Herencia de permisos desde rol base

Al crear un rol, el operador SHALL poder optar por usar un rol activo existente como base. Si se selecciona un rol base, el nuevo rol SHALL heredar (copiar) los permisos del rol base en el momento de la creación, sin mantener una relación posterior.

#### Scenario: Checkbox habilita el selector de rol base

- **WHEN** el operador activa el checkbox "Usar un rol existente como base"
- **THEN** aparece un selector con la lista de roles activos existentes

#### Scenario: Selector deshabilitado cuando checkbox no está activo

- **WHEN** el checkbox "Usar un rol existente como base" no está activo
- **THEN** el selector de rol base no es visible ni interactuable

#### Scenario: Nuevo rol hereda permisos del rol base

- **WHEN** el operador selecciona un rol base y confirma la creación
- **THEN** el nuevo rol se crea con los mismos permisos que tenía el rol base en ese momento

#### Scenario: Cambios posteriores en el rol base no afectan al derivado

- **WHEN** se modifican los permisos del rol base después de crear el rol derivado
- **THEN** los permisos del rol derivado permanecen sin cambios
