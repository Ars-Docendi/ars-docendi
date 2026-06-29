# aprobacion-pedidos-designacion

## Purpose

Flujo de aprobación de los pedidos de designación docente del lado de los roles revisores (Coordinador, Secretaría, Decanato y Administración). Cubre el tablero de revisión filtrado por ámbito, la cadena de aprobación que avanza etapa por etapa, las acciones de aceptar / rechazar / devolver / marcar prioritario con sus reglas de dominio (justificativo obligatorio, terminalidad, guard de etapa, retroceso de un nivel), el detalle role-aware con cadena de aprobación e historial, el recorrido completo de la cadena mediante cambio de rol, y el routing/gating de las superficies de revisión.

## Requirements

### Requirement: Tablero de revisión filtrado por ámbito

El sistema SHALL ofrecer a los roles revisores (Coordinador, Secretaría, Decanato, Administración) un tablero de revisión (`/designaciones/revision`) tipo Kanban sin drag, con las columnas **Pendiente (mi etapa)**, **Aprobado**, **Rechazado** y **Devuelto**. El tablero MUST listar únicamente los pedidos del ámbito del actor [BR-designaciones-009]: el Coordinador ve solo los de su carrera; Secretaría, Decanato y Administración ven todo el departamento. La pantalla MUST representar explícitamente los estados Loading, Empty, Error y Success.

#### Scenario: El Coordinador ve solo los pedidos de su carrera [BR-designaciones-009]

- **GIVEN** un Coordinador autenticado cuyo ámbito es su carrera
- **WHEN** abre `/designaciones/revision`
- **THEN** la columna "Pendiente (mi etapa)" lista los pedidos en `en_revision_coordinador` de su carrera
- **AND** no muestra pedidos de otras carreras

#### Scenario: Cada pedido cae en la columna correcta por estado

- **GIVEN** pedidos en estados `en_revision_*`, `en_lote`, `rechazado` y `devuelto` dentro del ámbito del revisor
- **WHEN** abre el tablero
- **THEN** los pedidos de su etapa aparecen en "Pendiente (mi etapa)", los `en_lote` en "Aprobado", los `rechazado` en "Rechazado" y los `devuelto` en "Devuelto"

#### Scenario: Tablero sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos en su ámbito
- **WHEN** abre el tablero
- **THEN** ve el estado vacío sin filas, sin romper la navegación

### Requirement: Aceptación que avanza la cadena de aprobación

El sistema SHALL permitir al revisor de la etapa actual aceptar un pedido, avanzándolo a la etapa siguiente de la cadena: `en_revision_coordinador` → `en_revision_secretaria` → `en_revision_decanato` → `en_lote`. La transición MUST registrar un evento "aceptar" en el historial. El estado `en_lote` es terminal para el prototipo.

#### Scenario: El Coordinador acepta y avanza a Secretaría

- **GIVEN** un pedido en `en_revision_coordinador` de la carrera del Coordinador
- **WHEN** el Coordinador lo acepta
- **THEN** el pedido pasa a `en_revision_secretaria` y se registra el evento en el historial

#### Scenario: La Secretaría acepta y avanza a Decanato

- **GIVEN** un pedido en `en_revision_secretaria`
- **WHEN** la Secretaría lo acepta
- **THEN** el pedido pasa a `en_revision_decanato`

#### Scenario: El Decanato acepta y el pedido va a lote

- **GIVEN** un pedido en `en_revision_decanato`
- **WHEN** el Decanato lo acepta
- **THEN** el pedido pasa a `en_lote` (terminal-prototipo) y no admite nuevas acciones

### Requirement: Administración revisa pero no aprueba

El sistema MUST denegar la acción de aceptar cuando el actor es Administración, en cualquier etapa [BR-designaciones-015]. Administración SHALL poder rechazar y devolver dentro del departamento, pero nunca avanzar la cadena.

#### Scenario: Administración no puede aceptar [BR-designaciones-015]

- **GIVEN** un pedido en cualquier etapa `en_revision_*`
- **WHEN** Administración intenta aceptarlo
- **THEN** el sistema MUST denegar la acción con un error de dominio y no cambiar el estado

### Requirement: Rechazo con justificativo, terminal

El sistema SHALL permitir al revisor de la etapa (o a Administración) rechazar un pedido en estado `en_revision_*`, llevándolo al estado terminal `rechazado`. El justificativo (comentario) MUST ser obligatorio [BR-designaciones-005]; el rechazo MUST ser terminal [BR-designaciones-011].

