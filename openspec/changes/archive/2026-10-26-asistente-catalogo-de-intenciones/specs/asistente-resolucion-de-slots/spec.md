## ADDED Requirements

### Requirement: Los slots se resuelven contra la base

El sistema SHALL resolver los slots contra valores leídos de la base —el índice de entidades y los catálogos cerrados del dominio— y MUST NOT resolverlos contra listas escritas en el código o en el catálogo de intenciones.

#### Scenario: Un valor que existe en la base resuelve

- **GIVEN** una persona presente en la base
- **WHEN** una pregunta la nombra en un slot de clase persona
- **THEN** el slot resuelve a esa persona

#### Scenario: Un valor que no existe en la base no resuelve

- **GIVEN** un apellido que no está en la base
- **WHEN** una pregunta lo nombra en un slot de clase persona
- **THEN** el slot no resuelve

#### Scenario: Un estado nuevo en la base se reconoce sin tocar el código

- **GIVEN** la restricción que declara los estados del trámite
- **WHEN** se leen los valores admitidos
- **THEN** son exactamente los que declara la base
- **AND** ninguno está escrito en el código del módulo ni en el catálogo

### Requirement: El vocabulario del trámite se lee de su restricción, con forma exigida

El sistema SHALL derivar los valores admitidos de `estado`, `novedad` y `tipo_baja` de las restricciones que los declaran, y SHALL fallar de forma ruidosa cuando la restricción no tenga la forma esperada.

#### Scenario: La restricción no tiene la forma esperada

- **GIVEN** una restricción que no enumera literales
- **WHEN** se intenta derivar su vocabulario
- **THEN** falla nombrando la restricción
- **AND** no devuelve una lista vacía

#### Scenario: Los cargos se leen de su tabla

- **GIVEN** la tabla de cargos
- **WHEN** se cargan los valores del dominio
- **THEN** están los cargos activos, por su nombre y por su abreviatura

### Requirement: Un slot que resuelve a más de un valor no resuelve

El sistema MUST NOT resolver un slot cuyo término corresponda a más de un valor del dominio, y una intención con un slot sin resolver MUST NOT considerarse reconocida.

#### Scenario: Dos personas con el mismo apellido

- **GIVEN** dos personas que comparten apellido
- **WHEN** una pregunta nombra ese apellido en un slot de clase persona
- **THEN** el slot no resuelve
- **AND** la intención no queda reconocida

#### Scenario: Un apellido único sí resuelve

- **GIVEN** un apellido que corresponde a una sola persona
- **WHEN** una pregunta lo nombra
- **THEN** el slot resuelve a esa persona

### Requirement: Los catálogos se cargan perezosamente y se cachean

El sistema SHALL construir los catálogos del dominio en el primer uso y no durante el arranque, y SHALL reusar lo construido en los turnos siguientes.

#### Scenario: El ping responde con la base detenida

- **GIVEN** el Host levantado sin base disponible
- **WHEN** se consulta el endpoint de smoke test
- **THEN** responde correctamente

#### Scenario: Dos turnos leen la base una sola vez

- **GIVEN** un catálogo ya construido
- **WHEN** se resuelve un segundo turno
- **THEN** la cantidad de lecturas a la base no aumenta
