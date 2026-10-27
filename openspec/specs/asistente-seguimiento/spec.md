# asistente-seguimiento Specification

## Purpose

Defines follow-up handling: the rewriter runs only with history, decides field by field, resets history on a topic change, is protected by anaphoric markers, shows the user any pivot, and resolves follow-ups by editing or nesting the previous query.

## Requirements

### Requirement: El reescritor solo corre con historial

El sistema MUST NOT llamar al modelo para reescribir cuando el historial vigente está vacío.

#### Scenario: Un primer turno no paga la reescritura

- **GIVEN** un turno sin historial
- **WHEN** se resuelve
- **THEN** no se hace ninguna llamada de reescritura

#### Scenario: Un seguimiento sí la paga

- **GIVEN** un turno con historial vigente
- **WHEN** se resuelve
- **THEN** se hace una llamada de reescritura

### Requirement: La regla de reescritura decide campo por campo

El sistema SHALL instruir la decisión de arrastrar o soltar **por cada campo del dominio**, y MUST NOT instruir que se conserve todo lo vigente sin condición.

El prompt SHALL incluir un ejemplo que reescribe y un ejemplo que descarta el historial.

#### Scenario: La regla enumera los campos

- **WHEN** se inspecciona el prompt de reescritura
- **THEN** nombra los campos del dominio uno por uno

#### Scenario: El prompt trae los dos ejemplos

- **WHEN** se inspecciona el prompt de reescritura
- **THEN** contiene un ejemplo que arrastra y uno que descarta

#### Scenario: El ejemplo de descarte no replica el dataset

- **WHEN** se comparan los ejemplos del prompt con las preguntas del dataset de capacidad
- **THEN** ninguno coincide con un ítem del dataset

### Requirement: El cambio de tema fuerza historial vacío

Al detectar un cambio de tema, el sistema MUST llamar al reescritor sin historial.

El sistema MUST NOT limitarse a instruirle al modelo que ignore el historial.

#### Scenario: En el pivote no se manda historial

- **GIVEN** un hilo con turnos y un mensaje que cambia de tema
- **WHEN** se resuelve el turno
- **THEN** lo que se le manda al modelo no contiene ningún turno anterior

#### Scenario: El inicio de segmento se mueve

- **GIVEN** un hilo con turnos y un mensaje que cambia de tema
- **WHEN** se resuelve el turno
- **THEN** el historial vigente del hilo arranca en el turno del pivote

### Requirement: El marcador anafórico protege el seguimiento

El sistema MUST NOT marcar cambio de tema cuando el mensaje contiene un marcador anafórico.

#### Scenario: El seguimiento canónico no se rompe

- **GIVEN** un hilo con turnos y el mensaje «¿y en Sistemas?»
- **WHEN** se evalúa el cambio de tema
- **THEN** no se lo marca como pivote

#### Scenario: Otra entidad sin anáfora sí lo marca

- **GIVEN** un hilo sobre una materia y un mensaje sobre otra entidad, sin ninguna anáfora
- **WHEN** se evalúa el cambio de tema
- **THEN** se lo marca como pivote

#### Scenario: Sin historial no hay pivote

- **GIVEN** un hilo sin turnos
- **WHEN** se evalúa el cambio de tema
- **THEN** no se lo marca

### Requirement: El pivote se le muestra al usuario

En el turno de pivote el sistema SHALL devolver la pregunta interpretada, para que el usuario vea que se soltó el tema anterior.

#### Scenario: El turno de pivote expone la pregunta interpretada

- **GIVEN** un turno marcado como pivote
- **WHEN** termina
- **THEN** el resultado incluye la pregunta interpretada

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
