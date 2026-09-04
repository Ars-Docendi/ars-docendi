## ADDED Requirements

### Requirement: Los archivos SQL versionados son la fuente autorizada del DDL

El sistema SHALL mantener el DDL de los schemas en archivos `.sql` versionados bajo `database/<schema>/`, numerados por orden de aplicación. Esos archivos SHALL ser la única fuente autorizada del schema: ninguna estructura MUST definirse solo en código C#. Cada archivo de tabla de negocio SHALL terminar invocando `SELECT audit.attach(...)` sobre la tabla que define.

#### Scenario: Una estructura nueva se autora en SQL

- **WHEN** se agrega una tabla, índice, constraint o función al sistema
- **THEN** su definición MUST vivir en un archivo `.sql` versionado bajo `database/`

#### Scenario: Toda tabla de negocio queda enganchada a la auditoría

- **WHEN** se revisa un archivo `.sql` que crea una tabla de negocio
- **THEN** MUST terminar con la llamada a `audit.attach` correspondiente

### Requirement: Los SQL se embeben en el assembly y los aplican las migraciones EF

El sistema SHALL embeber los archivos `.sql` como recursos del assembly y aplicarlos desde migraciones de Entity Framework Core mediante `migrationBuilder.Sql(...)`. El sistema MUST NOT leer los `.sql` desde el filesystem en runtime. El orden de aplicación SHALL quedar determinado por la migración que los invoca, no por el nombre del archivo.

#### Scenario: Migración sobre una base limpia

- **GIVEN** una base de datos vacía
- **WHEN** el Host arranca con `--migrate`
- **THEN** todos los schemas MUST quedar creados y el proceso MUST terminar con exit 0 sin levantar el web server

#### Scenario: El deploy no depende de rutas del filesystem

- **GIVEN** la aplicación empaquetada como imagen de contenedor
- **WHEN** se ejecuta la migración
- **THEN** MUST completarse sin requerir que el directorio `database/` esté presente en la imagen

#### Scenario: Reaplicar migraciones es idempotente

- **GIVEN** una base de datos con todas las migraciones ya aplicadas
- **WHEN** se vuelve a ejecutar `--migrate`
- **THEN** el proceso MUST terminar con exit 0 sin alterar el estado de la base

### Requirement: Construcciones de PostgreSQL que EF Core no genera

El sistema SHALL expresar en SQL crudo las construcciones que el proveedor de EF Core no puede generar: funciones y triggers plpgsql, `NULLS NOT DISTINCT` en índices de unicidad, y constraints `EXCLUDE`. El modelo de EF Core MUST permanecer coherente con el schema real: una estructura definida solo en SQL MUST no ser recreada ni contradicha por el modelo.

#### Scenario: El modelo de EF no diverge del schema real

- **GIVEN** un schema aplicado desde los archivos `.sql`
- **WHEN** se compara el modelo de EF Core contra la base
- **THEN** MUST no existir una migración pendiente que intente recrear estructuras ya definidas en SQL

#### Scenario: Los triggers sobreviven a la migración

- **GIVEN** una base migrada desde cero
- **WHEN** se ejecuta una operación que un trigger debe rechazar
- **THEN** el trigger MUST estar activo y rechazar la operación
