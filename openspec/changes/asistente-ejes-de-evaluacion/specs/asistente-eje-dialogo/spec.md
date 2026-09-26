## ADDED Requirements

### Requirement: El eje de diálogo corre conversaciones de varios turnos

El sistema SHALL ejecutar los turnos de un diálogo en orden, sobre el mismo hilo conversacional.

#### Scenario: Los turnos comparten hilo

- **GIVEN** un diálogo de tres turnos
- **WHEN** se lo corre
- **THEN** los tres se resuelven sobre el mismo hilo

#### Scenario: Un turno que falla no invalida los anteriores

- **GIVEN** un diálogo cuyo segundo turno falla
- **WHEN** termina la corrida
- **THEN** el primero conserva su desenlace y el diálogo informa dónde se cortó

### Requirement: Un turno puede declarar términos prohibidos en la pregunta interpretada

El sistema SHALL verificar que los términos prohibidos de un turno no aparezcan en su pregunta interpretada, y SHALL contar su presencia como fallo de ese turno.

#### Scenario: El arrastre se detecta

- **GIVEN** un turno que prohíbe un término del turno anterior
- **WHEN** la pregunta interpretada lo contiene
- **THEN** el turno se cuenta como incorrecto

#### Scenario: Sin arrastre el turno se evalúa normalmente

- **GIVEN** un turno con términos prohibidos ausentes de la pregunta interpretada
- **WHEN** se lo evalúa
- **THEN** se lo puntúa por su resultado

#### Scenario: El chequeo es sensible

- **GIVEN** una pregunta interpretada construida para arrastrar un término prohibido
- **WHEN** se la verifica
- **THEN** el chequeo la rechaza

### Requirement: Existe un diálogo de pivote duro

El dataset de diálogo SHALL incluir al menos una conversación cuyo segundo turno cambie de entidad sin usar ninguna referencia anafórica y prohíba los términos del primero.

#### Scenario: El pivote duro está en el dataset

- **GIVEN** el dataset de diálogo
- **WHEN** se lo inspecciona
- **THEN** hay al menos un diálogo marcado como pivote duro

#### Scenario: El pivote duro prohíbe los términos del turno anterior

- **GIVEN** un diálogo de pivote duro
- **WHEN** se inspecciona su segundo turno
- **THEN** declara términos prohibidos y su pregunta no contiene marcadores anafóricos
