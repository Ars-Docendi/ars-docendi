## ADDED Requirements

### Requirement: El turno lleva la decisión del enrutador sombra hasta el registro

El sistema SHALL incluir, en lo que cada turno deja para registrar, el nombre de la intención que el enrutador de dominio eligió, y SHALL usar nulo cuando el enrutador no capturó la pregunta. No capturar MUST NOT tratarse como un dato faltante ni como un error.

#### Scenario: Un turno capturado por el catálogo lleva el nombre de la intención

- **GIVEN** una pregunta que el enrutador de dominio captura con todos sus slots resueltos
- **WHEN** el turno se registra
- **THEN** lo registrado lleva el nombre de esa intención del catálogo

#### Scenario: Un turno que el catálogo no captura lleva nulo

- **GIVEN** una pregunta que ninguna intención del catálogo cubre
- **WHEN** el turno se registra
- **THEN** lo registrado lleva nulo como intención
- **AND** el turno se resuelve sin ningún error

#### Scenario: Un turno que no llegó al enrutador lleva nulo

- **GIVEN** un turno que termina antes del paso del enrutador, como un saludo o una meta-pregunta
- **WHEN** el turno se registra
- **THEN** lo registrado lleva nulo como intención

#### Scenario: Un turno que se cae después de decidir conserva la decisión

- **GIVEN** una pregunta que el enrutador captura y un turno que después termina en una excepción no prevista
- **WHEN** se escribe la fila del fallo
- **THEN** conserva el nombre de la intención que alcanzó a decidirse

### Requirement: El registro operativo persiste la intención sombra en columna propia

El sistema SHALL persistir la intención sombra en `asistente.registro_operativo.intencion_sombra`, de tipo `text` y anulable. La columna MUST NOT tener valor por omisión, y MUST llevar un comentario de esquema que la distinga de `carril`.

#### Scenario: La columna existe, es texto y admite nulo

- **GIVEN** una base con el DDL del asistente aplicado
- **WHEN** se inspecciona `asistente.registro_operativo`
- **THEN** existe la columna `intencion_sombra` de tipo `text`
- **AND** admite nulo
- **AND** no declara valor por omisión

#### Scenario: Un turno capturado deja su intención en la fila operativa

- **GIVEN** una pregunta que el enrutador captura
- **WHEN** el turno termina
- **THEN** la fila del registro operativo tiene esa intención en `intencion_sombra`

#### Scenario: El DDL del asistente sigue sin alterar nada

- **GIVEN** los archivos de `database/asistente/`
- **WHEN** se los inspecciona
- **THEN** ninguno contiene `ALTER TABLE` ni `DROP`

### Requirement: La intención sombra no llega al registro analítico

El sistema MUST NOT escribir la intención sombra en `asistente.registro_analitico`, y esa tabla MUST NOT tener columna para ella. La desvinculación de los dos registros se sostiene por la ausencia de columnas con las que cruzarlos, no por una convención de escritura.

#### Scenario: La tabla analítica no tiene columna para la intención

- **GIVEN** una base con el DDL del asistente aplicado
- **WHEN** se inspeccionan las columnas de `asistente.registro_analitico`
- **THEN** ninguna corresponde a la intención sombra

#### Scenario: Un turno capturado no deja rastro de la intención en el analítico

- **GIVEN** una pregunta que el enrutador captura
- **WHEN** el turno termina
- **THEN** la fila del registro analítico conserva solo pregunta, categoría, estado y día

### Requirement: `carril` conserva el significado de la ruta real del turno

El sistema SHALL seguir derivando `carril` de cómo se resolvió el turno realmente, y MUST NOT derivarlo de la decisión del enrutador sombra. Un turno capturado por el catálogo SHALL registrar el carril por el que efectivamente se resolvió.

#### Scenario: Un turno capturado que responde por SQL registra el carril SQL

- **GIVEN** una pregunta que el enrutador captura y que se resuelve generando y ejecutando una consulta
- **WHEN** el turno se registra
- **THEN** `carril` es el del carril SQL
- **AND** `intencion_sombra` lleva el nombre de la intención

#### Scenario: Un turno capturado que termina pidiendo aclaración registra ese carril

- **GIVEN** un turno que el enrutador capturó y que termina pidiendo una aclaración
- **WHEN** el turno se registra
- **THEN** `carril` es el de aclaración y no el del carril determinista

