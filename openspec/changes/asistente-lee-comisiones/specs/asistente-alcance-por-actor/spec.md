## MODIFIED Requirements

### Requirement: El alcance se evalúa por dominio consultado

El sistema SHALL decidir si un resultado vacío puede presentarse como «no hay datos» evaluando los **dominios que la consulta tocó**, y no con un único booleano del turno.

Una consulta que toca tablas de portal SHALL exigir, además de lo que exija por sus otros dominios, el permiso de trayectoria ajena y el alcance de portal del actor.

El sistema SHALL derivar qué dominios tocó la consulta de la **misma** detección que alimenta la declaración de cobertura. El sistema MUST NOT mantener una segunda detección del mismo hecho.

Ante un dominio que no reconoce, el sistema MUST NOT concluir que el actor alcanza todo.

Una consulta que toca sólo tablas de comisiones, más los catálogos de identity, SHALL considerarse que alcanza todo si y sólo si el actor tiene `comisiones.ver`, sin conjugar ámbito.

Una consulta que toca comisiones y designaciones SHALL considerarse que alcanza todo sólo si el actor cumple además la regla de designaciones.

#### Scenario: Un actor global sin el permiso de portal no afirma inexistencia

- **GIVEN** un actor de ámbito global con `designaciones.ver` y sin `portal.ver_trayectoria_ajena`
- **WHEN** pregunta por una habilidad que otras personas declararon
- **THEN** la respuesta reconoce el límite de alcance y no afirma que no haya registros

#### Scenario: Un jefe de cátedra con el permiso tampoco afirma inexistencia sobre el padrón

- **GIVEN** un jefe de cátedra con `portal.ver_trayectoria_ajena`
- **WHEN** pregunta por una habilidad que sólo declararon docentes fuera de sus materias
- **THEN** la respuesta reconoce el límite de alcance

#### Scenario: Una consulta que no toca portal conserva su evaluación

- **GIVEN** un actor de ámbito global con `designaciones.ver` y sin el permiso de portal
- **WHEN** pregunta algo que sólo toca designaciones e identity
- **THEN** un resultado vacío puede presentarse como que no hay registros

#### Scenario: Un actor global sin comisiones.ver no alcanza todo

- **GIVEN** un actor de ámbito global que no tiene `comisiones.ver`
- **WHEN** una consulta que sólo toca comisiones devuelve cero filas
- **THEN** el texto reconoce el límite de alcance y no afirma que no haya registros

#### Scenario: Una consulta sólo de comisiones no depende del ámbito de designaciones

- **GIVEN** un jefe de cátedra, que tiene `comisiones.ver` y no alcanza todas las designaciones
- **WHEN** una consulta que sólo toca comisiones devuelve cero filas
- **THEN** el texto puede decir que no hay registros que coincidan

#### Scenario: No se gasta el reintento donde no puede ayudar

- **GIVEN** un resultado vacío de un actor que no alcanza todo lo que la consulta tocó
- **WHEN** el sistema decide si reintenta la generación
- **THEN** no reintenta
