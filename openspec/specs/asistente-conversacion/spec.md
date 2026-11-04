# asistente-conversacion Specification

## Purpose

Specifies the behavior of the assistant's conversation surface in the browser: composer and send rules, character limit, initial state built from the capability catalog, collapsed reasoning, error and retry handling, abort and focus management, copy controls and theme-token usage.

## Requirements

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

### Requirement: El límite de caracteres se muestra al acercarse

El sistema SHALL limitar el mensaje a 2 000 caracteres, SHALL mostrar un contador «N / 2 000» recién desde los 1 800 caracteres, y MUST NOT anunciar el contador en una región viva.

#### Scenario: Lejos del límite no hay contador

- **GIVEN** el campo con 1 799 caracteres
- **WHEN** se inspecciona el composer
- **THEN** no hay contador visible

#### Scenario: Cerca del límite aparece el contador

- **GIVEN** el campo con 1 800 caracteres
- **WHEN** se inspecciona el composer
- **THEN** se ve «1 800 / 2 000», asociado al campo como descripción

#### Scenario: El campo no acepta más del límite

- **GIVEN** el composer
- **WHEN** se inspecciona el campo
- **THEN** tiene un máximo de 2 000 caracteres

### Requirement: El botón de envío tiene etiqueta visible y Enter es un atajo

El sistema SHALL mostrar un botón de envío con la etiqueta visible «Enviar», SHALL enviar con Enter e insertar un salto con Shift+Enter, y SHALL invertir Enter a salto de línea cuando el puntero es grueso.

#### Scenario: Enter envía y Shift+Enter hace salto

- **GIVEN** el campo con texto y un puntero fino
- **WHEN** el usuario presiona Enter
- **THEN** se emite el pedido
- **AND** con Shift+Enter se inserta un salto de línea sin enviar

#### Scenario: Con puntero grueso Enter hace salto

- **GIVEN** un dispositivo con puntero grueso
- **WHEN** el usuario presiona Enter
- **THEN** no se emite ningún pedido
- **AND** el campo contiene un salto de línea

#### Scenario: El botón se llama «Enviar» y el lanzador sigue siendo «Preguntar»

- **GIVEN** el modal abierto
- **WHEN** se inspeccionan los botones
- **THEN** hay un solo botón «Preguntar» —el lanzador— y el envío se llama «Enviar»

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

### Requirement: El razonamiento se muestra colapsado cuando viene

El sistema SHALL mostrar el razonamiento de una respuesta como una disclosure cerrada «Cómo lo interpreté» dentro del mensaje, MUST NOT mostrar la disclosure cuando el razonamiento no viene, y SHALL mantener la pregunta interpretada visible fuera de la disclosure.

#### Scenario: Con razonamiento hay disclosure cerrada

- **GIVEN** una respuesta con razonamiento
- **WHEN** el usuario la ve
- **THEN** encuentra un resumen «Cómo lo interpreté» cerrado dentro del mensaje
- **AND** al abrirlo lee el razonamiento

#### Scenario: Sin razonamiento no hay disclosure

- **GIVEN** una respuesta sin razonamiento
- **WHEN** el usuario la ve
- **THEN** no hay ningún «Cómo lo interpreté»

#### Scenario: La pregunta interpretada sigue visible

- **GIVEN** una respuesta con pregunta interpretada y razonamiento
- **WHEN** el usuario la ve
- **THEN** lee «Entendí: …» sin abrir nada

### Requirement: Ninguna etiqueta interna se muestra

El sistema MUST NOT mostrar el valor crudo de `estado`, la categoría de las métricas, el nombre interno de las áreas, códigos HTTP ni nombres de excepciones, y SHALL excluir la categoría del tipo de la respuesta en el cliente.

#### Scenario: El texto de la página no contiene identificadores internos

- **GIVEN** una respuesta con métricas y un catálogo con nombres internos de áreas
- **WHEN** se lee todo el texto de la página
- **THEN** no contiene `consulta_simple`, `respondida`, `designaciones.` ni `identity.`

### Requirement: Un error ofrece reintentar con la misma clave

El sistema SHALL ofrecer «Reintentar» en un turno que terminó en error, SHALL reusar la clave de idempotencia y el texto del intento original, y MUST NOT ofrecer reintento en un turno en vuelo ni en uno que el usuario dejó de esperar.

