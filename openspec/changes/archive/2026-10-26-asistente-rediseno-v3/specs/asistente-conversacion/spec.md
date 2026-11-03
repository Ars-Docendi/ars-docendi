## ADDED Requirements

### Requirement: The pending turn shows «Consultando…» in place of its answer, with one honest state

The system SHALL show, once a turn has been in flight longer than the existing 400 ms
threshold, pulsing dots and «Consultando…» in the place where that turn's answer will
appear. The visual indicator SHALL be hidden from assistive technology, which keeps
receiving the single threshold-gated announcement outside the conversation live region.
The dots SHALL not animate under reduced motion. The system MUST NOT show any sequence of
steps or progress stages.

#### Scenario: A slow turn shows the inline indicator

- **GIVEN** a turn in flight for more than 400 ms
- **WHEN** the user looks at the conversation
- **THEN** the pending turn shows the dots and «Consultando…» where its answer will go

#### Scenario: The inline indicator is not announced twice

- **GIVEN** the inline indicator visible
- **WHEN** the accessibility tree is inspected
- **THEN** the inline indicator is hidden from it and the only processing announcement is the existing status one

### Requirement: The existing states keep their meaning in the v3 visual language

The system SHALL keep, restyled with the v3 tokens and layout: the transport-error box
titled «No se pudo consultar» with «Reintentar»; the stopped-waiting note; the degraded
and clarification alerts; the maintenance banner at the top of the conversation column;
the remaining-quota indicator and the blocked-state text in a strip under the composer,
outside the conversation live region, visible to every actor regardless of debug mode;
«Entendí: …» when the question was reinterpreted; «Ver la consulta» only with the
query-visibility permission; and «Cómo lo interpreté» only in frontend debug mode. The
metrics line («N consultas al modelo» / «Resuelto sin consultar al modelo») joins the
quota indicator and blocked-state text in the same strip, but — PO-changed
(2026-09-26) — only in frontend debug mode, the same switch that gates «Cómo lo
interpreté»: outside debug mode it never mounts, regardless of whether the last turn
carries metrics. The user's question SHALL be shown right-aligned on the neutral bubble
of the v3 design.

#### Scenario: The quota indicator stays out of the live region

- **GIVEN** an actor with a daily quota
- **WHEN** a turn completes and the indicator updates
- **THEN** the update is shown in the strip under the composer and is not part of the conversation live region

#### Scenario: The metrics line is absent outside debug mode

- **GIVEN** frontend debug mode is off and a turn just answered
- **WHEN** the user looks at the strip under the composer
- **THEN** the quota indicator is shown and the metrics line does not exist in the DOM

#### Scenario: The metrics line appears in debug mode

- **GIVEN** frontend debug mode is on and a turn just answered
- **WHEN** the user looks at the strip under the composer
- **THEN** the metrics line reads how many model calls that turn cost, next to the quota indicator

#### Scenario: The error box offers «Reintentar»

- **GIVEN** a turn that failed in transport
- **WHEN** the user looks at it
- **THEN** a box titled «No se pudo consultar» explains the failure in Spanish and offers «Reintentar»

## MODIFIED Requirements

### Requirement: El composer no envía mientras hay un turno en vuelo

El sistema MUST NOT iniciar un turno nuevo mientras otro está en vuelo, ni por Enter ni por el botón de envío, y SHALL permitir seguir escribiendo mientras tanto. Mientras hay un turno en vuelo, el lugar del botón de envío SHALL mostrar «Enviar» deshabilitado hasta el umbral del indicador y «Dejar de esperar» a partir de él; «Enviar» SHALL estar deshabilitado también cuando el campo está vacío.

#### Scenario: Enter durante un turno en vuelo no dispara otro

- **GIVEN** un turno en vuelo
- **WHEN** el usuario escribe otra pregunta y presiona Enter
- **THEN** no se emite un segundo pedido al backend
- **AND** el texto queda en el campo

#### Scenario: En vuelo no hay un «Enviar» habilitado

- **GIVEN** un turno en vuelo
- **WHEN** se inspecciona el lugar del botón de envío
- **THEN** muestra «Enviar» deshabilitado antes del umbral y «Dejar de esperar» después

