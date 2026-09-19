## ADDED Requirements

### Requirement: El carril social resuelve con cero tokens

El sistema SHALL resolver saludo, agradecimiento y meta-pregunta con respuestas fijas, sin llamar al modelo.

#### Scenario: Un saludo no llama al modelo

- **GIVEN** el mensaje «hola»
- **WHEN** se resuelve el turno
- **THEN** se responde sin ninguna llamada al modelo

#### Scenario: Un agradecimiento no llama al modelo

- **GIVEN** el mensaje «gracias»
- **WHEN** se resuelve el turno
- **THEN** se responde sin ninguna llamada al modelo

#### Scenario: La meta-pregunta no llama al modelo

- **GIVEN** una pregunta sobre qué puede hacer el asistente
- **WHEN** se resuelve el turno
- **THEN** se responde sin ninguna llamada al modelo

### Requirement: La guarda de precisión exige ausencia de contenido

El sistema SHALL clasificar como social únicamente cuando, quitada la apertura social, no quede ningún token de contenido.

#### Scenario: Un saludo con pregunta no se intercepta

- **GIVEN** el mensaje «hola, ¿cuántos docentes tiene Inglés Nivel IV?»
- **WHEN** se clasifica
- **THEN** no se intercepta y el turno sigue al carril de datos

#### Scenario: Un agradecimiento con pregunta no se intercepta

- **GIVEN** un mensaje que agradece y además pregunta algo del dominio
- **WHEN** se clasifica
- **THEN** no se intercepta

### Requirement: Una pregunta de dominio no es una meta-pregunta

El sistema MUST NOT clasificar como meta-pregunta una pregunta sobre datos del dominio.

#### Scenario: Una pregunta por carreras es de dominio

- **GIVEN** el mensaje «¿qué carreras hay?»
- **WHEN** se clasifica
- **THEN** no se intercepta como meta-pregunta

#### Scenario: Ningún ítem del dataset de capacidad se intercepta

- **GIVEN** las preguntas del dataset de capacidad
- **WHEN** se las clasifica
- **THEN** ninguna se intercepta

### Requirement: Con una aclaración pendiente el enrutador no corre

El sistema MUST saltear la clasificación social cuando hay una aclaración pendiente en el hilo.

#### Scenario: Un agradecimiento no le roba la respuesta a un menú abierto

- **GIVEN** un hilo con una aclaración pendiente
- **WHEN** llega un mensaje que sin aclaración sería un agradecimiento
- **THEN** el turno lo trata como respuesta a la aclaración

### Requirement: El enrutador es una clase pura

El sistema SHALL implementar la clasificación social sin acceso a base de datos ni a red.

#### Scenario: Se ejercita en memoria

- **WHEN** se prueban sus casos
- **THEN** corren sin base de datos y sin red
