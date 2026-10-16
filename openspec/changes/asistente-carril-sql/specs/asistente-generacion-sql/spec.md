## ADDED Requirements

### Requirement: Generación de SQL con temperatura cero

El sistema SHALL pedir la traducción de la pregunta a SQL en una única llamada al modelo, con temperatura cero, usando el prefijo de sistema sin modificarlo.

La respuesta MUST interpretarse como un objeto con: si la pregunta es contestable, la consulta, el razonamiento y una categoría estimada.

#### Scenario: La llamada usa temperatura cero

- **WHEN** se ejecuta la generación de un turno
- **THEN** la solicitud al modelo lleva temperatura cero

#### Scenario: El prefijo llega sin modificar

- **WHEN** se ejecuta la generación de un turno
- **THEN** el prefijo de sistema de la solicitud es idéntico al que expone el proveedor de esquema

#### Scenario: Se interpreta una respuesta envuelta en delimitadores de código

- **GIVEN** una respuesta del modelo con el objeto envuelto en delimitadores de bloque de código
- **WHEN** se interpreta
- **THEN** se obtienen igual la consulta y el razonamiento

#### Scenario: Una respuesta ininteligible no se adivina

- **GIVEN** una respuesta del modelo que no se puede interpretar como el objeto esperado
- **WHEN** se interpreta
- **THEN** el turno resulta no contestable, y no se intenta extraer una consulta del texto

### Requirement: Fecha de referencia como parámetro del turno

La fecha de referencia MUST resolverse una vez por turno e inyectarse en el prompt de **usuario**. MUST ser inyectable desde afuera.

La consulta generada MUST NOT depender del reloj de la base.

#### Scenario: La fecha viaja en el mensaje

- **GIVEN** un turno con una fecha de referencia fija
- **WHEN** se inspecciona el prompt de usuario de la generación
- **THEN** contiene esa fecha

#### Scenario: La fecha no viaja en el prefijo

- **WHEN** se inspecciona el prefijo de sistema
- **THEN** no contiene ninguna fecha de referencia

#### Scenario: Con la misma fecha, la generación es reproducible

- **GIVEN** la misma pregunta, el mismo esquema y la misma fecha inyectada
- **WHEN** se genera dos veces
- **THEN** la solicitud al modelo es idéntica en las dos

#### Scenario: La fecha real y la fija son intercambiables

- **GIVEN** la implementación de fecha fija usada en evaluación
- **WHEN** se sustituye por la implementación real
- **THEN** el resto del carril no cambia

### Requirement: Una pregunta no contestable corta el turno

Cuando la generación declara que la pregunta no es contestable, el turno MUST terminar sin producir consulta, sin ejecutar nada y **sin hacer la segunda llamada al modelo**.

#### Scenario: No hay segunda llamada

- **GIVEN** una generación que declara la pregunta no contestable
- **WHEN** termina el turno
- **THEN** se consumió exactamente una llamada al modelo

#### Scenario: No se ejecuta nada

- **GIVEN** una generación que declara la pregunta no contestable
- **WHEN** termina el turno
- **THEN** no se abrió ninguna conexión de solo lectura

### Requirement: El razonamiento llega a la respuesta

El razonamiento devuelto por la generación MUST interpretarse y MUST llegar al resultado del turno, en lugar de descartarse.

#### Scenario: El razonamiento sobrevive al turno

- **GIVEN** una generación que devuelve un razonamiento no vacío
- **WHEN** el turno termina con una respuesta
- **THEN** el resultado incluye ese razonamiento

#### Scenario: El razonamiento sobrevive a la abstención

- **GIVEN** una generación que declara la pregunta no contestable y devuelve un razonamiento
- **WHEN** el turno termina
- **THEN** el resultado incluye ese razonamiento
