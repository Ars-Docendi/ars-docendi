## ADDED Requirements

### Requirement: Implementación gateada por un change OpenSpec aprobado

Antes de implementar una feature, SHALL existir un change OpenSpec con sus `tasks` completas (todos los artefactos de `applyRequires` en estado `done` según `openspec status`). La implementación MUST detenerse si ese change no existe o no está listo.

#### Scenario: Implementación sin change listo

- **WHEN** un developer intenta implementar una feature sin un change OpenSpec con tasks listas
- **THEN** el gate detiene el flujo e indica crear/aprobar el change con `/opsx:propose`

#### Scenario: Implementación con change listo

- **WHEN** existe `openspec/changes/<id>/` con `applyRequires` satisfecho (`tasks` en estado `done`)
- **THEN** la implementación procede vía `/add-feature`, que aplica los gates del proyecto y delega la ejecución de tasks a `/opsx:apply`

### Requirement: Retiro del gate basado en docs/

El gate de implementación SHALL dejar de exigir `docs/product/specs/<slug>.md` y `docs/plans/active/<slug>.md`.

#### Scenario: Evaluar el gate sin archivos en docs/

- **WHEN** se evalúa el gate de implementación de una feature
- **THEN** no se requiere ningún archivo bajo `docs/plans/` ni `docs/product/specs/`, solo el change OpenSpec aprobado
