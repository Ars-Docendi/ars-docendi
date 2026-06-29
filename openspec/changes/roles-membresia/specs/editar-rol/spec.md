## ADDED Requirements

### Requirement: Edición de rol existente

La pantalla `/roles` SHALL permitir editar el Nombre y la Descripción de cualquier rol existente. El Nombre SHALL seguir siendo único y obligatorio tras la edición.

#### Scenario: Modal de edición se abre con datos pre-cargados

- **WHEN** el operador hace clic en "Editar" en una fila de la tabla
- **THEN** se abre un modal con los campos Nombre y Descripción pre-poblados con los valores actuales del rol

#### Scenario: Edición exitosa actualiza la tabla

- **WHEN** el operador modifica los campos y confirma
- **THEN** el modal se cierra y la tabla refleja los nuevos valores

#### Scenario: Nombre vacío bloquea el envío

- **WHEN** el operador borra el campo Nombre e intenta guardar
- **THEN** el sistema muestra un error de validación y no actualiza el rol

#### Scenario: Nombre duplicado con otro rol bloquea el envío

- **WHEN** el operador cambia el Nombre a uno que ya usa otro rol
- **THEN** el sistema muestra un error inline y no actualiza el rol

#### Scenario: El propio nombre no es considerado duplicado

- **WHEN** el operador guarda el rol sin cambiar el Nombre
- **THEN** la validación de unicidad no rechaza el nombre actual del propio rol
