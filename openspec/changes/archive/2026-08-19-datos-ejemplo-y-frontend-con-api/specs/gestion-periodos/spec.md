## ADDED Requirements

### Requirement: Gestión persistente de períodos

El listado, alta, edición, activación, desactivación y eliminación de períodos MUST operar mediante la API de Designaciones. La unicidad del período activo y las restricciones por pedidos asociados MUST validarse en backend.

#### Scenario: Guardado exitoso

- **GIVEN** datos válidos y un actor autorizado
- **WHEN** la API confirma la creación o edición
- **THEN** una consulta posterior devuelve el período con los valores persistidos

#### Scenario: Segundo período activo

- **GIVEN** un período activo distinto al que se guarda
- **WHEN** se intenta activar otro período
- **THEN** la API MUST rechazar la operación sin desactivar el existente

#### Scenario: Eliminación restringida

- **GIVEN** un período referenciado por pedidos
- **WHEN** se intenta eliminarlo
- **THEN** la API MUST rechazar la operación con un conflicto identificable y conservar el período

## REMOVED Requirements

### Requirement: Mock data para validación visual

**Reason**: Los estados visuales se validarán con registros sintéticos persistidos y respuestas reales de la API; el runtime no conservará un modo mock con fixtures TypeScript.

**Migration**: Trasladar la variedad de estados y ventanas al seed no productivo. Mantener fixtures específicas únicamente dentro de tests automatizados.
