## ADDED Requirements

### Requirement: La ambigüedad se detecta con una consulta, no con el modelo

El sistema SHALL detectar las colisiones de valores consultando la base, y MUST NOT usar el modelo para decidir si una pregunta es ambigua.

El índice de colisiones SHALL cargarse de la base y cachearse. MUST NOT contener valores embebidos en el código.

#### Scenario: Una materia repetida entre carreras devuelve las opciones

- **GIVEN** una pregunta que nombra una materia que existe en varias carreras
- **WHEN** se resuelve el turno
- **THEN** devuelve las carreras como opciones, sin llamar al modelo

#### Scenario: Un apellido compartido devuelve las personas

- **GIVEN** una pregunta que nombra un apellido compartido por varias personas
- **WHEN** se resuelve el turno
- **THEN** devuelve las personas con su nombre completo, sin llamar al modelo

#### Scenario: Con el discriminador presente no se pregunta

- **GIVEN** una pregunta que nombra la materia y también su carrera
- **WHEN** se resuelve el turno
- **THEN** no se pide aclaración

#### Scenario: Un valor sin colisión no dispara

- **GIVEN** una pregunta que nombra una materia que existe en una sola carrera
- **WHEN** se resuelve el turno
- **THEN** no se pide aclaración

### Requirement: El detector no se extiende a la vaguedad

El sistema MUST pedir aclaración únicamente ante una colisión verificada por consulta, y MUST NOT pedirla porque la pregunta parezca vaga.

#### Scenario: Una pregunta vaga sin colisión no pide aclaración

- **GIVEN** una pregunta general que no nombra ningún valor en colisión
- **WHEN** se resuelve el turno
- **THEN** no se pide aclaración

### Requirement: El fixture reproduce las colisiones

El sistema SHALL verificar que el fixture de evaluación contiene las colisiones que el detector necesita.

Sin ellas el detector no dispara, y los diálogos que lo prueban darían verde sin medir nada.

#### Scenario: El fixture tiene materias compartidas y apellidos compartidos

- **WHEN** se inspecciona el generador del fixture
- **THEN** declara al menos una materia en varias carreras y al menos un apellido en varias personas

### Requirement: La respuesta a una aclaración se reconoce en tres pasos

El sistema SHALL reconocer la opción elegida por etiqueta completa, luego por token distintivo, y luego por ordinal.

El reconocimiento MUST resolverse sin llamar al modelo.

#### Scenario: La etiqueta completa se reconoce

- **GIVEN** una aclaración pendiente con opciones
- **WHEN** el usuario responde con la etiqueta completa de una
- **THEN** se reconoce esa opción

#### Scenario: Un token distintivo se reconoce

- **GIVEN** una aclaración pendiente con opciones que difieren en una palabra
- **WHEN** el usuario responde solo esa palabra
- **THEN** se reconoce esa opción

#### Scenario: El ordinal se reconoce

- **GIVEN** una aclaración pendiente con varias opciones
- **WHEN** el usuario responde con el número de una
- **THEN** se reconoce esa opción

### Requirement: Una respuesta ambigua no se resuelve al azar

El sistema MUST volver a preguntar cuando la respuesta del usuario empata con más de una opción.

#### Scenario: Un empate vuelve a preguntar

- **GIVEN** una aclaración pendiente cuyas opciones comparten un token
- **WHEN** el usuario responde ese token compartido
- **THEN** el sistema vuelve a ofrecer el menú y no elige ninguna

### Requirement: Los intentos tienen tope y salida

El sistema SHALL limitar cuántas veces se reofrece el menú y SHALL abandonar la aclaración al agotarse.

#### Scenario: Al agotar los intentos la aclaración se abandona

- **GIVEN** una aclaración pendiente que agotó sus intentos
- **WHEN** llega otra respuesta que no se reconoce
- **THEN** la aclaración se abandona y el turno lo dice, sin quedar pendiente

### Requirement: El reconocedor entrega la etiqueta canónica

El sistema SHALL entregar a las etapas siguientes la etiqueta exacta de la opción elegida, y MUST NOT entregar el texto que escribió el usuario.

#### Scenario: Un ordinal se convierte en etiqueta antes de seguir

- **GIVEN** una aclaración pendiente
- **WHEN** el usuario responde con un ordinal
- **THEN** la pregunta que sigue al reescritor lleva la etiqueta de la opción, no el ordinal
