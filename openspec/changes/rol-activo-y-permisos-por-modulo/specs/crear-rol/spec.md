## MODIFIED Requirements

### Requirement: Creación de nuevo rol

La pantalla `/roles` SHALL ofrecer "Nuevo rol", que abre la pantalla completa `/roles/nuevo` con Nombre, Descripción, ámbito y los permisos agrupados por módulo, todos destildados. El Nombre SHALL ser obligatorio y único. Un único "Guardar" SHALL crear el rol con los permisos marcados. El nuevo rol SHALL comenzar activo y no SHALL adquirir identidad de sistema.

#### Scenario: Formulario de alta se abre al hacer clic en "Nuevo rol"

- **WHEN** un operador con `roles.administrar` hace clic en "Nuevo rol"
- **THEN** se abre la pantalla de alta con Nombre, Descripción, ámbito y todos los módulos y permisos destildados

#### Scenario: Creación exitosa agrega el rol a la tabla

- **WHEN** el operador completa los datos válidos, marca permisos y guarda
- **THEN** vuelve a la grilla, el nuevo rol aparece con los permisos marcados y su contador

#### Scenario: Nombre vacío bloquea el envío

- **WHEN** el operador intenta guardar con el campo Nombre vacío
- **THEN** el sistema muestra un error de validación y no crea el rol

#### Scenario: Nombre duplicado bloquea el envío

- **WHEN** el operador ingresa un Nombre que ya existe en otro rol activo
- **THEN** el sistema muestra un error inline y no crea el rol

## REMOVED Requirements

### Requirement: Herencia de permisos desde rol base

**Reason**: Un rol nuevo siempre arranca con todos los permisos destildados; crear roles es poco frecuente y la copia desde otro rol no se usa.

**Migration**: El operador marca los permisos en la pantalla de alta.
