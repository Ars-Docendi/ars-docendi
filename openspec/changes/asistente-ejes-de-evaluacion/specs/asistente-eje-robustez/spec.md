## ADDED Requirements

### Requirement: Cada ítem de robustez hereda la consulta de su origen

El sistema SHALL derivar la consulta de referencia de un ítem de robustez del ítem de capacidad que declara como origen, y MUST NOT permitir que un ítem de robustez declare una consulta propia.

#### Scenario: La consulta es la misma que la del origen

- **GIVEN** un ítem de robustez que declara un origen
- **WHEN** se carga el dataset
- **THEN** su consulta de referencia es exactamente la del ítem de origen

#### Scenario: Un origen inexistente se rechaza al cargar

- **GIVEN** un ítem de robustez cuyo origen no está en el dataset de capacidad
- **WHEN** se carga el dataset
- **THEN** la carga falla nombrando el origen que falta

#### Scenario: Un ítem que declara consulta propia se rechaza

- **GIVEN** un ítem de robustez con una consulta escrita en el archivo
- **WHEN** se carga el dataset
- **THEN** la carga falla

### Requirement: El ítem de robustez cambia el fraseo y nada más

El sistema SHALL heredar del origen la categoría, el actor y el criterio de orden, y SHALL tomar del ítem solo la pregunta perturbada y su clase de perturbación.

#### Scenario: El actor y la categoría vienen del origen

- **GIVEN** un ítem de robustez
- **WHEN** se lo compara con su origen
- **THEN** comparten actor, categoría y criterio de orden

#### Scenario: La pregunta difiere de la del origen

- **GIVEN** un ítem de robustez
- **WHEN** se compara su pregunta con la del origen
- **THEN** son distintas

### Requirement: Las clases de perturbación son cerradas

El sistema SHALL aceptar únicamente las clases de perturbación declaradas, y SHALL rechazar el dataset que use otra.

#### Scenario: Una clase desconocida se rechaza

- **GIVEN** un ítem con una clase de perturbación que no está en la lista
- **WHEN** se carga el dataset
- **THEN** la carga falla nombrando la clase

#### Scenario: El reporte agrupa por clase de perturbación

- **GIVEN** una corrida del eje de robustez
- **WHEN** se lee el reporte
- **THEN** los conteos están desagregados por clase
