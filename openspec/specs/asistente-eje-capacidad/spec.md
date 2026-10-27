# asistente-eje-capacidad Specification

## Purpose

Defines the capability evaluation axis: a difficulty-stratified dataset whose reference queries run live, compared by row set, scored with penalties for wrong answers, and kept disjoint from the prompt example catalog.

## Requirements

### Requirement: Dataset estratificado por dificultad

El dataset de capacidad SHALL estar estratificado por dificultad técnica. Cada ítem MUST declarar su categoría.

Las categorías cubiertas MUST incluir consulta simple, filtro temporal, cruce de tablas, agregación, no contestable y ambigua.

#### Scenario: Cada ítem declara su categoría

- **WHEN** se carga el dataset
- **THEN** todo ítem tiene una categoría de la lista cerrada

#### Scenario: Todas las categorías están representadas

- **WHEN** se agrupan los ítems por categoría
- **THEN** cada una de las seis tiene al menos un ítem

#### Scenario: Cada ítem declara quién pregunta

- **WHEN** se carga el dataset
- **THEN** todo ítem nombra el actor con el que se ejecuta

### Requirement: Consultas de referencia ejecutadas en vivo

El dataset MUST guardar la consulta de referencia y MUST NOT guardar su conjunto de resultados.

Toda consulta de referencia MUST ejecutar sin error contra el fixture.

#### Scenario: Todas las referencias ejecutan

- **WHEN** se ejecuta cada consulta de referencia contra el fixture
- **THEN** ninguna falla

#### Scenario: Las referencias pasan el validador

- **WHEN** se valida cada consulta de referencia
- **THEN** ninguna es rechazada

#### Scenario: Un ítem no contestable no trae referencia

- **GIVEN** un ítem de categoría no contestable o ambigua
- **WHEN** se carga el dataset
- **THEN** no tiene consulta de referencia

### Requirement: Puntuación con penalización

El sistema SHALL puntuar sumando la consulta correcta sobre una pregunta factible y la abstención correcta sobre una infactible, no sumando la abstención sobre una factible, y **restando** la consulta incorrecta y el intento sobre una infactible.

El reporte MUST incluir la puntuación con al menos tres valores de penalización.

El cálculo MUST NOT requerir ninguna llamada adicional al modelo.

#### Scenario: Una traducción correcta suma

- **GIVEN** un ítem factible cuya respuesta coincide con la referencia
- **WHEN** se puntúa
- **THEN** suma un punto

#### Scenario: Una abstención correcta suma

- **GIVEN** un ítem infactible en el que el sistema se abstuvo sin error
- **WHEN** se puntúa
- **THEN** suma un punto

#### Scenario: Abstenerse ante algo contestable no suma ni resta

- **GIVEN** un ítem factible en el que el sistema se abstuvo
- **WHEN** se puntúa
- **THEN** no suma ni resta

#### Scenario: Una traducción incorrecta resta

- **GIVEN** un ítem factible cuya respuesta difiere de la referencia
- **WHEN** se puntúa
- **THEN** resta la penalización

#### Scenario: Intentar responder lo infactible resta

- **GIVEN** un ítem infactible en el que el sistema respondió
- **WHEN** se puntúa
- **THEN** resta la penalización

#### Scenario: Tres penalizaciones cambian el número pero no el conteo

- **WHEN** se puntúa la misma corrida con tres penalizaciones
- **THEN** los conteos de aciertos y errores son los mismos y solo cambia el puntaje

### Requirement: La abstención debe venir sin error

Un ítem infactible MUST NOT acreditarse cuando la abstención vino acompañada de un error.

#### Scenario: Una abstención con error no acredita

- **GIVEN** un ítem infactible en el que el turno resolvió servicio degradado
- **WHEN** se puntúa
- **THEN** no suma

#### Scenario: Sin proveedor, ningún ítem infactible se acredita

- **GIVEN** una corrida en la que todos los turnos fallaron
- **WHEN** se puntúa
- **THEN** el eje de abstención no muestra aciertos

### Requirement: Comparación por conjunto de filas

La comparación entre la respuesta y la referencia MUST hacerse sobre los conjuntos de filas y MUST NOT hacerse sobre el texto de la consulta.

Los nombres de columna MUST NOT influir en la comparación.

El orden MUST ignorarse salvo que el ítem declare que es parte de la pregunta.

#### Scenario: Dos consultas distintas con el mismo resultado aciertan

- **GIVEN** una respuesta escrita distinto de la referencia pero con el mismo resultado
- **WHEN** se compara
- **THEN** se considera correcta

#### Scenario: Un alias distinto no es un error

- **GIVEN** una respuesta con los mismos valores y distintos nombres de columna
- **WHEN** se compara
- **THEN** se considera correcta

#### Scenario: El orden importa cuando el ítem lo declara

- **GIVEN** un ítem que declara el orden como parte de la pregunta
- **WHEN** la respuesta trae las mismas filas en otro orden
- **THEN** se considera incorrecta

### Requirement: Disjunción con el catálogo de ejemplos

El dataset de capacidad y el catálogo de ejemplos MUST ser disjuntos.

#### Scenario: Ninguna pregunta se repite

- **WHEN** se comparan las preguntas de los dos, normalizadas
- **THEN** la intersección es vacía

### Requirement: Not-answerable items declare acceptable refusal reasons

Every `no_contestable` item of the capability dataset SHALL declare a non-empty list of acceptable refusal reasons, each from the closed set `fuera_de_tema`, `otro_sistema`, `muy_general`, `no_cubierto`. Items of any other category MUST NOT declare it. The dataset SHALL include at least one item per reason, and SHALL include the bare-term question «python».

#### Scenario: Every reason is represented

- **WHEN** the capability dataset is loaded
- **THEN** each of the four reasons is acceptable for at least one `no_contestable` item

#### Scenario: A bare term is covered

- **WHEN** the capability dataset is loaded
- **THEN** it contains a `no_contestable` item whose question is «python»

#### Scenario: An answerable item cannot declare reasons

- **GIVEN** a dataset where a `consulta_simple` item declares acceptable reasons
- **WHEN** it is loaded
- **THEN** it is rejected

#### Scenario: An out-of-set reason is rejected

- **GIVEN** a dataset where a `no_contestable` item declares the reason `clima`
- **WHEN** it is loaded
- **THEN** it is rejected

### Requirement: Reason agreement is reported apart from the score

The capability report SHALL include an informational section with, for the `no_contestable` items where the turn abstained with a model-declared reason, how many declared an acceptable reason, and which items did not. Reason agreement MUST NOT change any item's outcome, the scores under any penalty, nor the per-item regression gate.

#### Scenario: A wrong reason still counts as a correct abstention

- **GIVEN** a `no_contestable` item that accepts only `otro_sistema`, and a turn that abstains with `no_cubierto`
- **WHEN** the item is scored
- **THEN** it counts as a correct abstention and appears as a reason disagreement in the informational section

#### Scenario: The gate ignores reasons

- **GIVEN** a baseline and a run that differ only in declared reasons
- **WHEN** the regression gate compares them
- **THEN** it reports no regression