#### Scenario: Rechazo sin justificativo es denegado [BR-designaciones-005]

- **GIVEN** un pedido en `en_revision_coordinador`
- **WHEN** el revisor intenta rechazarlo sin justificativo
- **THEN** el sistema MUST denegar la acción e indicar que el justificativo es obligatorio

#### Scenario: El rechazo es terminal [BR-designaciones-011]

- **GIVEN** un pedido rechazado
- **WHEN** se intenta cualquier acción posterior (aceptar, devolver, reenviar)
- **THEN** el sistema MUST denegarla (idempotencia terminal)

### Requirement: Devolución que retrocede un nivel y permite reenvío

El sistema SHALL permitir al revisor de la etapa (o a Administración) devolver un pedido, llevándolo a `devuelto` con `propietarioActual` y `etapaRetorno` correspondientes a un retroceso de un nivel [BR-designaciones-014]: desde `en_revision_coordinador` vuelve al Jefe de Cátedra; desde `en_revision_secretaria` al Coordinador; desde `en_revision_decanato` a la Secretaría. El comentario MUST ser obligatorio [BR-designaciones-005]. El propietario del pedido devuelto SHALL poder reenviarlo, retomando la etapa desde la que se devolvió (`etapaRetorno`).

#### Scenario: Devolución sin comentario es denegada [BR-designaciones-005]

- **GIVEN** un pedido en `en_revision_coordinador`
- **WHEN** el Coordinador intenta devolverlo sin comentario
- **THEN** el sistema MUST denegar la acción e indicar que el comentario es obligatorio

#### Scenario: La devolución retrocede un nivel [BR-designaciones-014]

- **GIVEN** un pedido en `en_revision_secretaria`
- **WHEN** la Secretaría lo devuelve con comentario
- **THEN** el pedido pasa a `devuelto`, con `propietarioActual = Coordinador` y `etapaRetorno = en_revision_secretaria`

#### Scenario: El reenvío retoma la etapa del revisor que devolvió [BR-designaciones-014]

- **GIVEN** un pedido `devuelto` con `etapaRetorno = en_revision_coordinador` cuyo propietario es el Jefe de Cátedra
- **WHEN** el Jefe de Cátedra lo reenvía
- **THEN** el pedido vuelve a `en_revision_coordinador`

### Requirement: Marcar prioritario con justificativo sin cambiar estado

El sistema SHALL permitir a cualquier actor dentro de su ámbito marcar un pedido no terminal como prioritario, fijando `prioritario = true` sin cambiar el estado del pedido [BR-designaciones-017]. El justificativo (motivo) MUST ser obligatorio.

#### Scenario: Prioritario exige justificativo [BR-designaciones-017]

- **GIVEN** un pedido no terminal
- **WHEN** un actor intenta marcarlo prioritario sin justificativo
- **THEN** el sistema MUST denegar la acción e indicar que el justificativo es obligatorio

#### Scenario: La prioridad no cambia el estado [BR-designaciones-017]

- **GIVEN** un pedido en `en_revision_coordinador`
- **WHEN** un actor lo marca prioritario con justificativo
- **THEN** el pedido queda con `prioritario = true` y conserva el estado `en_revision_coordinador`

### Requirement: Guard de etapa — solo el revisor de la etapa actual actúa

El sistema MUST permitir aceptar, rechazar o devolver únicamente al revisor cuyo rol corresponde a la etapa actual del pedido [BR-designaciones-013] (o a Administración como revisor depto-wide, salvo aceptar). Un rol que no es el revisor de la etapa actual MUST ser denegado.

#### Scenario: Un rol fuera de su etapa es denegado [BR-designaciones-013]

- **GIVEN** un pedido en `en_revision_secretaria`
- **WHEN** el Coordinador (etapa anterior) intenta aceptarlo, rechazarlo o devolverlo
- **THEN** el sistema MUST denegar la acción con un error de dominio

### Requirement: Detalle del pedido role-aware con cadena de aprobación e historial

El sistema SHALL ofrecer una pantalla de detalle (`/designaciones/pedidos/:id`) visible para cualquier rol con visibilidad por ámbito sobre el pedido, mostrando los datos completos del docente y del pedido (`DataList`), la cadena de aprobación (`ApprovalTimeline`) y el historial de eventos (`AuditLog`). Para el revisor de la etapa actual dentro de su ámbito, el detalle MUST ofrecer las acciones Aceptar, Rechazar, Devolver y Marcar prioritario mediante un modal que aplica la regla de comentario obligatorio [BR-designaciones-005]. Para el resto de los roles, el detalle MUST ser de solo lectura.