#### Scenario: Con el campo vacío «Enviar» está deshabilitado

- **GIVEN** ningún turno en vuelo y el campo vacío
- **WHEN** se inspecciona «Enviar»
- **THEN** está deshabilitado

#### Scenario: Al terminar el turno se puede enviar de nuevo

- **GIVEN** un turno que acaba de resolver
- **WHEN** el usuario presiona Enter con texto en el campo
- **THEN** se emite un pedido nuevo con una clave de idempotencia distinta

### Requirement: El estado inicial usa sólo el catálogo

El sistema SHALL construir la pantalla de bienvenida sólo con el catálogo de capacidades: el destello, el título «¿Qué querés saber del sistema?» y los ejemplos verificados como tarjetas en una grilla de dos columnas; el alcance, la cantidad de áreas y los límites SHALL quedar en la ayuda «?» del encabezado. El sistema MUST NOT mostrar el nombre interno ni la descripción de ninguna área —la descripción es el comentario de la tabla que se le manda al modelo, no un texto para el usuario—, y SHALL quitar la bienvenida con el primer turno. Estos ejemplos son las únicas sugerencias clicables del asistente.

#### Scenario: Los ejemplos se muestran como tarjetas

- **GIVEN** un catálogo con cuatro ejemplos
- **WHEN** el usuario ve la bienvenida
- **THEN** ve el título y los cuatro ejemplos como tarjetas en dos columnas

#### Scenario: Ni el nombre interno ni el comentario de un área aparecen

- **GIVEN** un catálogo cuya área tiene nombre interno y un comentario escrito para el modelo
- **WHEN** el usuario ve la bienvenida y abre la ayuda «?»
- **THEN** ni el nombre interno ni el comentario aparecen en ningún lugar de la página

#### Scenario: Un ejemplo se envía al elegirlo

- **GIVEN** la bienvenida con ejemplos
- **WHEN** el usuario pulsa uno
- **THEN** se emite un pedido con ese texto como pregunta

#### Scenario: Los ejemplos se deshabilitan en vuelo

- **GIVEN** un turno en vuelo
- **WHEN** se inspeccionan los ejemplos
- **THEN** están deshabilitados

### Requirement: La conversación sobrevive al cierre del modal

El sistema SHALL conservar los turnos y el hilo del modal al cerrarlo y reabrirlo durante la misma sesión de la página, y MUST NOT persistir en el navegador ningún turno, pregunta, respuesta ni fila. La única preferencia que el asistente SHALL guardar en el navegador es el estado colapsado o expandido del rail, por usuario.

#### Scenario: Cerrar y reabrir conserva la respuesta

- **GIVEN** el modal con una respuesta en el hilo
- **WHEN** el usuario lo cierra y lo vuelve a abrir
- **THEN** la respuesta sigue en el hilo

#### Scenario: Nada de la conversación queda en el almacenamiento del navegador

- **GIVEN** una conversación con turnos y el rail colapsado
- **WHEN** se inspecciona el almacenamiento local y de sesión
- **THEN** no contiene ningún turno, pregunta ni fila, y sólo contiene la preferencia del rail

### Requirement: Desmontar el dueño de la conversación aborta el turno en vuelo

El sistema SHALL abortar el request en vuelo cuando se desmonta el lanzador —el único dueño del estado de la conversación—, y MUST NOT abortarlo al cerrar el modal ni al navegar entre pantallas de la aplicación.

#### Scenario: Desmontar el lanzador aborta

- **GIVEN** el modal con un turno en vuelo
- **WHEN** el lanzador se desmonta, por ejemplo al cerrar la sesión
- **THEN** la señal del turno queda abortada
- **AND** no se intenta actualizar el estado de un componente desmontado

#### Scenario: Cerrar el modal no aborta

- **GIVEN** el modal con un turno en vuelo
- **WHEN** el usuario cierra el modal
- **THEN** la señal del turno sigue activa
- **AND** al reabrir, la respuesta que llegó está en el hilo

### Requirement: «Dejar de esperar» aborta el pedido y libera el campo sin marcarlo como error

