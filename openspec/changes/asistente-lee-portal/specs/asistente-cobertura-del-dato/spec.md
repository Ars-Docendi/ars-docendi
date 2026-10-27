## ADDED Requirements

### Requirement: Toda respuesta apoyada en portal declara su cobertura

El sistema SHALL declarar, en toda respuesta que se apoye en una tabla de portal, sobre cuántas personas del padrón existe ese dato.

El sistema MUST NOT afirmar que ninguna persona cumple una condición sin decir cuántas cargaron el dato consultado.

#### Scenario: El resultado vacío no se presenta como un hecho

- **GIVEN** una consulta sobre una tabla de portal que devuelve cero filas
- **WHEN** se arma la respuesta
- **THEN** el texto dice cuántas personas del padrón cargaron ese dato

#### Scenario: Con filas, el límite viaja al redactor

- **GIVEN** una consulta sobre portal que devuelve filas
- **WHEN** se arma el prompt de redacción
- **THEN** el prompt contiene la cobertura y la prohibición de presentarla como el total del Departamento

#### Scenario: Una consulta que no toca portal no declara nada

- **GIVEN** una consulta sobre designaciones
- **WHEN** se arma la respuesta
- **THEN** no se agrega ninguna declaración de cobertura y no se consulta nada de más

### Requirement: La cobertura se suma al aviso de alcance, no lo reemplaza

El sistema SHALL conservar el aviso de límite de alcance cuando corresponda, y agregar la cobertura además de él.

#### Scenario: Los dos límites conviven

- **GIVEN** un actor que no alcanza todo lo que la consulta tocó, sobre una tabla de portal vacía para él
- **WHEN** se arma el texto del resultado vacío
- **THEN** el texto menciona el límite de alcance y también la cobertura

### Requirement: La cobertura respeta el alcance del actor

El sistema SHALL calcular el numerador de la cobertura con la conexión de lectura del actor, sometida a RLS.

El sistema MUST NOT declarar una cobertura calculada sobre filas que el actor no puede ver.

#### Scenario: Sin permiso, el numerador es lo que el actor alcanza

- **GIVEN** un actor sin el permiso de trayectoria ajena
- **WHEN** se calcula la cobertura de una tabla de portal
- **THEN** el numerador cuenta solamente los perfiles que ese actor alcanza

### Requirement: Un fallo del conteo no interrumpe el turno

El sistema SHALL responder sin la declaración de cobertura si el conteo falla.

#### Scenario: El conteo falla y el turno responde igual

- **GIVEN** una consulta sobre portal donde el conteo de cobertura lanza una excepción
- **WHEN** se resuelve el turno
- **THEN** la respuesta se entrega sin la cobertura y sin error
