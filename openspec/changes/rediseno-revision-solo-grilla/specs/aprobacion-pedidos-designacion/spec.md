## MODIFIED Requirements

### Requirement: Tablero de revisión filtrado por ámbito

El sistema SHALL ofrecer a los roles revisores (Coordinador, Secretaría, Decanato, Administración) una
superficie de revisión (`/designaciones/revision`) en forma de **tabla** organizada en cuatro grupos
por **estado de avance** — En revisión (toda la cadena), Aceptados, Devueltos, Rechazados — iguales
para todo actor (no hay una columna "propia" por rol). La superficie MUST listar únicamente los
pedidos del ámbito del actor [BR-designaciones-009]: el Coordinador ve solo los de su carrera;
Secretaría, Decanato y Administración ven todo el departamento. Dentro del grupo "En revisión", los
pedidos en el turno del actor (`esTuTurno`) SHALL ordenarse primero. La pantalla MUST representar
explícitamente los estados Loading, Empty, Error y Success.

#### Scenario: El Coordinador ve solo los pedidos de su carrera [BR-designaciones-009]

- **GIVEN** un Coordinador autenticado cuyo ámbito es su carrera
- **WHEN** abre `/designaciones/revision`
- **THEN** el grupo "En revisión" lista los pedidos `en_revision_*` de su carrera
- **AND** no muestra pedidos de otras carreras

#### Scenario: Cada pedido cae en el grupo correcto por estado de avance

- **GIVEN** pedidos en estados `en_revision_*`, `en_lote`, `rechazado` y `devuelto` dentro del ámbito
  del revisor
- **WHEN** abre la superficie de revisión
- **THEN** los pedidos en `en_revision_*` aparecen en "En revisión" (los del turno del actor primero),
  los `en_lote` en "Aceptados", los `devuelto` en "Devueltos" y los `rechazado` en "Rechazados"

#### Scenario: Superficie sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos en su ámbito
- **WHEN** abre la superficie de revisión
- **THEN** ve el estado vacío sin filas, sin romper la navegación

### Requirement: Marcar prioritario con justificativo sin cambiar estado

El sistema SHALL permitir a cualquier actor dentro de su ámbito marcar un pedido no terminal como
prioritario, fijando `prioritario = true` sin cambiar el estado del pedido [BR-designaciones-017]. El
justificativo (motivo) MUST ser obligatorio para marcar. El sistema SHALL permitir también **quitar**
la marca de un pedido ya prioritario (`prioritario = false`), sin cambiar el estado y sin exigir
justificativo — bajar la urgencia no requiere la misma justificación que subirla. Ambas acciones
respetan el mismo guard de ámbito/turno que aceptar, rechazar y devolver.

#### Scenario: Prioritario exige justificativo al marcar [BR-designaciones-017]

- **GIVEN** un pedido no terminal, no prioritario
- **WHEN** un actor intenta marcarlo prioritario sin justificativo
- **THEN** el sistema MUST denegar la acción e indicar que el justificativo es obligatorio

#### Scenario: Marcar prioritario no cambia el estado [BR-designaciones-017]

- **GIVEN** un pedido en `en_revision_coordinador`
- **WHEN** un actor lo marca prioritario con justificativo
- **THEN** el pedido queda con `prioritario = true` y conserva el estado `en_revision_coordinador`

#### Scenario: Quitar prioritario no exige justificativo

- **GIVEN** un pedido no terminal marcado como prioritario
- **WHEN** un actor con visibilidad sobre el pedido lo despriorizar sin comentario
- **THEN** el sistema MUST ejecutar la acción: el pedido queda con `prioritario = false`, sin exigir
  justificativo

#### Scenario: Quitar prioritario no cambia el estado

- **GIVEN** un pedido prioritario en `en_revision_secretaria`
- **WHEN** un actor lo despriorizar
- **THEN** el pedido conserva el estado `en_revision_secretaria`

### Requirement: Detalle del pedido role-aware con cadena de aprobación e historial

El sistema SHALL ofrecer una pantalla de detalle (`/designaciones/pedidos/:id`) visible para cualquier
rol con visibilidad por ámbito sobre el pedido, mostrando los datos completos del docente y del pedido
(`DataList`), la cadena de aprobación (`ApprovalTimeline`) y el historial de eventos (`AuditLog`). Un
botón/link **Volver** a la superficie de revisión (`/designaciones/revision`) MUST estar siempre
visible, para cualquier rol. Para el revisor de la etapa actual dentro de su ámbito, el detalle MUST
ofrecer las acciones Aceptar, Rechazar, Devolver, y **Marcar prioritario o Quitar prioritario según
corresponda** (nunca ambas a la vez — Marcar cuando el pedido no es prioritario, Quitar cuando ya lo
es), mediante un modal que aplica la regla de comentario obligatorio [BR-designaciones-005] (Marcar
prioritario también, ver BR-017; Quitar prioritario no). Para el resto de los roles, el detalle MUST
ser de solo lectura salvo el botón Volver. Un pedido `rechazado` MUST mostrar su motivo de rechazo
destacado (citado, diferenciado del resto del detalle).