#### Scenario: Reintentar reusa la clave

- **GIVEN** un turno que falló por red
- **WHEN** el usuario pulsa «Reintentar»
- **THEN** el pedido nuevo lleva la misma clave de idempotencia y el mismo texto

#### Scenario: Un turno en vuelo no ofrece reintento

- **GIVEN** un turno en vuelo
- **WHEN** se inspecciona el hilo
- **THEN** no hay ningún «Reintentar»

#### Scenario: Un turno que se dejó de esperar no ofrece reintento

- **GIVEN** un turno que el usuario dejó de esperar
- **WHEN** se inspecciona ese turno
- **THEN** no hay ningún «Reintentar»

### Requirement: Un hilo perdido se reinicia solo

El sistema SHALL descartar el identificador de hilo cuando el backend responde 404, de modo que el siguiente turno abra una conversación nueva.

#### Scenario: Tras un 404 el próximo turno va sin hilo

- **GIVEN** una conversación con hilo y un turno que respondió 404
- **WHEN** el usuario envía la siguiente pregunta
- **THEN** el pedido viaja sin identificador de hilo

### Requirement: Conversación nueva vacía el hilo

El sistema SHALL ofrecer «Nueva conversación», que vacía los turnos, descarta el hilo y devuelve el foco al campo; SHALL deshabilitarla sin turnos o en vuelo; y MUST NOT pedir confirmación.

#### Scenario: Nueva conversación arranca de cero

- **GIVEN** una conversación con al menos un turno
- **WHEN** el usuario pulsa «Nueva conversación»
- **THEN** el hilo queda vacío
- **AND** el próximo pedido viaja sin identificador de hilo
- **AND** el foco está en el campo

#### Scenario: Sin turnos está deshabilitada

- **GIVEN** el estado inicial
- **WHEN** se inspecciona «Nueva conversación»
- **THEN** está deshabilitada

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

### Requirement: Todo turno lleva señal de aborto y un tope de tiempo del cliente

El sistema SHALL emitir cada turno con una señal de aborto propia y un timeout por request de 160 000 ms, MUST NOT fijar ese timeout en el cliente HTTP compartido, y SHALL mostrar el timeout como un error con reintento.

#### Scenario: El pedido viaja con señal y timeout

- **GIVEN** un turno
- **WHEN** se emite el pedido
- **THEN** lleva una señal de aborto
- **AND** el cliente HTTP recibe un timeout de 160 000 ms para ese request

#### Scenario: El timeout es un error con reintento

- **GIVEN** un turno cuyo request venció por timeout
- **WHEN** el usuario lo ve
- **THEN** lee que el asistente tardó demasiado y que pruebe con una pregunta más acotada
- **AND** puede reintentar

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

### Requirement: El hilo no arrastra al usuario que subió

El sistema SHALL considerar anclado el hilo cuando está a 24 px o menos del fondo; al enviar SHALL desplazar al fondo; cuando llega contenido y el usuario no está anclado MUST NOT desplazarlo y SHALL mostrar «Ir al final»; al pulsar «Ir al final» SHALL desplazar al fondo sin animación.

#### Scenario: Subió y llega una respuesta

- **GIVEN** un hilo desplazado hacia arriba
- **WHEN** llega una respuesta
- **THEN** la posición de scroll no cambia
- **AND** aparece «Ir al final»

#### Scenario: Ir al final baja y desaparece

- **GIVEN** «Ir al final» visible
- **WHEN** el usuario lo pulsa
- **THEN** el hilo queda en el fondo
- **AND** el botón desaparece

#### Scenario: Al enviar siempre se ve la pregunta

- **GIVEN** un hilo desplazado hacia arriba
- **WHEN** el usuario envía una pregunta
- **THEN** el hilo se desplaza al fondo

### Requirement: Al llegar una respuesta el hilo muestra su inicio, no su final

El sistema SHALL desplazar el hilo al inicio de la tarjeta de respuesta cuando llega una respuesta y el usuario estaba anclado abajo.

#### Scenario: Anclado, la respuesta se ve desde el principio

- **GIVEN** un hilo anclado abajo
- **WHEN** llega una respuesta con una tabla larga
- **THEN** el hilo queda posicionado en el inicio de la tarjeta de respuesta
- **AND** no aparece «Ir al final»

