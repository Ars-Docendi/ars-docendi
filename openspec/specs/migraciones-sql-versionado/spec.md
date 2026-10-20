# migraciones-sql-versionado Specification

## Purpose

Definir el DDL versionado como fuente autorizada y su aplicación idempotente mediante migraciones de Entity Framework Core.

## Requirements

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

### Requirement: Correcciones mediante historia incremental protegida

Toda corrección de DDL SHALL agregarse mediante SQL y wrapper incremental, después del historial reconocido de su contexto. MUST preservar bytes de SQL/wrappers históricos y protección del inventario confiable. Los snapshots de modelo SHALL actualizarse cuando corresponda sin convertirse en DDL paralelo ni reescribir migraciones aplicadas.

#### Scenario: Reparación de una función histórica

- **GIVEN** la función de numeración pertenece al baseline protegido
- **WHEN** se corrige su truncamiento
- **THEN** una migración incremental reemplaza su definición sin editar el SQL histórico ni resetear su secuencia

### Requirement: Preflight de datos antes de activar garantías

Antes de activar restricciones nuevas el proceso SHALL verificar datos relevantes en solo lectura y bloquear incompatibilidades. La migración MUST conservar filas, IDs, vínculos Microsoft, snapshots y documentos; MUST NOT completar datos faltantes, fusionar identidades, purgar ni adoptar historia alpha automáticamente.

#### Scenario: Huérfano impide FK nueva

- **GIVEN** una designación con materia inexistente
- **WHEN** se prepara el upgrade
- **THEN** se informa la incompatibilidad y no se borra ni reasigna esa designación para validar la FK

#### Scenario: Datos coherentes

- **GIVEN** una base compatible con datos anteriores y documentos referenciados
- **WHEN** se aplica el upgrade
- **THEN** conserva los datos y bytes, aplica solo incrementales y la reejecución no agrega cambios

### Requirement: Instalación y upgrade son verificaciones diferentes

Cada cambio de schema SHALL verificarse tanto en instalación nueva como en upgrade desde la versión anterior con datos sintéticos. Las verificaciones MUST usar recursos y runner reales, comprobar catálogo de constraints/triggers/índices y distinguir estado, preview sin escrituras, aplicación y no-op. Cero pruebas descubiertas MUST NOT considerarse éxito.

#### Scenario: Preview sobre datos previos

- **GIVEN** una base reconocida en la versión anterior con datos
- **WHEN** se genera preview
- **THEN** no cambia filas, histories ni auditoría y el script contiene únicamente operaciones pendientes

#### Scenario: Restricciones nuevas efectivas

- **GIVEN** el upgrade completado
- **WHEN** se intentan escrituras sintéticas que violan las nuevas garantías
- **THEN** fallan por la constraint o trigger esperado y la inspección del catálogo confirma la garantía real

### Requirement: Índices derivados de consultas comprobadas

Los índices nuevos SHALL responder a consultas y distribuciones identificadas. La validación MUST comparar planes/cardinalidad/buffers y costo de escritura con datasets reproducibles, considerando índices parciales y de exclusión existentes. MUST NOT prometer aceleraciones productivas desde una única medición ni agregar índices duplicados por un barrido simplista de FKs.

#### Scenario: FK con índice parcial suficiente

- **GIVEN** una FK nullable con índice para valores no nulos
- **WHEN** se revisan candidatos de rendimiento
- **THEN** no se propone otro índice equivalente solo porque el existente es parcial

#### Scenario: Candidato sin beneficio

- **GIVEN** un índice candidato de listado
- **WHEN** mediciones repetibles no muestran utilidad frente al costo o cobertura existente
- **THEN** se registra su descarte y no se agrega por completar una lista de tareas

### Requirement: Recuperación compatible sin reset automático

El despliegue SHALL preservar el procedimiento de backup verificable BD+objetos con escritores detenidos cuando corresponda. Ante fallo MUST detener publicación y conservar evidencia; preferirá roll-forward. Una recuperación SHALL requerir autorización explícita y destino aislado con versión compatible, nunca Down destructivo, reseed o restore automático sobre una base compartida.

#### Scenario: Fallo durante el upgrade

- **GIVEN** respaldo y preview registrados
- **WHEN** falla una migración incremental
- **THEN** no se registra deploy exitoso ni se ejecuta un reset para completar la instalación
