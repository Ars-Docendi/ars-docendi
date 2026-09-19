## ADDED Requirements

### Requirement: El catálogo se deriva de los privilegios efectivos

El sistema SHALL construir el catálogo de capacidades a partir de los privilegios efectivos del rol con el que el actor consulta, y MUST NOT derivarlo del contenido del prompt.

#### Scenario: El catálogo no menciona una columna que el actor no puede leer

- **GIVEN** un actor sin acceso a datos personales
- **WHEN** pide el catálogo de capacidades
- **THEN** no aparece ninguna columna personal

#### Scenario: Dos actores con permisos distintos reciben conteos distintos

- **GIVEN** un actor con acceso a datos personales y otro sin él
- **WHEN** los dos piden el catálogo
- **THEN** los conteos de columnas difieren

#### Scenario: Los conteos salen de la base

- **GIVEN** el catálogo de un actor
- **WHEN** se comparan sus conteos con las columnas que su rol puede leer
- **THEN** coinciden

### Requirement: Los ejemplos del catálogo son ejecutables por el actor

El sistema SHALL incluir entre cuatro y seis ejemplos tomados del catálogo de ejemplos verificados, y cada ejemplo SHALL ser ejecutable con los privilegios del actor.

#### Scenario: Un ejemplo que el actor no puede ejecutar no se ofrece

- **GIVEN** un ejemplo del catálogo que toca una columna personal
- **WHEN** un actor sin acceso a datos personales pide el catálogo
- **THEN** ese ejemplo no aparece

#### Scenario: Los ejemplos vienen del catálogo verificado

- **GIVEN** el catálogo de capacidades de un actor
- **WHEN** se comparan sus ejemplos con el catálogo de ejemplos
- **THEN** cada ejemplo ofrecido es la pregunta de un ejemplo del catálogo

### Requirement: El catálogo dice qué no puede responder

El sistema SHALL incluir en el catálogo los límites de lo que el asistente puede hacer.

#### Scenario: El catálogo declara que no escribe

- **WHEN** un actor pide el catálogo
- **THEN** entre los límites figura que el asistente no modifica nada

#### Scenario: El catálogo declara que no consulta fuentes externas

- **WHEN** un actor pide el catálogo
- **THEN** entre los límites figura que solo consulta datos del propio sistema

### Requirement: El catálogo distingue el alcance de las capacidades

El sistema SHALL informar el ámbito del actor por separado de los conteos de capacidades.

#### Scenario: El ámbito no altera los conteos

- **GIVEN** dos actores con el mismo acceso a datos y ámbitos distintos
- **WHEN** los dos piden el catálogo
- **THEN** los conteos coinciden y el ámbito informado difiere

### Requirement: El catálogo resuelve sin llamar al modelo

El sistema MUST NOT emitir ninguna llamada al proveedor del modelo para construir el catálogo.

#### Scenario: Pedir el catálogo cuesta cero tokens

- **WHEN** un actor pide el catálogo
- **THEN** el proveedor del modelo no recibe ninguna llamada

#### Scenario: Con el proveedor caído el catálogo sigue respondiendo

- **GIVEN** el corte al proveedor abierto
- **WHEN** un actor pide el catálogo
- **THEN** lo recibe completo

### Requirement: La meta-pregunta se responde con el catálogo real

El sistema SHALL responder la meta-pregunta con el catálogo derivado de los privilegios, y MUST NOT responderla con un texto fijo.

#### Scenario: «¿qué podés hacer?» devuelve capacidades reales

- **GIVEN** un actor
- **WHEN** pregunta qué puede hacer el asistente
- **THEN** la respuesta menciona áreas y ejemplos derivados de sus privilegios

#### Scenario: La meta-pregunta sigue costando cero tokens

- **WHEN** un actor hace la meta-pregunta
- **THEN** el proveedor del modelo no recibe ninguna llamada
