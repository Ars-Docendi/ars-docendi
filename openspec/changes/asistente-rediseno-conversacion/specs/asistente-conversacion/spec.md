## ADDED Requirements

### Requirement: El composer no envía mientras hay un turno en vuelo

El sistema MUST NOT iniciar un turno nuevo mientras otro está en vuelo, ni por Enter ni por el botón de envío, y SHALL permitir seguir escribiendo mientras tanto.

#### Scenario: Enter durante un turno en vuelo no dispara otro

- **GIVEN** un turno en vuelo
- **WHEN** el usuario escribe otra pregunta y presiona Enter
- **THEN** no se emite un segundo pedido al backend
- **AND** el texto queda en el campo

#### Scenario: El botón de envío está deshabilitado en vuelo

- **GIVEN** un turno en vuelo
- **WHEN** se inspecciona el botón «Enviar»
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

El sistema SHALL construir el estado inicial con el alcance, los ejemplos, la cantidad de áreas y los límites que devuelve el catálogo de capacidades, MUST NOT mostrar el nombre interno ni la descripción de ninguna área —la descripción es el comentario de la tabla que se le manda al modelo, no un texto para el usuario—, y SHALL quitar el estado inicial con el primer turno.

#### Scenario: De las áreas sólo se dice cuántas hay

- **GIVEN** un catálogo cuya área tiene nombre interno y un comentario escrito para el modelo
- **WHEN** el usuario ve el estado inicial
- **THEN** lee cuántas áreas de datos conoce el asistente
- **AND** ni el nombre interno ni el comentario aparecen en ningún lugar de la página

#### Scenario: Un ejemplo se envía al elegirlo

- **GIVEN** el estado inicial con ejemplos
- **WHEN** el usuario pulsa uno
- **THEN** se emite un pedido con ese texto como pregunta

#### Scenario: Los ejemplos se deshabilitan en vuelo

- **GIVEN** un turno en vuelo
- **WHEN** se inspeccionan los ejemplos
- **THEN** están deshabilitados

### Requirement: Sin acceso no hay formulario

El sistema SHALL mostrar únicamente el aviso de sin acceso en la ruta del asistente cuando el backend responde 403, y MUST NOT renderizar el campo ni el botón de envío.

#### Scenario: Con 403 sólo hay aviso

- **GIVEN** un usuario cuyo catálogo de capacidades responde 403
- **WHEN** navega a la ruta del asistente
- **THEN** lee que no tiene acceso con sus permisos actuales
- **AND** no hay campo de pregunta ni botón de envío

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

El sistema SHALL conservar los turnos y el hilo del modal al cerrarlo y reabrirlo durante la misma sesión de la página, MUST NOT persistirlos en el navegador, y SHALL mantener la conversación de la ruta y la del modal como hilos independientes.

#### Scenario: Cerrar y reabrir conserva la respuesta

- **GIVEN** el modal con una respuesta en el hilo
- **WHEN** el usuario lo cierra y lo vuelve a abrir
- **THEN** la respuesta sigue en el hilo

#### Scenario: Nada queda en el almacenamiento del navegador

- **GIVEN** una conversación con turnos
- **WHEN** se inspecciona el almacenamiento local y de sesión
- **THEN** no contiene ningún turno

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

El sistema SHALL mostrar «Dejar de esperar» sólo mientras hay un turno en vuelo; al pulsarlo SHALL abortar el request, marcar el turno como no esperado con un texto que diga que la consulta ya salió y cuenta para el cupo, liberar el campo y devolverle el foco; MUST NOT presentarlo como error; y MUST NOT prometer que se cancela el trabajo del servidor.

#### Scenario: Dejar de esperar libera el composer

- **GIVEN** un turno en vuelo
- **WHEN** el usuario pulsa «Dejar de esperar»
- **THEN** el request se aborta
- **AND** el turno muestra que se dejó de esperar y que la consulta cuenta para el cupo, sin alerta de error
- **AND** «Enviar» queda habilitado y el foco está en el campo
- **AND** «Dejar de esperar» ya no está

#### Scenario: Sin turno en vuelo no hay «Dejar de esperar»

- **GIVEN** una conversación sin turno en vuelo
- **WHEN** se inspecciona la franja de estado
- **THEN** no hay «Dejar de esperar»

### Requirement: Desmontar el dueño de la conversación aborta el turno en vuelo

El sistema SHALL abortar el request en vuelo cuando se desmonta el componente dueño del estado de la conversación, y MUST NOT abortarlo al cerrar el modal.

#### Scenario: Navegar fuera de la ruta aborta

- **GIVEN** la ruta del asistente con un turno en vuelo
- **WHEN** la página se desmonta
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

El sistema SHALL ofrecer «Copiar respuesta» y, si hay tabla, «Copiar tabla» como TSV con cabecera; SHALL confirmar cambiando la etiqueta a «Copiado» durante dos segundos; SHALL mantener las acciones siempre visibles; y MUST NOT renderizarlas cuando el portapapeles del navegador no está disponible.

#### Scenario: Copiar respuesta escribe el texto

- **GIVEN** una respuesta con texto y portapapeles disponible
- **WHEN** el usuario pulsa «Copiar respuesta»
- **THEN** el portapapeles contiene el texto de la respuesta
- **AND** la etiqueta pasa a «Copiado»

#### Scenario: Copiar tabla produce TSV con cabecera

- **GIVEN** una respuesta con tabla
- **WHEN** el usuario pulsa «Copiar tabla»
- **THEN** el portapapeles contiene una fila de cabecera y una fila por resultado, separadas por tabulaciones

#### Scenario: Sin portapapeles no hay botón

- **GIVEN** un navegador sin portapapeles disponible
- **WHEN** el usuario ve una respuesta
- **THEN** no hay ningún botón de copiar

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

El sistema SHALL pintar la conversación únicamente con tokens de `@ars-docendi/ui/theme.css`, SHALL mostrar el mismo panel con el mismo aspecto en la ruta y en el modal, SHALL ocupar la pantalla completa en anchos de 640 px o menos, y SHALL respetar `prefers-reduced-motion`.

#### Scenario: Ningún color fuera del tema

- **GIVEN** la hoja de estilos de la feature
- **WHEN** se buscan valores hexadecimales
- **THEN** sólo aparecen en comentarios

#### Scenario: El texto de la respuesta es el mismo en ambos montajes

- **GIVEN** la misma respuesta en la ruta y en el modal
- **WHEN** se comparan
- **THEN** el color y el tamaño del texto son los mismos
