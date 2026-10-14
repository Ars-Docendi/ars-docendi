## MODIFIED Requirements

### Requirement: Los archivos SQL versionados son la fuente autorizada del DDL

El sistema SHALL mantener el DDL de los schemas en archivos `.sql` versionados bajo `database/<schema>/`, agrupados por responsabilidad e identificados de forma inequívoca. Esos archivos SHALL ser la única fuente autorizada del schema: ninguna estructura MUST definirse solo en código C#. Toda tabla de negocio que requiera auditoría SHALL quedar enganchada mediante `SELECT audit.attach(...)` después de existir tanto la tabla como la infraestructura de auditoría; se SHALL permitir diferir ese enganche a un script explícito para resolver dependencias.

#### Scenario: Una estructura nueva se autora en SQL

- **WHEN** se agrega una tabla, índice, constraint o función al sistema
- **THEN** su definición MUST vivir en un archivo `.sql` versionado bajo `database/`

#### Scenario: Toda tabla de negocio queda enganchada a la auditoría

- **WHEN** finaliza la instalación del baseline o una migración que crea una tabla de negocio auditada
- **THEN** la llamada a `audit.attach` correspondiente MUST haberse aplicado después de sus dependencias
- **AND** el orden y el recurso que la ejecuta MUST ser identificables

## ADDED Requirements

### Requirement: Baseline consolidado de alpha para instancias nuevas

El sistema SHALL instalar directamente el modelo final vigente de Identity/Audit, Storage, Designaciones y Portal mediante un baseline por contexto con schema, sin recrear estructuras históricas transitorias ni ejecutar backfills innecesarios para una base nueva. MUST conservar catálogos obligatorios, permisos, funciones, triggers, secuencias, índices, constraints y referencias entre schemas. Aulas/Tareas MUST NOT recibir estructuras ficticias sólo para igualar el número de baselines. El corte SHALL rechazar histories anteriores o schemas gestionados existentes sin historial reconocido antes de escribir, sin drop, stamp ni conversión automática.

#### Scenario: Instalación nueva del modelo final

- **GIVEN** una base vacía
- **WHEN** se aplica el baseline consolidado
- **THEN** MUST existir el modelo final vigente y sus catálogos de sistema
- **AND** Storage MUST existir antes de aplicar referencias a sus archivos desde Designaciones y Portal

#### Scenario: Historial anterior encontrado

- **GIVEN** una base con identificadores alpha retirados o schema gestionado sin historial reconocido
- **WHEN** se consulta o intenta aplicar el nuevo baseline
- **THEN** MUST devolverse incompatibilidad sin modificar schema, datos ni historial

### Requirement: Autoría asistida sin efectos sobre bases

El repositorio SHALL ofrecer una operación de scaffolding que reciba contexto y nombre y genere juntos el script SQL y su migración asociada. MUST validar nombres/contextos, evitar sobrescribir archivos y ofrecer dry-run sin writes. MUST NOT conectar a bases ni ejecutar migraciones durante la generación.

#### Scenario: Creación válida

- **GIVEN** contexto conocido y nombre sin colisión
- **WHEN** se genera una migración
- **THEN** MUST producirse SQL y wrapper EF asociados y reconocibles por el inventario

#### Scenario: Dry-run o colisión

- **WHEN** se solicita dry-run o un nombre que colisiona con archivos existentes
- **THEN** MUST preservarse el filesystem en dry-run y rechazarse la colisión sin sobrescritura

### Requirement: Integridad de recursos e historial protegido

CI SHALL rechazar referencias SQL faltantes o no embebidas, identificadores/rutas duplicados y scripts sin consumidor ni clasificación explícita. Después del corte revisado, SHALL rechazar modificaciones o eliminaciones de los archivos históricos protegidos comparados con una referencia Git confiable. El corte alpha SHALL establecer el nuevo inventario protegido sin dejar un bypass genérico. El orden de recursos SHALL ser explícito y compartido por ejecución e inventario.

#### Scenario: Recurso inválido

- **GIVEN** una migración que referencia SQL ausente o que no se incluye en la imagen publicada
- **WHEN** se valida el cambio
- **THEN** CI MUST fallar antes del deploy

#### Scenario: Reescritura de historia protegida

- **GIVEN** el baseline posterior al corte está registrado como protegido
- **WHEN** un cambio altera o elimina una migración o recurso histórico protegido
- **THEN** CI MUST rechazarlo e indicar los archivos afectados

#### Scenario: Adición incremental

- **GIVEN** historia protegida intacta
- **WHEN** se agrega una nueva migración y sus recursos válidos
- **THEN** la validación MUST permitir esa adición sin exigir otra versión manual global
