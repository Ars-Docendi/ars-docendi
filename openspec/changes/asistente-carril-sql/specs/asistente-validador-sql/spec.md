## ADDED Requirements

### Requirement: Emisión y chequeo de identificadores entrecomillados

El tokenizador del validador MUST emitir el contenido de los identificadores delimitados por comillas dobles como un token propio, respetando la regla de escape de la comilla doble duplicada.

Ese token MUST chequearse contra la lista de **funciones** prohibidas. MUST NOT chequearse contra palabras clave prohibidas.

> En PostgreSQL las comillas dobles delimitan un identificador, no una cadena: `"set_config"` sigue siendo la función `set_config`. Descartar su contenido antes de tokenizar permite fijar un actor distinto del propio y saltear Row Level Security.

#### Scenario: Se rechaza la escritura del actor con comillas dobles

- **WHEN** se valida `SELECT "set_config"('app.asistente_user_id','x',true)`
- **THEN** se rechaza

#### Scenario: Se rechaza la variante con subconsulta escalar

- **WHEN** se valida una consulta que invoca `"set_config"` dentro de una subconsulta escalar de la lista de selección
- **THEN** se rechaza

#### Scenario: Se rechaza la variante con join lateral

- **WHEN** se valida una consulta que invoca `"set_config"` dentro de un join lateral
- **THEN** se rechaza

#### Scenario: Se rechaza la lectura del ajuste con comillas dobles

- **WHEN** se valida `SELECT "current_setting"('app.asistente_user_id')`
- **THEN** se rechaza

#### Scenario: Las mayúsculas y los espacios intercalados no evaden

- **WHEN** se valida la misma invocación con mayúsculas mezcladas y espacios entre el identificador y el paréntesis
- **THEN** se rechaza

#### Scenario: Un alias entrecomillado legítimo se acepta

- **WHEN** se valida `SELECT count(*) AS "cantidad" FROM identity.carreras`
- **THEN** se acepta

#### Scenario: Un alias que coincide con una palabra clave se acepta

- **WHEN** se valida una consulta cuyo alias entrecomillado coincide con una palabra clave prohibida
- **THEN** se acepta, porque un identificador no es una palabra clave

### Requirement: Rechazo mecánico de las funciones de reloj

El validador MUST rechazar las ocho funciones de reloj: `now`, `current_date`, `current_timestamp`, `localtime`, `localtimestamp`, `statement_timestamp`, `clock_timestamp` y `transaction_timestamp`.

El rechazo MUST ser mecánico y MUST NOT depender de ninguna instrucción del prompt.

#### Scenario: Cada función de reloj se rechaza

- **WHEN** se valida una consulta que usa cada una de las ocho funciones de reloj
- **THEN** cada una se rechaza

#### Scenario: Las formas sin paréntesis también se rechazan

- **GIVEN** las funciones de reloj que PostgreSQL admite sin paréntesis
- **WHEN** se validan en esa forma
- **THEN** se rechazan igual

#### Scenario: Una fecha literal se acepta

- **WHEN** se valida una consulta que compara contra una fecha literal recibida por parámetro
- **THEN** se acepta

### Requirement: Rechazo de mutación y de sentencias múltiples

El validador MUST rechazar toda palabra clave de mutación o de definición de datos.

El validador MUST rechazar cualquier entrada que contenga más de una sentencia.

#### Scenario: Las palabras clave de mutación se rechazan

- **WHEN** se valida una consulta que contiene una sentencia de inserción, actualización, borrado, creación, alteración, eliminación o concesión de privilegios
- **THEN** se rechaza

#### Scenario: Dos sentencias se rechazan

- **WHEN** se valida una entrada con dos sentencias separadas por punto y coma
- **THEN** se rechaza

#### Scenario: Un punto y coma final no molesta

- **WHEN** se valida una única sentencia terminada en punto y coma
- **THEN** se acepta

#### Scenario: Un punto y coma dentro de una cadena no cuenta

- **WHEN** se valida una única sentencia con un punto y coma dentro de un literal de texto
- **THEN** se acepta

### Requirement: Los comentarios y las cadenas no evaden el chequeo

El tokenizador MUST descartar el contenido de los comentarios de línea y de bloque, y el de los literales de texto, antes de buscar palabras prohibidas.

El tokenizador MUST reconocer los literales delimitados por signo pesos.

#### Scenario: Una palabra prohibida dentro de un comentario no rechaza

- **WHEN** se valida una consulta legítima con una palabra prohibida dentro de un comentario
- **THEN** se acepta

#### Scenario: Un comentario no oculta una función prohibida

- **WHEN** se valida una consulta que intercala un comentario de bloque entre el nombre de una función prohibida y su paréntesis
- **THEN** se rechaza

#### Scenario: Una palabra prohibida dentro de un literal de texto no rechaza

- **WHEN** se valida una consulta que compara contra un literal que contiene una palabra prohibida
- **THEN** se acepta

#### Scenario: Un literal delimitado por signo pesos no evade

- **WHEN** se valida una consulta que usa un literal delimitado por signo pesos para ocultar una invocación prohibida
- **THEN** se rechaza o el contenido queda tratado como literal, nunca como código ejecutable no chequeado

### Requirement: El validador es la segunda capa, no la primera

El sistema MUST mantener el rechazo del motor como primera capa: el rol de lectura no tiene privilegios de mutación y la transacción se declara de solo lectura.

Un ataque que evadiera el validador MUST seguir fallando en el motor.

#### Scenario: El ataque autocontenido no obtiene alcance global

- **GIVEN** un actor de alcance acotado
- **WHEN** el carril ejecuta la consulta de ataque que intenta fijarse otro actor
- **THEN** o bien la consulta es rechazada, o bien devuelve el alcance legítimo del actor y nunca el global

#### Scenario: Una escritura dentro de la transacción falla

- **GIVEN** una consulta de escritura que el validador no rechazara
- **WHEN** se ejecuta en la transacción del carril
- **THEN** el motor la rechaza