### Requirement: El foco vuelve al lanzador al cerrar

El sistema SHALL devolver el foco al botón lanzador cuando el modal se cierra.

#### Scenario: Escape devuelve el foco

- **GIVEN** el modal abierto desde el lanzador
- **WHEN** el usuario presiona Escape
- **THEN** el foco está en el lanzador

### Requirement: El modal contiene el foco

El sistema SHALL hacer inerte el resto de la aplicación mientras el modal está abierto, de modo que Tab no salga del diálogo, y SHALL restaurarla al cerrar.

#### Scenario: Abierto, la aplicación de atrás está inerte

- **GIVEN** el modal abierto
- **WHEN** se inspecciona la raíz de la aplicación
- **THEN** está marcada como inerte

#### Scenario: Cerrado, vuelve a ser interactiva

- **GIVEN** el modal recién cerrado
- **WHEN** se inspecciona la raíz de la aplicación
- **THEN** no está marcada como inerte

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

### Requirement: Una tabla más ancha que la tarjeta scrollea dentro de su marco

El sistema SHALL hacer que la tabla de resultados scrollee en ambos ejes dentro de su propio marco, con cabecera pegajosa y celdas numéricas alineadas, y MUST NOT recortar columnas.

#### Scenario: El envoltorio lleva la clase propia

- **GIVEN** una respuesta con tabla
- **WHEN** se inspecciona el envoltorio de la tabla
- **THEN** lleva la clase que sobreescribe el recorte del envoltorio de la librería

#### Scenario: Ocho columnas en el modal no pierden datos

- **GIVEN** una respuesta con ocho columnas en el modal
- **WHEN** el usuario la ve
- **THEN** puede desplazar la tabla horizontalmente dentro de su marco y ver todas las columnas

### Requirement: Las columnas sensibles se marcan de forma visible y accesible

El sistema SHALL marcar cada columna sensible con un candado oculto a lectores y el texto «(dato personal)» sólo para lectores, SHALL mostrar la leyenda «Las columnas con candado contienen datos personales.» cuando hay al menos una, y MUST NOT explicar el enmascaramiento ni el proveedor.

#### Scenario: La cabecera sensible se anuncia

- **GIVEN** una respuesta con una columna sensible y otra que no
- **WHEN** se inspeccionan las cabeceras
- **THEN** la sensible se anuncia con «(dato personal)» y la otra no
- **AND** la leyenda está bajo la tabla

#### Scenario: Sin sensibles no hay leyenda

- **GIVEN** una respuesta sin columnas sensibles
- **WHEN** el usuario ve la tabla
- **THEN** no hay leyenda de datos personales

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

### Requirement: The reasoning disclosure only renders in frontend debug mode

The system SHALL render the "Cómo lo interpreté" disclosure only when the frontend debug
flag is on, and MUST NOT render it — even when `respuesta.razonamiento` is present —
when the flag is off. The flag SHALL default to off, including in a development build
(`import.meta.env.DEV`); it MUST be explicitly opted into via the frontend environment
variable `VITE_ASISTENTE_DEBUG` set to `"true"`.

#### Scenario: Debug mode off hides the disclosure even with reasoning

- **GIVEN** a response with `razonamiento` and the frontend debug flag off
- **WHEN** the user views it
- **THEN** there is no "Cómo lo interpreté" disclosure anywhere in the message

#### Scenario: Debug mode on shows the collapsed disclosure

- **GIVEN** a response with `razonamiento` and the frontend debug flag on
- **WHEN** the user views it
- **THEN** they find a closed "Cómo lo interpreté" summary inside the message
- **AND** opening it reveals the reasoning

#### Scenario: A development build does not enable debug mode by itself

- **GIVEN** a development build (`import.meta.env.DEV` true) without the debug
  environment variable set
- **WHEN** a response with `razonamiento` is viewed
- **THEN** there is no "Cómo lo interpreté" disclosure

#### Scenario: The interpreted-question line is unaffected

- **GIVEN** a response with `preguntaInterpretada` and `razonamiento`, and the frontend
  debug flag off
- **WHEN** the user views it
- **THEN** they still read "Entendí: …" visibly, outside of any disclosure

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
