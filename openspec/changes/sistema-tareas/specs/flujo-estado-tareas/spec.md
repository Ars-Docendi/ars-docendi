## ADDED Requirements

### Requirement: Estados de una tarea

Una tarea SHALL tener exactamente uno de los siguientes estados en todo momento: Pendiente, En curso, Pausa, Resuelta o Cancelada. Toda tarea nueva MUST crearse en estado Pendiente.

#### Scenario: Tarea nueva nace en Pendiente

- **WHEN** una autoridad crea una tarea
- **THEN** el estado inicial de la tarea es Pendiente

### Requirement: El Responsable cambia libremente entre Pendiente, En curso, Pausa y Resuelta

El usuario Responsable de una tarea SHALL poder cambiar su estado a cualquiera de Pendiente, En curso, Pausa o Resuelta, sin restricción de orden entre ellos. El Responsable MUST NOT poder cambiar el estado a Cancelada.

#### Scenario: El Responsable marca la tarea como Resuelta

- **GIVEN** una tarea En curso asignada como Responsable al usuario actual, con el campo Solución completo
- **WHEN** el Responsable la marca como Resuelta
- **THEN** el estado de la tarea pasa a Resuelta y queda registrado en el historial

#### Scenario: El Responsable intenta cancelar

- **GIVEN** una tarea con el usuario actual como Responsable
- **WHEN** el Responsable intenta cambiar el estado a Cancelada
- **THEN** el sistema rechaza la acción y la tarea conserva su estado anterior

### Requirement: Transición a Pausa requiere comentario del motivo

Al cambiar el estado de una tarea a Pausa, el sistema SHALL exigir un comentario no vacío describiendo la consulta, que MUST agregarse al hilo de comentarios internos de la tarea.

#### Scenario: Pausa sin comentario bloqueada

- **WHEN** el Responsable intenta marcar la tarea como Pausa sin ingresar un comentario
- **THEN** el sistema muestra un error y la tarea conserva su estado anterior

#### Scenario: Pausa con comentario registra la consulta

- **WHEN** el Responsable marca la tarea como Pausa con el comentario "Necesito confirmar el alcance con el docente"
- **THEN** el estado pasa a Pausa y ese comentario aparece como el más reciente del hilo

### Requirement: Transición a Resuelta requiere el campo Solución

Al cambiar el estado de una tarea a Resuelta, el sistema SHALL exigir que el campo Solución no esté vacío, con el detalle de cómo se resolvió la tarea. El campo Solución MUST quedar visible en la pantalla de Detalle una vez completado.

#### Scenario: Resuelta sin Solución bloqueada

- **WHEN** el Responsable intenta marcar la tarea como Resuelta sin completar el campo Solución
- **THEN** el sistema muestra un error y la tarea conserva su estado anterior

#### Scenario: Resuelta con Solución completa la transición

- **WHEN** el Responsable completa el campo Solución con "Se corrigió el aula asignada y se notificó al docente" y marca la tarea como Resuelta
- **THEN** el estado pasa a Resuelta y el campo Solución queda visible en el Detalle de la tarea

### Requirement: Cancelar una tarea es exclusivo de la autoridad creadora

Solo la autoridad que creó la tarea SHALL poder cambiar su estado a Cancelada, desde cualquier estado no terminal (Pendiente, En curso o Pausa).

#### Scenario: La autoridad creadora cancela la tarea

- **GIVEN** una tarea En curso creada por la autoridad actual
- **WHEN** la autoridad la cancela
- **THEN** el estado pasa a Cancelada y queda registrado en el historial

#### Scenario: Otra autoridad no puede cancelar una tarea ajena

- **GIVEN** una tarea creada por una autoridad distinta a la que está autenticada
- **WHEN** la autoridad actual intenta cancelarla
- **THEN** el sistema rechaza la acción

### Requirement: Edición de campos exclusiva de la autoridad creadora

Solo la autoridad que creó la tarea SHALL poder editar Título, Descripción, Fecha de Inicio, Fecha de Fin, Prioridad y Responsable. El usuario Responsable MUST NOT poder editar estos campos; puede modificar únicamente el estado, el porcentaje de avance y el campo Solución.

#### Scenario: La autoridad creadora edita la prioridad

- **GIVEN** una tarea creada por la autoridad actual
- **WHEN** la autoridad cambia su Prioridad de Media a Alta
- **THEN** el cambio se guarda y queda registrado en el historial

#### Scenario: El Responsable no puede editar el título

- **GIVEN** una tarea con el usuario actual como Responsable, creada por otra persona
- **WHEN** el Responsable intenta editar el Título
- **THEN** el sistema no permite la edición; solo las acciones de cambiar el estado, actualizar el avance y completar la Solución están disponibles
