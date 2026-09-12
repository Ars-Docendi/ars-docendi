## ADDED Requirements

### Requirement: Comentarios de esquema en español sobre los objetos legibles

El sistema SHALL registrar un comentario en la base de datos para cada tabla y cada columna que el manifiesto de privilegios declara concedida. Los comentarios MUST estar escritos en español y MUST incluir los sinónimos con que el dominio nombra al objeto cuando difieren de su identificador.

Las tablas clasificadas como denegadas MUST NOT recibir comentario.

#### Scenario: Toda tabla concedida tiene comentario

- **WHEN** se recorre el manifiesto de privilegios y se consulta el catálogo de la base
- **THEN** cada tabla declarada concedida tiene un comentario no vacío

#### Scenario: Toda columna concedida tiene comentario

- **WHEN** se recorre el manifiesto de privilegios y se consulta el catálogo de la base
- **THEN** cada columna declarada concedida a cualquiera de los dos roles tiene un comentario no vacío

#### Scenario: Las tablas denegadas no se describen

- **GIVEN** una tabla clasificada como denegada en el manifiesto
- **WHEN** se consulta su comentario en el catálogo
- **THEN** no tiene comentario

#### Scenario: Los comentarios son idempotentes

- **WHEN** la migración de comentarios se aplica dos veces seguidas
- **THEN** la segunda aplicación no falla y el catálogo queda igual

### Requirement: Prefijo de prompt derivado de los privilegios efectivos

El sistema SHALL construir el prefijo estable del prompt de sistema leyendo los privilegios de lectura **efectivos** de la conexión que lo pide, junto con los comentarios de esquema de los objetos alcanzados.

El prefijo MUST NOT derivarse de una lista de tablas o columnas embebida en el código de la aplicación.

#### Scenario: Una columna revocada desaparece del prefijo

- **GIVEN** un prefijo construido para el rol de lectura básica
- **WHEN** se revoca el privilegio de lectura sobre una columna y se construye el prefijo en un proceso nuevo
- **THEN** el prefijo ya no menciona esa columna

#### Scenario: El rol con datos personales tiene un prefijo distinto

- **WHEN** se construyen los prefijos de los dos roles de lectura
- **THEN** el prefijo del rol con datos personales menciona las columnas personales y el del rol básico no

#### Scenario: Las tablas denegadas no aparecen

- **WHEN** se construye el prefijo de cualquiera de los dos roles
- **THEN** no menciona ninguna tabla clasificada como denegada en el manifiesto

### Requirement: Estabilidad del prefijo entre turnos

El prefijo MUST NOT contener ningún dato que varíe por turno: ni la fecha de referencia, ni la identidad del actor, ni la pregunta, ni los ejemplos seleccionados.

Dos construcciones del prefijo para el mismo rol y el mismo esquema MUST producir el mismo texto, byte a byte.

#### Scenario: Dos turnos distintos comparten prefijo

- **GIVEN** dos turnos con preguntas distintas, actores distintos y fechas de referencia distintas
- **WHEN** se comparan los prefijos de sistema de sus llamadas de generación
- **THEN** son idénticos byte a byte

#### Scenario: El prefijo no contiene la fecha ni el actor

- **WHEN** se inspecciona el texto del prefijo
- **THEN** no contiene la fecha de referencia del turno ni ningún identificador de actor

#### Scenario: El prefijo se calcula una sola vez

- **GIVEN** un proceso recién arrancado
- **WHEN** varios turnos consecutivos piden el prefijo del mismo rol
- **THEN** la base se consulta una sola vez

#### Scenario: El ping sigue respondiendo sin base

- **GIVEN** la base de datos detenida
- **WHEN** se solicita el endpoint de smoke test del módulo
- **THEN** responde correctamente, porque el prefijo se construye recién cuando alguien lo pide

### Requirement: Huella del prefijo

El sistema SHALL exponer una huella estable del prefijo completo, para que los reportes de evaluación puedan sellarse con ella.

La huella MUST ser reproducible entre procesos y entre máquinas para el mismo prefijo.

#### Scenario: La misma huella en dos procesos

- **GIVEN** el mismo esquema y el mismo rol
- **WHEN** se calcula la huella del prefijo en dos procesos distintos
- **THEN** las dos huellas coinciden

#### Scenario: Un cambio de esquema cambia la huella

- **GIVEN** una huella calculada sobre el esquema actual
- **WHEN** se concede una columna nueva y se recalcula en un proceso nuevo
- **THEN** la huella es distinta
