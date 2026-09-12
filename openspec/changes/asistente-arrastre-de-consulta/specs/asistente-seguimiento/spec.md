## ADDED Requirements

### Requirement: El seguimiento se resuelve editando o anidando la consulta anterior

El sistema SHALL ofrecerle al generador las consultas de los turnos del segmento vigente, dentro del tope de historial, cuando el turno es un seguimiento.

El sistema SHALL instruir al modelo para que edite o anide esa consulta en lugar de rehacerla desde cero.

Las consultas arrastradas SHALL viajar en la parte variable del prompt y MUST NOT formar parte del prefijo estable, cuya huella sella los reportes de evaluación.

#### Scenario: Una referencia a un resultado anterior se resuelve

- **GIVEN** un turno anterior que devolvió las materias de una carrera
- **WHEN** el usuario pregunta por los profesores «de esa materia»
- **THEN** la generación recibe la consulta del turno anterior y la respuesta devuelve filas

#### Scenario: Un primer turno no arrastra nada

- **GIVEN** un hilo sin turnos previos
- **WHEN** se arma el mensaje de generación
- **THEN** no contiene ninguna consulta anterior

#### Scenario: El prefijo estable no cambia entre turnos de un mismo hilo

- **GIVEN** dos turnos consecutivos del mismo hilo, el segundo con consulta arrastrada
- **WHEN** se comparan las huellas del prefijo de las dos llamadas de generación
- **THEN** son idénticas

### Requirement: El arrastre respeta el segmento y el tope del historial

El sistema SHALL arrastrar únicamente las consultas de los turnos del segmento vigente, con el mismo recorte que ya aplica a las preguntas.

Al soltarse el tema, el sistema MUST NOT arrastrar ninguna consulta de los turnos anteriores al pivote.

#### Scenario: Un pivote suelta las consultas

- **GIVEN** un hilo con turnos respondidos y un mensaje que el detector marca como cambio de tema
- **WHEN** se arma el mensaje de generación
- **THEN** no lleva ninguna consulta de los turnos anteriores al pivote

#### Scenario: El tope acota cuántas consultas viajan

- **GIVEN** un segmento con más turnos respondidos que el tope del historial
- **WHEN** se arma el mensaje de generación
- **THEN** lleva a lo sumo tantas consultas como el tope permite, las más recientes
