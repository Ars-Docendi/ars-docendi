## ADDED Requirements

### Requirement: Confirmación de acciones de revisión vía modal

El sistema SHALL exigir una confirmación explícita en un modal antes de ejecutar cualquier acción de revisión (aceptar, rechazar, devolver, priorizar) desde el detalle de pedido. Al disparar la acción desde el panel de revisión, el sistema MUST abrir un modal propio de esa acción que matchea el diseño (`screens.pen`: `modalAprobar`, `modalRechazar`, `modalDevolver`, `modalPriorizar`) y MUST describir el efecto de la acción y a quién se notifica mediante una caja de aviso (info para aceptar; warning para rechazar, devolver y priorizar). La mutación de dominio SHALL ejecutarse únicamente al confirmar dentro del modal; ninguna acción de revisión MUST ejecutarse con un solo click directo sin pasar por la confirmación.

#### Scenario: Aceptar abre el modal de confirmación

- **GIVEN** un revisor en la etapa actual viendo el detalle de un pedido `en_revision_*`
- **WHEN** dispara la acción "Aceptar" desde el panel de revisión
- **THEN** el sistema abre el modal "Aceptar pedido" con el subtítulo de la etapa y la caja de aviso del efecto
- **AND** no cambia el estado del pedido hasta que el revisor confirme

#### Scenario: Confirmar en el modal ejecuta la transición

- **GIVEN** el modal de "Aceptar pedido" abierto
- **WHEN** el revisor confirma con "Aprobar y enviar"
- **THEN** el sistema ejecuta la aceptación, avanza la cadena y registra el evento en el historial
- **AND** cierra el modal

#### Scenario: Cancelar cierra el modal sin efecto

- **GIVEN** el modal de confirmación de cualquier acción abierto
- **WHEN** el revisor elige "Cancelar" (o cierra el modal)
- **THEN** el sistema no ejecuta ninguna mutación y el pedido conserva su estado

### Requirement: Edición del comentario dentro del modal de confirmación

El sistema SHALL permitir editar el comentario/justificativo dentro del modal de confirmación. El modal MUST pre-cargar el textarea con el texto ya ingresado en el panel inline de revisión, y el valor confirmado en el modal SHALL ser el que se envía con la acción.

#### Scenario: El comentario del panel inline se traslada al modal

- **GIVEN** un revisor que tipeó un comentario en el textarea del panel inline
- **WHEN** dispara una acción y se abre el modal
- **THEN** el textarea del modal aparece pre-cargado con ese comentario
- **AND** el revisor puede modificarlo antes de confirmar

#### Scenario: Se envía el comentario editado en el modal

- **GIVEN** el modal abierto con un comentario pre-cargado
- **WHEN** el revisor edita el comentario y confirma
- **THEN** la acción se ejecuta con el texto final del modal (no con el original del panel)

### Requirement: Validación del justificativo obligatorio dentro del modal

El sistema MUST validar la obligatoriedad del justificativo dentro del modal de confirmación para las acciones que lo requieren —rechazar y devolver [BR-designaciones-005], y priorizar [BR-designaciones-017]—. Mientras el justificativo esté vacío, el botón de confirmar MUST estar bloqueado y el modal MUST indicar que el justificativo es obligatorio; la acción NO MUST ejecutarse. Para aceptar, el comentario SHALL ser opcional.

#### Scenario: Rechazar sin justificativo queda bloqueado en el modal [BR-designaciones-005]

- **GIVEN** el modal "Rechazar pedido" abierto con el justificativo vacío
- **WHEN** el revisor intenta confirmar
- **THEN** el sistema no ejecuta el rechazo e indica que el justificativo es obligatorio

#### Scenario: Devolver sin justificativo queda bloqueado en el modal [BR-designaciones-005]

- **GIVEN** el modal "Devolver pedido" abierto con el justificativo vacío
- **WHEN** el revisor intenta confirmar
- **THEN** el sistema no ejecuta la devolución e indica que el justificativo es obligatorio

#### Scenario: Aceptar permite confirmar sin comentario

- **GIVEN** el modal "Aceptar pedido" abierto con el comentario vacío
- **WHEN** el revisor confirma con "Aprobar y enviar"
- **THEN** el sistema ejecuta la aceptación (el comentario es opcional)