#### Scenario: El revisor de la etapa ve las acciones, incluida la que corresponde de prioridad

- **GIVEN** un Coordinador viendo el detalle de un pedido no prioritario de su carrera en
  `en_revision_coordinador`
- **WHEN** abre `/designaciones/pedidos/:id`
- **THEN** ve los datos, la cadena de aprobación, el historial, el botón Volver, y las acciones
  Aceptar / Rechazar / Devolver / **Marcar prioritario**

#### Scenario: El pedido ya prioritario ofrece Quitar prioritario, no Marcar

- **GIVEN** un Coordinador viendo el detalle de un pedido **ya prioritario** de su carrera
- **WHEN** abre el detalle
- **THEN** ve la acción **Quitar prioritario** en lugar de "Marcar prioritario"

#### Scenario: El Jefe de Cátedra ve el detalle en solo lectura, con Volver

- **GIVEN** un Jefe de Cátedra viendo un pedido propio ya enviado a revisión
- **WHEN** abre el detalle
- **THEN** ve los datos, la cadena de aprobación, el historial y el botón Volver, sin acciones de
  revisión

#### Scenario: El historial se muestra con el verbo y la etapa de cada evento

- **GIVEN** un pedido con eventos de creación, envío y aceptación en su historial
- **WHEN** se muestra el detalle
- **THEN** el `AuditLog` lista cada evento con su actor, su acción y su fecha, y el `ApprovalTimeline`
  refleja la etapa alcanzada

#### Scenario: El botón Volver navega a la superficie de revisión

- **GIVEN** cualquier rol viendo el detalle de un pedido
- **WHEN** hace click en "Volver"
- **THEN** el sistema navega a `/designaciones/revision`

#### Scenario: El pedido rechazado muestra el motivo destacado

- **GIVEN** un pedido `rechazado` con un motivo de rechazo registrado en su historial
- **WHEN** se muestra su detalle
- **THEN** el motivo se muestra destacado (citado), diferenciado visualmente del resto del detalle

### Requirement: Confirmación de acciones de revisión vía modal

El sistema SHALL exigir una confirmación explícita en un modal antes de ejecutar cualquier acción de
revisión (aceptar, rechazar, devolver, marcar prioritario, **quitar prioritario**) desde el detalle de
pedido. Al disparar la acción desde el panel de revisión, el sistema MUST abrir un modal propio de esa
acción que matchea el diseño y MUST describir el efecto de la acción y a quién se notifica mediante
una caja de aviso (info para aceptar y quitar prioritario; warning para rechazar, devolver y marcar
prioritario). La mutación de dominio SHALL ejecutarse únicamente al confirmar dentro del modal; ninguna
acción de revisión MUST ejecutarse con un solo click directo sin pasar por la confirmación.

#### Scenario: Aceptar abre el modal de confirmación

- **GIVEN** un revisor en la etapa actual viendo el detalle de un pedido `en_revision_*`
- **WHEN** dispara la acción "Aceptar" desde el panel de revisión
- **THEN** el sistema abre el modal "Aceptar pedido" con el subtítulo de la etapa y la caja de aviso
  del efecto
- **AND** no cambia el estado del pedido hasta que el revisor confirme

#### Scenario: Quitar prioritario abre su propio modal de confirmación

- **GIVEN** un revisor viendo el detalle de un pedido prioritario dentro de su ámbito
- **WHEN** dispara la acción "Quitar prioritario"
- **THEN** el sistema abre el modal "Quitar prioridad" con una caja de aviso informativa del efecto
- **AND** no cambia `prioritario` hasta que el revisor confirme

#### Scenario: Confirmar en el modal ejecuta la transición

- **GIVEN** el modal de "Aceptar pedido" abierto
- **WHEN** el revisor confirma con "Aprobar y enviar"
- **THEN** el sistema ejecuta la aceptación, avanza la cadena y registra el evento en el historial
- **AND** cierra el modal

#### Scenario: Cancelar cierra el modal sin efecto

- **GIVEN** el modal de confirmación de cualquier acción abierto
- **WHEN** el revisor elige "Cancelar" (o cierra el modal)
- **THEN** el sistema no ejecuta ninguna mutación y el pedido conserva su estado

### Requirement: Validación del justificativo obligatorio dentro del modal

El sistema MUST validar la obligatoriedad del justificativo dentro del modal de confirmación para las
acciones que lo requieren —rechazar y devolver [BR-designaciones-005], y marcar prioritario
[BR-designaciones-017]—. Mientras el justificativo esté vacío, el botón de confirmar MUST estar
bloqueado y el modal MUST indicar que el justificativo es obligatorio; la acción NO MUST ejecutarse.
Para aceptar y para **quitar prioritario**, el comentario SHALL ser opcional.

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

#### Scenario: Quitar prioritario permite confirmar sin comentario

- **GIVEN** el modal "Quitar prioridad" abierto con el comentario vacío
- **WHEN** el revisor confirma
- **THEN** el sistema ejecuta la acción (el comentario es opcional)