#### Scenario: La respuesta al usuario no cambia por haber capturado

- **GIVEN** una pregunta que el enrutador captura
- **WHEN** el turno se resuelve
- **THEN** la respuesta es la misma que sin el enrutador

### Requirement: Una tabla dorada fija qué captura el enrutador sobre los datasets de evaluación

El sistema SHALL verificar el mapeo de cada ítem de `capacidad.json` y `robustez.json` a la intención que el enrutador captura —o a nulo— contra una tabla dorada versionada, y el test SHALL fallar cuando el mapeo observado difiera del fijado. La verificación MUST NOT consumir ninguna llamada al proveedor del modelo, y la tabla dorada MUST NOT regenerarse como efecto de correr el test.

#### Scenario: El mapeo observado coincide con la tabla dorada

- **GIVEN** la tabla dorada vigente y los dos datasets de evaluación
- **WHEN** el enrutador evalúa cada pregunta
- **THEN** cada ítem captura lo que la tabla dorada fija

#### Scenario: Una intención más laxa captura un ítem que estaba en nulo

- **GIVEN** una intención del catálogo que empieza a capturar un ítem fijado en nulo
- **WHEN** corre el test
- **THEN** falla nombrando el ítem, la intención que lo capturó y la dirección del cambio

#### Scenario: Un ítem deja de capturarse

- **GIVEN** un ítem que la tabla dorada fija con una intención y que deja de capturarse
- **WHEN** corre el test
- **THEN** falla nombrando el ítem y la captura perdida

#### Scenario: La tabla dorada cubre todos los ítems de los dos datasets

- **GIVEN** los ítems de `capacidad.json` y de `robustez.json`
- **WHEN** corre el test
- **THEN** falla si algún ítem no tiene entrada en la tabla dorada
- **AND** falla si la tabla dorada tiene una entrada que ningún ítem reclama

#### Scenario: Fijar el mapeo no cuesta llamadas al modelo

- **GIVEN** el contador de llamadas del turno
- **WHEN** corre la verificación completa de la tabla dorada
- **THEN** el contador no cambia

### Requirement: La tabla dorada mide cobertura y consistencia de fraseo

El sistema SHALL derivar de la tabla dorada la cobertura del catálogo sobre el corpus y la consistencia entre cada paráfrasis de `robustez.json` y su ítem de origen en `capacidad.json`. Una paráfrasis y su origen SHALL resolver a la misma decisión, y una divergencia SHALL reportarse como inconsistencia del enrutador. El test MUST NOT afirmar que la intención capturada sea la correcta para la pregunta.

#### Scenario: Una paráfrasis decide lo mismo que su origen

- **GIVEN** un ítem de `robustez.json` con su `origen` en `capacidad.json`
- **WHEN** el enrutador evalúa los dos
- **THEN** ambos capturan la misma intención, o ninguno captura

#### Scenario: Una paráfrasis que diverge de su origen se reporta

- **GIVEN** un ítem de robustez que captura una intención distinta de la de su origen
- **WHEN** corre el test
- **THEN** falla nombrando los dos ítems y las dos decisiones

#### Scenario: Un origen inexistente hace fallar el test

- **GIVEN** un ítem de robustez cuyo `origen` no corresponde a ningún ítem de capacidad
- **WHEN** corre el test
- **THEN** falla nombrando el ítem y el origen que no existe

### Requirement: El README del módulo documenta la consulta que produce el número

El sistema SHALL documentar en el README de `Modules.Asistente` la consulta ejecutable que calcula la cobertura del carril determinista sobre tráfico real desde el registro operativo, junto con la advertencia de que `carril` e `intencion_sombra` responden preguntas distintas.

#### Scenario: La consulta está escrita y es ejecutable

- **GIVEN** el README del módulo
- **WHEN** se busca cómo obtener la cobertura sobre tráfico real
- **THEN** hay una consulta SQL completa sobre `asistente.registro_operativo`
- **AND** dice que `carril` es la ruta real y `intencion_sombra` la que se habría tomado

#### Scenario: El modelo de datos documenta la columna en el mismo commit

- **GIVEN** el commit que agrega `intencion_sombra`
- **WHEN** se revisan sus archivos
- **THEN** incluye `docs/architecture/data-model.md` con la columna descrita
