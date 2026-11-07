## MODIFIED Requirements

### Requirement: Transición a Resuelta requiere el campo Solución

Al cambiar el estado de una tarea a Resuelta, el sistema SHALL exigir que el campo Solución no esté vacío, con el detalle de cómo se resolvió la tarea. El campo Solución MUST quedar visible en la pantalla de Detalle una vez completado.

Marcar una tarea como Resuelta SHALL llevar su porcentaje de avance a 100, y el cambio MUST quedar en el historial si el valor era otro.

#### Scenario: Resolver una tarea completa su avance

- **GIVEN** una tarea En curso con 60% de avance
- **WHEN** el Responsable la marca como Resuelta con su Solución
- **THEN** el avance pasa a 100% y queda registrado en el historial

#### Scenario: Resuelta sin Solución bloqueada

- **WHEN** el Responsable intenta marcar la tarea como Resuelta sin completar el campo Solución
- **THEN** el sistema muestra un error y la tarea conserva su estado anterior

#### Scenario: Resuelta con Solución completa la transición

- **WHEN** el Responsable completa el campo Solución con "Se corrigió el aula asignada y se notificó al docente" y marca la tarea como Resuelta
- **THEN** el estado pasa a Resuelta y el campo Solución queda visible en el Detalle de la tarea
