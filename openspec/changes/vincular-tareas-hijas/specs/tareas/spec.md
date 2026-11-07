## ADDED Requirements

### Requirement: Vincular y desvincular tareas hijas existentes

Desde el Detalle de una tarea, su autoridad creadora SHALL poder vincular como hija una tarea existente y desvincular una hija, validado en backend con `tareas.gestionar`. La tarea vinculada MUST NOT tener otro padre, estar Cancelada, ser la propia tarea ni uno de sus ancestros. La tarea vinculada y sus descendientes SHALL tomar el Proyecto del padre, previa confirmación en pantalla si cambia, y ese Proyecto MUST admitir tareas. Desvincular SHALL conservar el Proyecto de la tarea. Ambas acciones SHALL quedar en el historial de las dos tareas y recalcular el avance del padre y sus ancestros.

#### Scenario: Vincular una tarea existente

- **GIVEN** una tarea padre y otra tarea sin padre, ambas con la misma autoridad creadora
- **WHEN** la autoridad vincula la segunda como hija
- **THEN** pasa a figurar entre las hijas y el avance del padre se recalcula

#### Scenario: La tarea vinculada toma el Proyecto del padre

- **GIVEN** una tarea padre en un Proyecto y una tarea sin Proyecto que tiene su propia hija
- **WHEN** la autoridad confirma la vinculación
- **THEN** la tarea y su hija quedan en el Proyecto del padre

#### Scenario: No se admite un ciclo

- **GIVEN** una tarea hija de otra
- **WHEN** la autoridad intenta vincular a su ancestro como hija suya
- **THEN** el sistema rechaza la acción con un error de regla de negocio

#### Scenario: Una tarea con padre no se mueve directamente

- **GIVEN** una tarea que ya es hija de otra
- **WHEN** la autoridad intenta vincularla a una tarea distinta
- **THEN** el sistema rechaza la acción y pide desvincularla primero

#### Scenario: Una tercera persona no puede vincular

- **GIVEN** una tarea creada por otra autoridad
- **WHEN** un usuario con `tareas.gestionar` intenta vincularle una hija
- **THEN** la API responde 403

#### Scenario: Desvincular una hija

- **GIVEN** una tarea padre con dos hijas
- **WHEN** la autoridad desvincula una
- **THEN** la tarea conserva su Proyecto, deja de tener padre y el avance del padre se recalcula con la hija restante
