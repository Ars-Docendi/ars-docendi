## ADDED Requirements

### Requirement: La conexión con datos personales exige permiso y alcance global

El sistema SHALL usar la conexión con acceso a datos personales únicamente cuando el actor tenga el permiso correspondiente **y** su alcance sea global. En cualquier otro caso SHALL usar la conexión básica.

La condición MUST evaluarse sobre el permiso y el ámbito leídos del usuario autenticado, y MUST NOT depender de ningún código de rol embebido.

#### Scenario: Actor global con permiso obtiene la conexión con datos personales

- **GIVEN** un actor con el permiso de ver datos de docentes y alcance global
- **WHEN** se resuelve su perfil
- **THEN** corresponde la conexión con acceso a datos personales

#### Scenario: Actor con permiso pero ámbito acotado obtiene la conexión básica

- **GIVEN** un actor con el permiso y ámbito de materia o de carrera
- **WHEN** se resuelve su perfil
- **THEN** corresponde la conexión básica

#### Scenario: Actor sin el permiso obtiene la conexión básica

- **GIVEN** un actor sin el permiso de ver datos de docentes
- **WHEN** se resuelve su perfil
- **THEN** corresponde la conexión básica

### Requirement: Con la conexión básica el motor rechaza las columnas personales

El sistema SHALL apoyar la restricción en el motor y no en el código: con la conexión básica, una consulta sobre una columna personal MUST fallar en el motor.

#### Scenario: El motor rechaza la lectura

- **GIVEN** la conexión básica
- **WHEN** se consulta una columna personal de `identity.personas`
- **THEN** el motor rechaza la consulta por falta de privilegio

### Requirement: Un actor acotado que pregunta por datos personales recibe una abstención

El sistema SHALL resolver el turno con una abstención cuando el actor no puede acceder a los datos personales pedidos.

La respuesta MUST NOT contener los valores ni el error crudo del motor.

#### Scenario: El jefe de cátedra que pide teléfonos no los recibe

- **GIVEN** un actor con ámbito de materia que pregunta por teléfonos
- **WHEN** termina el turno
- **THEN** la respuesta no contiene ningún teléfono

#### Scenario: El rechazo del motor no se filtra al usuario

- **GIVEN** un turno en el que el motor rechaza la lectura de una columna personal
- **WHEN** se inspecciona el texto de la respuesta
- **THEN** no contiene el mensaje crudo del motor ni nombres de columnas

### Requirement: El riesgo residual está declarado en el código

El sistema SHALL documentar, en el código que toma la decisión, que un actor de ámbito acotado sigue pudiendo leer nombre, apellido y legajo de todo el padrón, con la referencia al trabajo de endurecimiento pendiente.

#### Scenario: La anotación existe donde se toma la decisión

- **WHEN** se inspecciona la resolución del perfil del actor
- **THEN** declara el riesgo residual y referencia el endurecimiento pendiente
