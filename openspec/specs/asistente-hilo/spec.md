# asistente-hilo Specification

## Purpose

Defines the conversation thread that backs follow-ups: it is resolved by identifier and owned by one actor, expires on inactivity, stores questions and the query behind each answer but never rows, and losing it degrades follow-ups without breaking the turn.

## Requirements

### Requirement: El hilo se resuelve por identificador y pertenece a un actor

El sistema SHALL resolver el hilo por su identificador y SHALL atarlo al actor que lo abrió.

Un intento de usar un hilo de otro actor MUST rechazarse.

#### Scenario: El actor que lo abrió lo recupera

- **GIVEN** un hilo abierto por un actor
- **WHEN** ese mismo actor lo pide por identificador
- **THEN** lo recupera con sus turnos

#### Scenario: Un hilo ajeno se rechaza

- **GIVEN** un hilo abierto por un actor
- **WHEN** otro actor lo pide por identificador
- **THEN** el sistema lo rechaza y no expone ningún turno

#### Scenario: Un identificador inexistente no rompe el turno

- **WHEN** se pide un hilo que no existe
- **THEN** el turno sigue como si fuera el primero, sin error

### Requirement: El hilo expira por inactividad

El sistema SHALL descartar el hilo después de un período de inactividad.

#### Scenario: Un hilo inactivo deja de resolverse

- **GIVEN** un hilo cuyo último turno superó el período de inactividad
- **WHEN** se lo pide
- **THEN** no se resuelve

#### Scenario: Un turno nuevo renueva la vigencia

- **GIVEN** un hilo próximo a expirar
- **WHEN** se le agrega un turno
- **THEN** vuelve a estar vigente

### Requirement: El hilo guarda preguntas y nunca filas

El sistema MUST NOT guardar en el hilo las filas devueltas por ninguna consulta.

#### Scenario: Los turnos del hilo no contienen datos de filas

- **GIVEN** un turno que devolvió filas con columnas sensibles
- **WHEN** se inspecciona el hilo
- **THEN** no contiene ningún valor de ninguna fila

### Requirement: El recorte ancla el inicio del segmento vigente

El sistema SHALL recortar el historial que entrega desde el inicio del segmento vigente, y MUST NOT anclarlo en el primer turno del hilo.

#### Scenario: Al mover el inicio de segmento, el historial vigente se acorta

- **GIVEN** un hilo con varios turnos
- **WHEN** se mueve el inicio de segmento al último
- **THEN** el historial vigente contiene solo los turnos desde ahí

#### Scenario: El historial vigente tiene un tope

- **GIVEN** un hilo con más turnos que el tope
- **WHEN** se pide el historial vigente
- **THEN** devuelve a lo sumo el tope, tomando los más recientes

### Requirement: Perder el hilo degrada el seguimiento pero no rompe el turno

El sistema SHALL responder una pregunta autocontenida aunque no exista hilo.

#### Scenario: Sin hilo, una pregunta autocontenida se responde igual

- **GIVEN** un turno sin identificador de hilo
- **WHEN** la pregunta es autocontenida
- **THEN** se responde normalmente

### Requirement: El hilo guarda la consulta que respondió cada turno

El sistema SHALL guardar, junto a la pregunta interpretada de cada turno, la consulta SQL que produjo la respuesta que el usuario vio.

El sistema MUST NOT guardar ninguna fila leída de la base. La consulta se guarda porque sus literales provienen de la pregunta del usuario, que el hilo ya guardaba; no introduce en el hilo ninguna clase de dato nueva.

Un turno que terminó sin filas, en abstención o en servicio degradado SHALL guardar la pregunta sin consulta.

Con reintento, el sistema SHALL guardar la consulta que devolvió filas y MUST NOT guardar la que volvió vacía.

#### Scenario: Un turno respondido anota su consulta

- **GIVEN** un turno que devolvió filas
- **WHEN** se agrega al hilo
- **THEN** el hilo guarda la pregunta interpretada y la consulta ejecutada

#### Scenario: Un turno sin filas no anota consulta

- **GIVEN** un turno cuya consulta no devolvió ninguna fila
- **WHEN** se agrega al hilo
- **THEN** el hilo guarda la pregunta y no guarda ninguna consulta

#### Scenario: Con reintento se anota la consulta que respondió

- **GIVEN** un turno cuya primera consulta volvió vacía y cuya segunda devolvió filas
- **WHEN** se agrega al hilo
- **THEN** el hilo guarda la segunda consulta, no la primera

#### Scenario: El hilo nunca guarda filas

- **GIVEN** cualquier turno, con o sin datos personales en su resultado
- **WHEN** se inspecciona el hilo
- **THEN** no contiene ningún valor proveniente de una fila del resultado
