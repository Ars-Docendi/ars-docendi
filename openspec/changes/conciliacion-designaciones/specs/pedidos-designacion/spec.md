# Spec Delta

## MODIFIED Requirements

### Requirement: Edición y eliminación

Un borrador SHALL ser editable y eliminable sólo por un Jefe de Cátedra dentro de su ámbito. Un pedido devuelto SHALL ser editable por su propietario actual —Jefe, Coordinador o Secretaría— dentro de su ámbito, pero MUST NOT ser eliminable [BR-designaciones-008, BR-designaciones-009]. Un pedido `en_lote` o `no_aprobado` SHALL ser editable, en cualquier campo y conservando el snapshot inicial, por quien tenga `designaciones.editar` dentro de su ámbito, y MUST NOT ser eliminable. Un pedido `aprobado`, `rechazado` o `cancelado` MUST NOT ser editable.

#### Scenario: Corregir una devolución

- **GIVEN** un pedido devuelto a Coordinación o Secretaría
- **WHEN** el propietario edita datos válidos y lo reenvía
- **THEN** la API acepta ambas operaciones y conserva el snapshot inicial

#### Scenario: Borrar un borrador

- **WHEN** su Jefe de Cátedra elimina un borrador dentro de su ámbito
- **THEN** el pedido deja de existir

#### Scenario: Corregir un pedido en lote con permiso

- **GIVEN** un pedido `en_lote` y un usuario con `designaciones.editar` en su ámbito
- **WHEN** edita cargo, dedicación u horas con datos válidos
- **THEN** la API acepta la edición, el pedido sigue `en_lote`, conserva el snapshot inicial y el historial registra la edición

#### Scenario: Editar un pedido en lote sin permiso

- **GIVEN** un Jefe de Cátedra sin `designaciones.editar` y un pedido suyo `en_lote`
- **WHEN** intenta editarlo por API
- **THEN** la API MUST denegarlo sin modificar el pedido
