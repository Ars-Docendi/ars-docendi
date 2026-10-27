## ADDED Requirements

### Requirement: Opciones y sugerencias son campos separados

El sistema SHALL exponer las opciones de una aclaración y las sugerencias de un rechazo en campos distintos, y MUST NOT usar un solo campo para las dos cosas.

#### Scenario: Una aclaración trae opciones y no sugerencias

- **GIVEN** un turno que terminó necesitando una aclaración
- **WHEN** el cliente lee la respuesta
- **THEN** trae opciones y el campo de sugerencias está vacío

#### Scenario: Un rechazo trae sugerencias y no opciones

- **GIVEN** un turno que terminó como no contestable
- **WHEN** el cliente lee la respuesta
- **THEN** trae sugerencias y el campo de opciones está vacío

### Requirement: Todo rechazo trae al menos una sugerencia accionable

El sistema SHALL incluir al menos una sugerencia en cada turno que termine como no contestable, y las sugerencias SHALL ser preguntas del catálogo de ejemplos verificados.

#### Scenario: Un rechazo por esquema insuficiente sugiere qué preguntar

- **GIVEN** una pregunta que el asistente no puede responder
- **WHEN** el turno termina
- **THEN** la respuesta incluye al menos una sugerencia

#### Scenario: Las sugerencias son preguntas que el asistente sabe responder

- **GIVEN** un turno rechazado
- **WHEN** se comparan sus sugerencias con el catálogo de ejemplos
- **THEN** cada sugerencia es la pregunta de un ejemplo del catálogo

#### Scenario: Sin parecido léxico igual hay sugerencias

- **GIVEN** una pregunta que no se parece a ningún ejemplo del catálogo
- **WHEN** el turno se rechaza
- **THEN** igualmente se devuelven sugerencias

### Requirement: La consulta generada solo se expone con permiso

El sistema SHALL incluir la consulta generada en la respuesta únicamente cuando el actor tenga el permiso correspondiente, y MUST NOT incluirla en ningún otro caso.

#### Scenario: Sin el permiso, la consulta no viaja

- **GIVEN** un actor sin el permiso de ver la consulta
- **WHEN** hace una pregunta que se responde
- **THEN** la respuesta no contiene ninguna consulta

#### Scenario: Con el permiso, la consulta viaja

- **GIVEN** un actor con el permiso de ver la consulta
- **WHEN** hace una pregunta que se responde
- **THEN** la respuesta contiene la consulta que se ejecutó

#### Scenario: El permiso no se concede a ningún rol por omisión

- **GIVEN** una base recién migrada
- **WHEN** se consultan los roles que tienen el permiso de ver la consulta
- **THEN** no lo tiene ninguno

### Requirement: La respuesta no expone etiquetas internas

El sistema MUST NOT incluir en ningún campo visible al usuario identificadores internos, nombres de excepción, ni mensajes crudos del motor o del proveedor.

#### Scenario: Un fallo del motor se muestra como texto comprensible

- **GIVEN** una consulta que el motor rechaza
- **WHEN** el usuario lee la respuesta
- **THEN** no contiene el nombre de ninguna tabla, columna ni código de error
