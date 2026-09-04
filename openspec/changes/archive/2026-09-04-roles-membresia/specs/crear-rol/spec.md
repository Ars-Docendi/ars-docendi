## ADDED Requirements

### Requirement: Creación de nuevo rol

La pantalla `/roles` SHALL permitir crear nuevos roles con Nombre y Descripción. El Nombre SHALL ser obligatorio y único. La Descripción SHALL ser obligatoria.

#### Scenario: Formulario de alta se abre al hacer clic en "Nuevo rol"

- **WHEN** el operador hace clic en el botón "Nuevo rol"
- **THEN** se abre un modal con campos Nombre, Descripción y la opción de usar un rol base

#### Scenario: Creación exitosa agrega el rol a la tabla

- **WHEN** el operador completa Nombre y Descripción y confirma
- **THEN** el modal se cierra y el nuevo rol aparece en la tabla

#### Scenario: Nombre vacío bloquea el envío

- **WHEN** el operador intenta guardar con el campo Nombre vacío
- **THEN** el sistema muestra un error de validación y no crea el rol

#### Scenario: Nombre duplicado bloquea el envío

- **WHEN** el operador ingresa un Nombre que ya existe en otro rol
- **THEN** el sistema muestra un error inline y no crea el rol

### Requirement: Herencia de permisos desde rol base

Al crear un rol, el operador SHALL poder optar por usar un rol existente como base. Si se selecciona un rol base, el nuevo rol SHALL heredar (copiar) los permisos del rol base en el momento de la creación.

#### Scenario: Checkbox habilita el selector de rol base

- **WHEN** el operador activa el checkbox "Usar un rol existente como base"
- **THEN** aparece un selector con la lista de roles existentes

#### Scenario: Selector deshabilitado cuando checkbox no está activo

- **WHEN** el checkbox "Usar un rol existente como base" no está activo
- **THEN** el selector de rol base no es visible ni interactuable

#### Scenario: Nuevo rol hereda permisos del rol base

- **WHEN** el operador selecciona un rol base y confirma la creación
- **THEN** el nuevo rol se crea con los mismos permisos que tenía el rol base en ese momento

#### Scenario: Cambios posteriores en el rol base no afectan al derivado

- **WHEN** se modifican los permisos del rol base después de haber creado el rol derivado
- **THEN** los permisos del rol derivado permanecen sin cambios
