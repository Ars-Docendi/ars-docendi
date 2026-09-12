## ADDED Requirements

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
