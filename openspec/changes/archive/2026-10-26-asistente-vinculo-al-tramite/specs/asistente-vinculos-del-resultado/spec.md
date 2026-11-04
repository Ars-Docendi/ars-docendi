## ADDED Requirements

### Requirement: El turno ofrece vínculo sólo a lo que el actor puede abrir

El sistema SHALL poder acompañar cada fila del resultado con un vínculo a la pantalla que muestra el recurso que esa fila identifica.

El sistema SHALL obtener ese vínculo del **módulo dueño del recurso**, con el mismo criterio de autorización que el endpoint de detalle de ese módulo. El sistema MUST NOT derivar el vínculo del hecho de que la fila haya sido devuelta: las filas las filtra el motor con sus propias policies, y la pantalla la autoriza el módulo, que son reglas distintas.

Cuando el módulo dueño no reconoce el recurso, o lo reconoce y el actor no puede abrirlo, el sistema MUST NOT ofrecer vínculo para esa fila.

Un fallo al resolver los vínculos MUST NOT impedir que el turno responda.

#### Scenario: Una fila que el actor puede abrir ofrece el vínculo

- **GIVEN** un actor que puede abrir el detalle de un trámite
- **WHEN** el asistente le devuelve una fila que cita el número de ese trámite
- **THEN** la respuesta trae un vínculo que señala esa celda

#### Scenario: Una fila que el actor no puede abrir no ofrece nada

- **GIVEN** un actor cuyo ámbito en el módulo dueño no alcanza a ese trámite
- **WHEN** el asistente le devuelve una fila que cita su número
- **THEN** la respuesta no trae ningún vínculo para esa fila
- **AND** la fila se muestra igual

#### Scenario: Sin filas no se consulta a nadie

- **GIVEN** un turno que termina sin filas
- **WHEN** se arma la respuesta
- **THEN** no se le pregunta nada al módulo dueño

#### Scenario: Un resolutor caído no tumba el turno

- **GIVEN** un turno con filas
- **WHEN** la resolución de vínculos falla
- **THEN** el turno responde con su texto y sus filas, y sin vínculos

### Requirement: El vínculo nombra el recurso, no la ruta

El sistema SHALL expresar cada vínculo como la posición en el resultado —fila y columna—, un **tipo** de recurso y su identificador.

El sistema MUST NOT incluir la ruta de la interfaz en la respuesta: la ruta es una decisión del cliente.

El cliente SHALL resolver la ruta a partir del tipo, y MUST NOT pintar vínculo alguno para un tipo que no conoce.

#### Scenario: Un tipo desconocido no se pinta

- **GIVEN** una respuesta con un vínculo de un tipo que el cliente no tiene en su mapa
- **WHEN** se muestra la tabla
- **THEN** la celda se muestra como texto, sin enlace

### Requirement: El módulo del asistente no referencia a los módulos que enlaza

El sistema SHALL declarar en el asistente un puerto de resolución de vínculos, y componer su implementación en el composition root.

El módulo del asistente MUST NOT referenciar el `Contracts` de ningún otro módulo para esta capacidad.

Con el puerto sin componer, el sistema SHALL responder los turnos sin vínculos.

#### Scenario: El asistente arranca sin quien resuelva

- **GIVEN** el módulo del asistente registrado sin adaptador
- **WHEN** un turno devuelve filas
- **THEN** responde normalmente y sin vínculos

### Requirement: Navegar desde el asistente conserva la conversación

Cuando el usuario sigue un vínculo desde el asistente abierto sobre otra pantalla, el sistema SHALL cerrar el asistente y llevarlo al destino.

El sistema SHALL conservar la conversación: al volver a abrir el asistente, el hilo sigue donde estaba.

#### Scenario: Seguir un vínculo desde el modal

- **GIVEN** una conversación con al menos un turno respondido y un vínculo
- **WHEN** el usuario sigue el vínculo
- **THEN** el modal se cierra y la aplicación navega al detalle
- **AND** al reabrir el asistente la conversación sigue completa
