## MODIFIED Requirements

### Requirement: El alcance se evalúa por dominio consultado

El sistema SHALL decidir si un resultado vacío puede presentarse como «no hay datos» evaluando los **dominios que la consulta tocó**, y no con un único booleano del turno.

Una consulta que toca tablas de portal SHALL exigir, además de lo que exija por sus otros dominios, el permiso de trayectoria ajena y el alcance de portal del actor.

El sistema SHALL derivar qué dominios tocó la consulta de la **misma** detección que alimenta la declaración de cobertura. El sistema MUST NOT mantener una segunda detección del mismo hecho.

Ante un dominio que no reconoce, el sistema MUST NOT concluir que el actor alcanza todo.

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
