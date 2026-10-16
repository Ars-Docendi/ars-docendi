## ADDED Requirements

### Requirement: Clasificación de toda columna legible

El sistema SHALL clasificar cada columna legible por alguno de los dos roles del asistente en exactamente una de tres categorías: `publica`, `sensible-valor` o `sensible-texto`.

El manifiesto MUST vivir en un único archivo versionado. La clasificación MUST NOT duplicarse en el código.

#### Scenario: Toda columna concedida está clasificada

- **GIVEN** el manifiesto de privilegios y el de sensibilidad
- **WHEN** se comparan sus columnas
- **THEN** toda columna que el de privilegios concede a algún rol figura clasificada en el de sensibilidad

#### Scenario: Una columna legible sin clasificar hace fallar el test

- **GIVEN** una columna concedida que el manifiesto de sensibilidad no clasifica
- **WHEN** corre la verificación
- **THEN** falla nombrando esa columna

#### Scenario: Una categoría desconocida se rechaza

- **GIVEN** un manifiesto con una columna en una categoría que no es ninguna de las tres
- **WHEN** se lo carga
- **THEN** la carga falla nombrando la columna y la categoría

### Requirement: Las cuatro columnas personales son `sensible-valor`

El sistema SHALL clasificar `documento`, `cuil`, `telefono` y `fecha_nacimiento` de `identity.personas` como `sensible-valor`.

#### Scenario: Las cuatro están marcadas

- **WHEN** se inspecciona el manifiesto
- **THEN** las cuatro columnas figuran como `sensible-valor`

### Requirement: El texto libre del trámite es `sensible-texto`

El sistema SHALL clasificar `designaciones.pedido_historial.comentario` como `sensible-texto`.

La categoría existe porque es texto libre que puede nombrar a cualquier persona, y redactarlo con reglas sería frágil.

#### Scenario: El comentario del historial está marcado

- **WHEN** se inspecciona el manifiesto
- **THEN** `pedido_historial.comentario` figura como `sensible-texto`

### Requirement: La clasificación se resuelve a identificadores del motor

El sistema SHALL resolver cada entrada del manifiesto al par formado por el identificador de la tabla y el número de atributo de la columna, tal como los reporta el motor.

El enmascarado MUST decidirse por ese par y MUST NOT decidirse por el nombre de la columna en el resultado.

#### Scenario: Un alias no esconde una columna sensible

- **GIVEN** una consulta que selecciona una columna `sensible-valor` bajo un alias que no aparece en el manifiesto
- **WHEN** se clasifica el resultado
- **THEN** la columna queda clasificada como `sensible-valor`

#### Scenario: Una columna calculada queda con origen desconocido

- **GIVEN** una consulta con una columna que no es una referencia directa a una columna de tabla
- **WHEN** se clasifica el resultado
- **THEN** esa columna queda con origen desconocido y se trata como pública

#### Scenario: La resolución no se repite en cada turno

- **GIVEN** dos turnos consecutivos con el mismo rol
- **WHEN** se cuentan las lecturas del catálogo
- **THEN** la resolución se hizo una sola vez