#### Scenario: El revisor de la etapa ve las acciones

- **GIVEN** un Coordinador viendo el detalle de un pedido de su carrera en `en_revision_coordinador`
- **WHEN** abre `/designaciones/pedidos/:id`
- **THEN** ve los datos, la cadena de aprobación y el historial, y las acciones Aceptar / Rechazar / Devolver / Marcar prioritario

#### Scenario: El Jefe de Cátedra ve el detalle en solo lectura

- **GIVEN** un Jefe de Cátedra viendo un pedido propio ya enviado a revisión
- **WHEN** abre el detalle
- **THEN** ve los datos, la cadena de aprobación y el historial, sin acciones de revisión

#### Scenario: El historial se muestra con el verbo y la etapa de cada evento

- **GIVEN** un pedido con eventos de creación, envío y aceptación en su historial
- **WHEN** se muestra el detalle
- **THEN** el `AuditLog` lista cada evento con su actor, su acción y su fecha, y el `ApprovalTimeline` refleja la etapa alcanzada

### Requirement: Recorrido de la cadena con cambio de rol

El sistema SHALL permitir, mediante un usuario "Demo (todos los roles)", cambiar el rol activo sin re-loguear, de modo que el actor que consume la capa de datos cambie de forma coherente y el mismo pedido sea visible y accionable en la etapa que corresponda a cada rol.

#### Scenario: Happy-path de la cadena completa

- **GIVEN** un pedido enviado por el Jefe de Cátedra (estado `en_revision_coordinador`)
- **WHEN** el usuario cambia a Coordinador y acepta, luego a Secretaría y acepta, luego a Decanato y acepta
- **THEN** el pedido recorre `en_revision_secretaria` → `en_revision_decanato` → `en_lote`, conservando su historial entre cambios de rol

#### Scenario: Camino de devolución y reenvío

- **GIVEN** un pedido en `en_revision_coordinador`
- **WHEN** el Coordinador lo devuelve con comentario y luego el Jefe de Cátedra lo reenvía
- **THEN** el pedido pasa a `devuelto` (propietario Jefe de Cátedra) y vuelve a `en_revision_coordinador` al reenviar

### Requirement: Routing y gating de las superficies de revisión

El sistema SHALL exponer la ruta `revision` protegida por `RequireRole` para Coordinador, Secretaría, Decanato y Administración, y la ruta `pedidos/:id` accesible a cualquier rol con la visibilidad acotada por ámbito y las acciones gated por etapa. La navegación SHALL ofrecer el ítem "Revisión" únicamente a los roles revisores, sin links muertos (invariante #7).

#### Scenario: Un rol no revisor no accede al tablero

- **GIVEN** un Docente autenticado
- **WHEN** intenta navegar a `/designaciones/revision`
- **THEN** el sistema lo redirige fuera de la ruta (gate por rol) y no muestra el ítem "Revisión" en la navegación

#### Scenario: El ítem "Revisión" aparece solo para revisores

- **GIVEN** un Coordinador autenticado
- **WHEN** se renderiza la navegación lateral
- **THEN** incluye el ítem "Revisión" apuntando a `/designaciones/revision`

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

### Requirement: Presentación destacada del pedido rechazado

En el tablero de revisión, un pedido en estado `rechazado` MUST presentarse con un **distintivo de estado "Rechazado"** (en lugar del distintivo de novedad que usan los demás estados) y su **motivo de rechazo** MUST mostrarse **destacado** (citado y diferenciado del resto del detalle), de modo que el revisor identifique de un vistazo que fue rechazado y por qué.

#### Scenario: La card rechazada muestra el distintivo "Rechazado"

- **GIVEN** un pedido en estado `rechazado`
- **WHEN** se renderiza su card en el tablero
- **THEN** la card muestra un distintivo de estado "Rechazado" en lugar del distintivo de novedad

#### Scenario: La card rechazada destaca el motivo

- **GIVEN** un pedido `rechazado` con un motivo de rechazo registrado
- **WHEN** se renderiza su card
- **THEN** el motivo se muestra destacado (citado), diferenciado visualmente del detalle de los demás estados
