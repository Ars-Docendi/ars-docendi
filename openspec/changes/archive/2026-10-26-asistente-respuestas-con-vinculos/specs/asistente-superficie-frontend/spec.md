## ADDED Requirements

### Requirement: Cada respuesta se presenta en un bloque propio

El cliente SHALL mostrar todo lo que responde a una pregunta —texto, tabla, ficha, opciones, avisos de estado, procedencia y barra de acciones— dentro de un bloque con fondo elevado, borde y radio del tema, separado de la burbuja de la pregunta.

El bloque MUST usar sólo tokens del tema y verse igual en los dos montajes.

#### Scenario: La respuesta se distingue de la pregunta

- **GIVEN** una conversación con un turno respondido
- **WHEN** el usuario mira el hilo
- **THEN** la pregunta se ve en su burbuja y la respuesta completa dentro de un bloque con borde

#### Scenario: El estado «Consultando…» ocupa el lugar del bloque

- **GIVEN** un turno en vuelo
- **WHEN** pasa el umbral de espera
- **THEN** «Consultando…» se muestra dentro del bloque de esa respuesta

### Requirement: El texto muestra como chips los elementos vinculados

El cliente SHALL mostrar cada posición señalada en el texto como un enlace en línea con aspecto de chip, con un nombre accesible que diga qué abre, y SHALL dejar el resto del texto sin cambios.

Seguir el chip SHALL comportarse como seguir el vínculo de una celda: cierra el asistente, navega al detalle y conserva la conversación.

Cuando la respuesta tiene elemento principal, el cliente MUST NOT mostrar chips en el texto: el enlace lo lleva la ficha.

#### Scenario: Un trámite nombrado en el texto se abre desde el chip

- **GIVEN** una respuesta de varias filas cuyo texto nombra un trámite vinculado
- **WHEN** el usuario sigue el chip de ese trámite
- **THEN** el asistente se cierra y la aplicación navega al detalle del trámite

#### Scenario: Con ficha no hay chips

- **GIVEN** una respuesta con elemento principal cuyo texto nombra ese mismo trámite
- **WHEN** se muestra la respuesta
- **THEN** el texto no tiene chips y la ficha ofrece «Abrir pedido»

### Requirement: Una respuesta con elemento principal lo muestra en una ficha

Cuando la respuesta tiene elemento principal de un tipo que el cliente conoce, el cliente SHALL mostrar una ficha con el tipo, el identificador legible, el estado y los datos clave del elemento, y un botón que abre su detalle.

Con ficha, el cliente MUST NOT repetir la tabla de una fila debajo: la tabla SHALL seguir disponible con «Ampliar tabla» de la barra de acciones.

Para un tipo que el cliente no conoce, el cliente SHALL mostrar la respuesta como si no tuviera elemento principal.

#### Scenario: Un pedido se muestra en su ficha

- **GIVEN** una respuesta cuyo principal es el trámite 2026-9005 con su ficha
- **WHEN** se muestra la respuesta
- **THEN** se ve la ficha del pedido con su estado y el botón «Abrir pedido»
- **AND** la tabla no se muestra debajo y se puede abrir con «Ampliar tabla»

#### Scenario: Abrir el pedido desde la ficha

- **GIVEN** una ficha de pedido
- **WHEN** el usuario elige «Abrir pedido»
- **THEN** el asistente se cierra y la aplicación navega al detalle de ese pedido

### Requirement: La respuesta dice de dónde salen sus datos

Cuando la respuesta nombra áreas de datos, el cliente SHALL mostrar al pie del bloque una línea «Datos de …» con el nombre de cada área para el usuario.

El cliente MUST NOT mostrar cantidades de filas, la consulta ni ninguna otra referencia a cómo se obtuvo el dato en esa línea.

#### Scenario: El pie nombra el área

- **GIVEN** una respuesta que nombra el área `designaciones`
- **WHEN** se muestra la respuesta
- **THEN** el pie dice «Datos de Designaciones»

### Requirement: La última respuesta se puede repetir

El cliente SHALL ofrecer «Repetir consulta» debajo de la última pregunta cuando su turno terminó con una respuesta, no hay un turno en vuelo y el asistente no está bloqueado.

Repetir SHALL reenviar exactamente la misma pregunta, con sus menciones, por el mismo camino que «Editar y reenviar»: reemplaza la última pregunta y su respuesta, y es un turno nuevo para el cupo.

#### Scenario: Repetir la última consulta

- **GIVEN** una conversación cuyo último turno está respondido
- **WHEN** el usuario elige «Repetir consulta»
- **THEN** la misma pregunta se envía otra vez
- **AND** la nueva respuesta reemplaza a la anterior en el hilo

#### Scenario: Sólo el último turno se repite

- **GIVEN** una conversación con dos turnos respondidos
- **WHEN** el usuario mira el primero
- **THEN** no se ofrece «Repetir consulta» en él

#### Scenario: Sin cupo no se ofrece repetir

- **GIVEN** un actor que alcanzó su límite de consultas
- **WHEN** mira el último turno
- **THEN** no se ofrece «Repetir consulta»

### Requirement: Después de seguir un vínculo se puede volver a la conversación

Cuando el asistente se cierra porque el usuario siguió un vínculo de una respuesta, el cliente SHALL mostrar en la pantalla de destino un botón flotante «Volver a la conversación» que reabre el asistente en el mismo hilo.

El botón SHALL desaparecer al reabrir el asistente o al navegar a otra pantalla, y MUST NOT aparecer cuando el asistente se cerró por otro motivo.

#### Scenario: Volver desde el detalle

- **GIVEN** un usuario que siguió un vínculo desde una respuesta
- **WHEN** llega al detalle
- **THEN** ve «Volver a la conversación»
- **AND** al elegirlo el asistente se abre con la conversación completa

#### Scenario: Cerrar el asistente no ofrece volver

- **GIVEN** un asistente abierto con una conversación
- **WHEN** el usuario lo cierra con «×»
- **THEN** no aparece «Volver a la conversación»

#### Scenario: Seguir navegando lo quita

- **GIVEN** el botón «Volver a la conversación» visible en el detalle
- **WHEN** el usuario navega a otra pantalla
- **THEN** el botón ya no se muestra
