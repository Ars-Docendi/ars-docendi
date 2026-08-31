## ADDED Requirements

### Requirement: El enrutador decide el carril y no ejecuta nada

El sistema SHALL decidir, para cada turno, si la pregunta corresponde a una intención del catálogo con todos sus slots resueltos, y MUST NOT invocar ninguna API ni construir ninguna respuesta.

#### Scenario: Una pregunta cubierta con todos sus slots va al carril determinista

- **GIVEN** una pregunta que reconoce una intención y resuelve todos sus slots
- **WHEN** el enrutador la evalúa
- **THEN** devuelve esa intención con sus slots resueltos

#### Scenario: El enrutador no puede llamar a ningún módulo

- **GIVEN** el enrutador de dominio
- **WHEN** se leen sus dependencias
- **THEN** ninguna es un cliente de otro módulo ni el proveedor del modelo

### Requirement: El default es el carril SQL y nunca un error

El sistema SHALL continuar al carril SQL cuando no reconozca ninguna intención o cuando algún slot no resuelva, y MUST NOT fallar por no haber capturado.

#### Scenario: Una pregunta que no matchea ninguna intención sigue a SQL

- **GIVEN** una pregunta que ninguna intención del catálogo cubre
- **WHEN** el enrutador la evalúa
- **THEN** no devuelve intención
- **AND** no lanza ningún error

#### Scenario: Un slot sin resolver no enruta

- **GIVEN** una pregunta que reconoce una intención cuyo slot no resuelve a un valor único
- **WHEN** el enrutador la evalúa
- **THEN** no devuelve intención

#### Scenario: Una colisión de entidades llega al detector de ambigüedad

- **GIVEN** una pregunta cuyo término de slot corresponde a más de un valor
- **WHEN** el turno se resuelve
- **THEN** el enrutador no la captura
- **AND** el turno termina pidiendo una aclaración

### Requirement: La decisión no consume llamadas al modelo

El sistema MUST NOT emitir ninguna llamada al proveedor del modelo para decidir el carril.

#### Scenario: Decidir el carril no mueve el contador del turno

- **GIVEN** el contador de llamadas del turno
- **WHEN** el enrutador decide
- **THEN** el contador no cambia

### Requirement: Un banco de preguntas negativas protege al catálogo

El sistema SHALL verificar contra los datasets de evaluación que el enrutador no capture preguntas legítimas ajenas al catálogo, y el test SHALL fallar nombrando la pregunta capturada y la intención culpable.

#### Scenario: Ninguna pregunta del eje de capacidad se captura

- **GIVEN** las preguntas del dataset de capacidad
- **WHEN** el enrutador las evalúa
- **THEN** no captura ninguna

#### Scenario: Ninguna pregunta del eje de robustez se captura

- **GIVEN** las preguntas del dataset de robustez
- **WHEN** el enrutador las evalúa
- **THEN** no captura ninguna

#### Scenario: Una intención demasiado laxa hace fallar el banco

- **GIVEN** una intención que captura una pregunta del banco
- **WHEN** corre el test
- **THEN** falla nombrando la pregunta y la intención

### Requirement: La decisión se observa sin cambiar la respuesta

El sistema SHALL registrar qué intención habría enrutado, y el turno SHALL resolverse por el carril SQL igual que antes.

#### Scenario: Una pregunta capturada se registra y sigue a SQL

- **GIVEN** una pregunta que el enrutador captura
- **WHEN** el turno se resuelve
- **THEN** queda registrado qué intención la habría capturado
- **AND** la respuesta es la misma que sin el enrutador
