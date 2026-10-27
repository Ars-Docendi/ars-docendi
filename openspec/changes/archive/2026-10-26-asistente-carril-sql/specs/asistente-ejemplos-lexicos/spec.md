## ADDED Requirements

### Requirement: Catálogo versionado de ejemplos

El sistema SHALL mantener un catálogo versionado de pares pregunta-consulta. Cada ejemplo MUST declarar su pregunta en español, su consulta SQL y la categoría de dificultad que ilustra.

Toda consulta del catálogo MUST ejecutar sin error contra la base.

#### Scenario: Todas las consultas del catálogo ejecutan

- **WHEN** se ejecuta cada consulta del catálogo contra una base con el esquema vigente
- **THEN** ninguna falla

#### Scenario: El catálogo no contiene reloj

- **WHEN** se valida cada consulta del catálogo con el validador de SQL
- **THEN** ninguna es rechazada

### Requirement: Selección por similitud léxica sin red

El sistema SHALL elegir los ejemplos más parecidos a la pregunta del turno por similitud léxica, normalizando sin acentos y descartando palabras vacías del español.

La selección MUST resolverse en proceso. MUST NOT hacer ninguna llamada de red ni ninguna llamada al modelo.

#### Scenario: Se eligen los ejemplos parecidos

- **GIVEN** un catálogo con ejemplos de varias familias de preguntas
- **WHEN** se pide la selección para una pregunta sobre cobertura de cátedra
- **THEN** los ejemplos elegidos son los de esa familia

#### Scenario: Los acentos no cambian la selección

- **GIVEN** dos formulaciones de la misma pregunta que difieren solo en acentos y mayúsculas
- **WHEN** se piden las selecciones
- **THEN** son iguales

#### Scenario: Las palabras vacías no dominan

- **GIVEN** dos ejemplos, uno que comparte con la pregunta solo palabras vacías y otro que comparte un término del dominio
- **WHEN** se pide la selección
- **THEN** se elige el segundo

#### Scenario: La selección no cuesta llamadas al modelo

- **GIVEN** un turno con techo de llamadas al modelo agotado
- **WHEN** se pide la selección de ejemplos
- **THEN** devuelve los ejemplos sin lanzar la excepción de techo superado

#### Scenario: Una pregunta sin parentesco no arrastra ejemplos irrelevantes

- **GIVEN** una pregunta que no comparte ningún término del dominio con el catálogo
- **WHEN** se pide la selección
- **THEN** devuelve una selección vacía en lugar de los ejemplos menos malos

### Requirement: Los ejemplos viajan en el prompt de usuario

Los ejemplos seleccionados MUST inyectarse en el prompt de usuario de la llamada de generación. MUST NOT formar parte del prefijo de sistema.

#### Scenario: El prefijo no cambia con los ejemplos

- **GIVEN** dos turnos cuyas preguntas seleccionan ejemplos distintos
- **WHEN** se comparan los prefijos de sistema de sus llamadas de generación
- **THEN** son idénticos

#### Scenario: Los ejemplos llegan al mensaje

- **GIVEN** un turno cuya pregunta selecciona al menos un ejemplo
- **WHEN** se inspecciona el prompt de usuario de la llamada de generación
- **THEN** contiene la pregunta y la consulta de ese ejemplo

### Requirement: Huella del catálogo

El sistema SHALL exponer una huella estable del catálogo de ejemplos, para el sellado de reportes de evaluación.

#### Scenario: La huella cambia al agregar un ejemplo

- **GIVEN** la huella del catálogo vigente
- **WHEN** se agrega un ejemplo y se recalcula
- **THEN** la huella es distinta

### Requirement: Disjunción con el dataset de evaluación

El catálogo de ejemplos y el dataset de capacidad MUST ser disjuntos: ninguna pregunta del catálogo puede aparecer en el dataset.

> La verificación mecánica de este requisito se implementa junto con el dataset, en el cambio de evaluación. Hasta entonces la disjunción es una convención escrita.

#### Scenario: Ninguna pregunta se repite

- **WHEN** se comparan las preguntas del catálogo con las del dataset de capacidad, normalizadas
- **THEN** la intersección es vacía
