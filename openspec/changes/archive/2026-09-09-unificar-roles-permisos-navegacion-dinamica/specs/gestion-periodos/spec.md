## MODIFIED Requirements

### Requirement: Gestión persistente de períodos

El listado, alta, edición, activación, desactivación y eliminación de períodos MUST operar mediante la API de Designaciones y requerir el permiso efectivo `periodos.administrar`. La unicidad del período activo y las restricciones por pedidos asociados MUST validarse en backend.

#### Scenario: Gestión autorizada por permiso

- **GIVEN** un actor con `periodos.administrar`
- **WHEN** lista o modifica períodos
- **THEN** la operación se ejecuta mediante la API y la pantalla refleja el estado persistido

#### Scenario: Guardado exitoso

- **GIVEN** datos válidos y un actor con `periodos.administrar`
- **WHEN** la API confirma la creación o edición
- **THEN** una consulta posterior devuelve el período con los valores persistidos

#### Scenario: Actor sin permiso

- **GIVEN** un actor autenticado sin `periodos.administrar`
- **WHEN** intenta abrir o mutar la gestión de períodos
- **THEN** el frontend oculta el enlace y el backend deniega la operación

#### Scenario: Segundo período activo

- **GIVEN** un período activo distinto al que se guarda
- **WHEN** se intenta activar otro período
- **THEN** la API MUST rechazar la operación sin desactivar el existente

#### Scenario: Eliminación restringida

- **GIVEN** un período referenciado por pedidos
- **WHEN** se intenta eliminarlo
- **THEN** la API MUST rechazar la operación con un conflicto identificable y conservar el período
