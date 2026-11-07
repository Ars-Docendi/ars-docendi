## ADDED Requirements

### Requirement: El asistente tiene dos montajes

El sistema SHALL exponer el asistente en una ruta propia y en un lanzador presente en toda la aplicación.

#### Scenario: La ruta propia existe

- **WHEN** el usuario navega a la ruta del asistente
- **THEN** ve la vista del asistente

#### Scenario: El lanzador está en la barra superior

- **GIVEN** un usuario con acceso al asistente
- **WHEN** mira la barra superior
- **THEN** encuentra el lanzador del asistente, habilitado

#### Scenario: El lanzador abre la misma vista

- **GIVEN** un usuario en cualquier pantalla
- **WHEN** usa el lanzador
- **THEN** ve la misma vista del asistente, sin perder dónde estaba

### Requirement: El acceso se decide por el permiso real y no por el rol

El sistema SHALL decidir la visibilidad del asistente consultando al backend, y MUST NOT decidirla con una lista de roles del frontend.

#### Scenario: Sin permiso no se ve el lanzador

- **GIVEN** un usuario cuyo rol no tiene el permiso del asistente
- **WHEN** mira la barra superior
- **THEN** no encuentra el lanzador

#### Scenario: Con permiso se ve el lanzador

- **GIVEN** un usuario cuyo rol tiene el permiso
- **WHEN** mira la barra superior
- **THEN** encuentra el lanzador habilitado

#### Scenario: El botón deshabilitado desaparece

- **GIVEN** la barra superior
- **WHEN** se la inspecciona
- **THEN** no queda ningún botón de ayuda deshabilitado con leyenda de «próximamente»

### Requirement: Los cuatro estados se renderizan distinguibles

El sistema SHALL renderizar de forma distinguible la respuesta, el rechazo, la aclaración y el servicio degradado.

#### Scenario: El degradado se muestra como estado y no como error

- **GIVEN** un turno que resolvió como servicio degradado
- **WHEN** el usuario lo ve
- **THEN** se le presenta como una situación temporal del asistente y no como un fallo de su pregunta

#### Scenario: La aclaración ofrece sus opciones para elegir

- **GIVEN** un turno que necesita aclaración
- **WHEN** el usuario lo ve
- **THEN** puede elegir una de las opciones sin volver a escribir la pregunta

#### Scenario: Un rechazo ofrece sus sugerencias

- **GIVEN** un turno no contestable con sugerencias
- **WHEN** el usuario lo ve
- **THEN** puede usar una sugerencia directamente

#### Scenario: Opciones y sugerencias se distinguen entre sí

- **GIVEN** un turno con opciones y otro con sugerencias
- **WHEN** el usuario los compara
- **THEN** las opciones se presentan como una elección que continúa el turno y las sugerencias como preguntas nuevas

### Requirement: Las columnas sensibles se muestran como tabla

El sistema SHALL renderizar las filas devueltas cuando el resultado trae columnas, con los valores reales.

#### Scenario: Un resultado con filas se muestra en tabla

- **GIVEN** un turno respondido con columnas y filas
- **WHEN** el usuario lo ve
- **THEN** ve la tabla con los valores

#### Scenario: Un resultado truncado lo dice sin declarar cuántas filas faltan

- **GIVEN** un turno cuyo resultado se truncó
- **WHEN** el usuario lo ve
- **THEN** se le informa que hay más resultados y **no** se le dice cuántos

### Requirement: Ninguna etiqueta interna llega a la interfaz

El sistema MUST NOT mostrar identificadores internos, nombres de excepción ni mensajes crudos de transporte.

#### Scenario: Un error de transporte se muestra comprensible

- **GIVEN** un pedido que falla por red
- **WHEN** el usuario lo ve
- **THEN** lee un mensaje en español que no contiene códigos de estado ni nombres técnicos
