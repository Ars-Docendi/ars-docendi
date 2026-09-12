## ADDED Requirements

### Requirement: El catálogo de intenciones es declarativo y cerrado

El sistema SHALL declarar las intenciones del carril determinista en un único recurso declarativo, y MUST NOT reconocer ninguna intención que no esté en él.

#### Scenario: Cada intención declara términos, slots y destino

- **GIVEN** el catálogo de intenciones
- **WHEN** se carga
- **THEN** cada intención tiene un nombre, un conjunto de términos, la lista de slots que exige y un destino lógico
- **AND** ninguno de esos campos está vacío

#### Scenario: Una intención con una clase de slot inexistente no carga

- **GIVEN** una intención que exige un slot de una clase que el resolutor no conoce
- **WHEN** se carga el catálogo
- **THEN** la carga falla nombrando la intención y la clase

#### Scenario: Los términos del catálogo están normalizados

- **GIVEN** el catálogo de intenciones
- **WHEN** se comparan sus términos con su propia forma normalizada
- **THEN** son iguales
- **AND** una intención con un término acentuado o en mayúscula no carga

### Requirement: El reconocimiento es por conjunto de términos y no depende del orden

El sistema SHALL reconocer una intención cuando todos sus términos aparecen en la pregunta normalizada, con independencia del orden en que aparezcan.

#### Scenario: La misma pregunta con otro orden reconoce la misma intención

- **GIVEN** una pregunta que reconoce una intención
- **WHEN** se reordenan sus palabras sin quitar ninguna
- **THEN** se reconoce la misma intención

#### Scenario: Falta un término y no hay intención

- **GIVEN** una pregunta a la que le falta uno de los términos de toda intención del catálogo
- **WHEN** se la evalúa
- **THEN** no se reconoce ninguna intención

### Requirement: El reconocimiento no llama al modelo

El sistema MUST NOT emitir ninguna llamada al proveedor del modelo para reconocer una intención ni para resolver sus slots.

#### Scenario: Un turno que reconoce una intención no consume llamadas

- **GIVEN** el contador de llamadas del turno en cero
- **WHEN** se reconoce una intención y se resuelven sus slots
- **THEN** el contador sigue en cero

### Requirement: El catálogo nombra un destino y no lo invoca

El sistema SHALL declarar el destino de cada intención como un identificador lógico, y el módulo MUST NOT adquirir ninguna referencia a otro módulo por causa del catálogo.

#### Scenario: El módulo sigue sin referenciar otros módulos

- **GIVEN** el proyecto del módulo del asistente
- **WHEN** se leen sus referencias de proyecto
- **THEN** la única es `ArsDocendi.Shared`

### Requirement: Toda intención del catálogo tiene caso de prueba

El sistema SHALL cubrir cada intención del catálogo con al menos un caso que la reconoce con sus slots resueltos y otro que la deja sin resolver.

#### Scenario: Una intención sin cobertura hace fallar la suite

- **GIVEN** el catálogo de intenciones
- **WHEN** se compara con los casos de prueba declarados
- **THEN** falla nombrando la intención que no tiene caso