El sistema SHALL mostrar «Dejar de esperar» en el lugar del botón de envío del composer sólo mientras hay un turno en vuelo y pasado el umbral del indicador; al pulsarlo SHALL abortar el request, marcar el turno como no esperado con el texto «Dejaste de esperar la respuesta. La consulta ya salió y cuenta para tu cupo.», liberar el campo y devolverle el foco; MUST NOT presentarlo como error; y MUST NOT prometer que se cancela el trabajo del servidor.

#### Scenario: Dejar de esperar libera el composer

- **GIVEN** un turno en vuelo pasado el umbral
- **WHEN** el usuario pulsa «Dejar de esperar»
- **THEN** el request se aborta
- **AND** el turno muestra que se dejó de esperar y que la consulta cuenta para el cupo, sin alerta de error
- **AND** el foco está en el campo y «Dejar de esperar» ya no está

#### Scenario: Sin turno en vuelo no hay «Dejar de esperar»

- **GIVEN** una conversación sin turno en vuelo
- **WHEN** se inspecciona el composer
- **THEN** no hay «Dejar de esperar»

### Requirement: Copiar sólo se ofrece cuando el portapapeles existe

El sistema SHALL ofrecer, como controles de ícono con nombre accesible y tooltip, «Copiar respuesta» en la barra de acciones de cada respuesta, «Copiar pregunta» en las herramientas de cada pregunta y «Copiar tabla» —como TSV con cabecera, en el orden mostrado— en la vista de tabla ampliada; SHALL confirmar cada copia cambiando el ícono por una tilde y el nombre accesible a «Copiado» durante dos segundos; y MUST NOT renderizar ninguno cuando el portapapeles del navegador no está disponible.

#### Scenario: Copiar respuesta escribe el texto

- **GIVEN** una respuesta con texto y portapapeles disponible
- **WHEN** el usuario pulsa «Copiar respuesta»
- **THEN** el portapapeles contiene el texto de la respuesta
- **AND** el control pasa a «Copiado» con una tilde durante dos segundos

#### Scenario: Copiar pregunta escribe la pregunta

- **GIVEN** una pregunta enviada y portapapeles disponible
- **WHEN** el usuario pulsa «Copiar pregunta»
- **THEN** el portapapeles contiene el texto de la pregunta, sin ninguna etiqueta agregada

#### Scenario: Copiar tabla produce TSV con cabecera

- **GIVEN** una respuesta con tabla abierta en la vista ampliada
- **WHEN** el usuario pulsa «Copiar tabla»
- **THEN** el portapapeles contiene una fila de cabecera y una fila por resultado, separadas por tabulaciones

#### Scenario: Sin portapapeles no hay controles de copia

- **GIVEN** un navegador sin portapapeles disponible
- **WHEN** el usuario ve una respuesta
- **THEN** no hay ningún control de copiar

### Requirement: La superficie usa sólo tokens del tema y se ve igual en los dos montajes

El sistema SHALL pintar el modal —rail, encabezado, conversación, composer, tabla ampliada y aviso de deshacer— únicamente con tokens de `@ars-docendi/ui/theme.css`, SHALL respetar `prefers-reduced-motion` en la transición del rail, los puntos de «Consultando…» y cualquier otra animación, y SHALL verse correctamente en los temas claro y oscuro. El asistente tiene un único montaje, el modal; el diseño para anchos angostos queda fuera de este requisito.

#### Scenario: Ningún color fuera del tema

- **GIVEN** la hoja de estilos de la feature
- **WHEN** se buscan valores hexadecimales u `oklch(`
- **THEN** sólo aparecen en comentarios

#### Scenario: Con movimiento reducido el rail no anima

- **GIVEN** un usuario con `prefers-reduced-motion: reduce`
- **WHEN** colapsa o expande el rail
- **THEN** el cambio de ancho ocurre sin transición

## REMOVED Requirements

### Requirement: Sin acceso no hay formulario

**Reason**: The `/asistente` route that could render the panel without access is removed (ARS-151). The only mount is the launcher's modal, and the launcher is not rendered without access.
**Migration**: Covered by `asistente-superficie-frontend` "El acceso se decide por el permiso real y no por el rol" (no launcher without the permission) and by the `/asistente` redirect, which opens nothing for a user without access.
