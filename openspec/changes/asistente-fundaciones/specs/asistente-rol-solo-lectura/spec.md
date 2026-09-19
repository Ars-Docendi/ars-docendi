## ADDED Requirements

### Requirement: Roles de solo lectura por ambiente

El sistema SHALL crear, para cada ambiente, dos roles de PostgreSQL destinados exclusivamente al asistente: `asistente_ro_<ambiente>` y `asistente_ro_pii_<ambiente>`. Ambos MUST tener `LOGIN` y contraseña propia del ambiente, y MUST carecer de todo privilegio de mutación (`INSERT`, `UPDATE`, `DELETE`, `TRUNCATE`) sobre cualquier objeto de la base.

El alta MUST ocurrir en el script de provisioning, antes de que corran las migraciones, y la baja MUST ocurrir junto con la destrucción de la base del ambiente.

#### Scenario: El rol no puede escribir

- **GIVEN** una base aprovisionada con las migraciones aplicadas
- **WHEN** una sesión autenticada como `asistente_ro_<ambiente>` ejecuta `INSERT INTO designaciones.pedidos ...`
- **THEN** PostgreSQL rechaza la sentencia con error de permisos y ninguna fila es creada

#### Scenario: El rol no puede modificar ni borrar

- **GIVEN** una sesión autenticada como `asistente_ro_<ambiente>`
- **WHEN** ejecuta `UPDATE`, `DELETE` o `TRUNCATE` sobre cualquier tabla de `identity` o `designaciones`
- **THEN** PostgreSQL rechaza cada sentencia con error de permisos

#### Scenario: Un rol por ambiente

- **GIVEN** dos ambientes aprovisionados en la misma instancia de PostgreSQL
- **WHEN** se listan los roles del cluster
- **THEN** existe un par de roles distinto por ambiente, y ninguna contraseña se comparte entre ambientes

#### Scenario: La baja del ambiente elimina sus roles

- **WHEN** se destruye la base de un ambiente efímero
- **THEN** los roles `asistente_ro_<ambiente>` y `asistente_ro_pii_<ambiente>` dejan de existir en el cluster

### Requirement: Privilegios de lectura enumerados columna por columna

El sistema SHALL conceder lectura mediante `GRANT SELECT` con la lista explícita de columnas de cada tabla concedida. El sistema MUST NOT usar `GRANT ... ON ALL TABLES IN SCHEMA` en ninguna circunstancia. Los `GRANT USAGE` sobre los schemas, los `GRANT SELECT` y `CREATE EXTENSION unaccent` MUST ejecutarse en una migración, no en el script de provisioning.

#### Scenario: Las columnas personales no salen por el rol básico

- **GIVEN** una sesión autenticada como `asistente_ro_<ambiente>`
- **WHEN** ejecuta `SELECT documento FROM identity.personas`
- **THEN** PostgreSQL rechaza la sentencia con error de permisos

#### Scenario: Un SELECT estrella sobre personas falla por el rol básico

- **GIVEN** una sesión autenticada como `asistente_ro_<ambiente>`
- **WHEN** ejecuta `SELECT * FROM identity.personas`
- **THEN** PostgreSQL rechaza la sentencia con error de permisos, porque la tabla tiene columnas no concedidas

#### Scenario: Las columnas concedidas sí se leen

- **GIVEN** una sesión autenticada como `asistente_ro_<ambiente>`
- **WHEN** ejecuta `SELECT id, legajo, nombre, apellido FROM identity.personas`
- **THEN** la consulta devuelve filas sin error

#### Scenario: El rol con permiso de datos personales sí las lee

- **GIVEN** una sesión autenticada como `asistente_ro_pii_<ambiente>`
- **WHEN** ejecuta `SELECT documento, telefono FROM identity.personas`
- **THEN** la consulta devuelve filas sin error

#### Scenario: El schema de auditoría queda fuera de alcance

- **GIVEN** una sesión autenticada como cualquiera de los dos roles del asistente
- **WHEN** ejecuta `SELECT * FROM audit.change_log`
- **THEN** PostgreSQL rechaza la sentencia con `permission denied for schema audit`

#### Scenario: La caché de idempotencia queda fuera de alcance

- **GIVEN** una sesión autenticada como cualquiera de los dos roles del asistente
- **WHEN** ejecuta `SELECT * FROM designaciones.idempotencia_comandos`
- **THEN** PostgreSQL rechaza la sentencia con error de permisos

### Requirement: Cadenas de conexión distinguibles en compilación

El sistema SHALL exponer cada cadena de conexión como un tipo propio —`CadenaDuena`, `CadenaSoloLectura` y `CadenaSoloLecturaPii`— y no como `string`. Ningún componente MAY recibir una cadena de conexión tipada como `string` desnudo.

#### Scenario: Pasar la cadena equivocada no compila

- **WHEN** un componente que declara recibir `CadenaSoloLectura` recibe una instancia de `CadenaDuena`
- **THEN** la solución no compila

#### Scenario: Cada tipo resuelve a una cadena distinta

- **GIVEN** el Host compuesto con la configuración de un ambiente
- **WHEN** se resuelven los tres tipos desde el contenedor de dependencias
- **THEN** cada uno devuelve una cadena de conexión con un usuario de base de datos distinto
