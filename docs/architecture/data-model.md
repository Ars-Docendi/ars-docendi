# Data model

Modelo de datos del sistema. **Un schema PostgreSQL por módulo** para aislar bounded contexts.

## ORM

**Entity Framework Core 10** con migraciones por módulo. Cada módulo tiene su propio `DbContext` apuntando a su schema:

- `IdentityDbContext` → schemas `identity` y `audit` (vive en `ArsDocendi.Shared`, ver más abajo)
- `DesignacionesDbContext` → schema `designaciones`
- `AulasDbContext` → schema `aulas`
- `PortalDbContext` → schema `portal`
- `TareasDbContext` → schema `tareas` (`proyectos`, `tareas`, `tarea_relaciones`, `tarea_comentarios`, `tarea_historial` y los catálogos `estados_proyecto`, `estados_tarea`, `prioridades` y `tipos_tarea`); DDL versionado en `database/tareas/`. Estados, prioridades y tipos se referencian por IDs numéricos; los números visibles de proyectos/tareas usan secuencias. Las cinco tablas de negocio tienen triggers `audit.attach`; sus snapshots nuevos usan la allowlist por tabla de `audit.safe_snapshot` y excluyen título, descripción, solución, texto y detalle libres.
- `AlmacenamientoDbContext` → schema `storage`

### Dueño de `identity` y `audit`

No son de ningún módulo: viven en **`ArsDocendi.Shared`**, porque son infraestructura transversal de la que dependen los 4 módulos. Es la única I/O admitida en ese proyecto — ver [AGENTS.md](../../AGENTS.md).

Consecuencia a vigilar: todos los módulos alcanzan `identity` sin pasar por Contracts. Leen para autorizar, vía `IConsultasIdentity`; escribir `personas`, `roles`, `permisos` o `rol_permisos` es exclusivo de la superficie de administración. Ver [dependency-graph.md](dependency-graph.md#frontera-de-lectura-sobre-identity).

### El modelo EF no genera DDL

**Todas** las entidades se mapean con `ExcludeFromMigrations()`. El DDL es el SQL versionado bajo `database/` (ver "Migraciones" más abajo); el modelo de EF sólo describe el schema para poder consultarlo. Así EF no puede divergir del SQL ni intentar recrear índices parciales, triggers plpgsql o constraints `EXCLUDE` que no sabe expresar.

Verificable con:

```bash
dotnet ef migrations has-pending-model-changes \
  --project backend/src/ArsDocendi.Shared \
  --startup-project backend/src/ArsDocendi.Host \
  --context IdentityDbContext
```

## Conexión

Single connection string compartida (mismo Postgres), pero cada DbContext usa `MigrationsHistoryTable("__EFMigrationsHistory", schema: "<modulo>")` para no mezclar historial de migraciones. En el corte de alpha, Designaciones/Portal/Aulas/Tareas dejan de usar el historial compartido de `public`; Identity y Storage mantienen el suyo. Este cambio es sólo para instalaciones nuevas, sin conversión ni marcado de histories anteriores.

```csharp
optionsBuilder.UseNpgsql(connectionString, npgsql =>
    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema: "designaciones"));
```

La clave del connection string es **`ArsDocendi`** (`ConnectionStrings:ArsDocendi`). Es el nombre que la infra de deploy inyecta por ambiente como `ConnectionStrings__ArsDocendi` (ver `infra/compose/compose.base.yml`), apuntando a la base aislada de cada ambiente (`arsdocendi_<env>`).

### Cadenas tipadas

Esa clave se lee **una sola vez**, en `AddArsDocendiShared`, y a partir de ahí la cadena viaja como tipo, no como `string`:

| Tipo                   | Usuario                       | Para qué                                 |
| ---------------------- | ----------------------------- | ---------------------------------------- |
| `CadenaDuena`          | `app_<ambiente>`              | Migrar, leer y escribir. Todo el sistema |
| `CadenaSoloLectura`    | `asistente_ro_<ambiente>`     | Consulta generada, sin datos personales  |
| `CadenaSoloLecturaPii` | `asistente_ro_pii_<ambiente>` | Consulta generada, con datos personales  |

Viven en `ArsDocendi.Shared/Persistencia/CadenasDeConexion.cs`. Los `DbContext` y los migradores las piden por tipo (`sp.GetRequiredService<CadenaDuena>()`), no por clave de configuración.

Son **tres tipos independientes**: sin clase base común y sin conversiones entre sí. Una base compartida dejaría escribir un parámetro del tipo base y volvería a aceptar cualquiera de las tres, que es el error que estos tipos existen para impedir. Pasar la cadena equivocada no compila.

Las dos de solo lectura se **derivan** de la del dueño —mismo host, mismo puerto, misma base, otro usuario y otra contraseña— en vez de configurarse por separado. Con tres cadenas independientes, un typo en el nombre de la base haría que el asistente leyera otro ambiente sin que nada fallara. Los roles y sus contraseñas llegan de la sección `Asistente` (`Asistente__RolSoloLectura`, `Asistente__PasswordSoloLectura`, y sus pares con PII).

`ToString()` de las tres devuelve la cadena **sin la contraseña**: interpolar una en un log o en un mensaje de excepción no filtra el secreto. El valor crudo está en `Valor`, que hay que pedir a propósito.

## Migraciones en deploy

El `ArsDocendi.Host` soporta un arranque **one-shot** de migraciones: con el argumento `--migrate` aplica Identity/Audit, Storage, Designaciones y Portal en ese orden, manteniendo la integración de Aulas/Tareas sin inventarles baselines, y deja para el final el migrador de `Modules.Asistente` (ver abajo). Termina con exit 0 **sin** levantar el web server. Lo invoca la infra de deploy (`infra/scripts/spin-up.sh`, variable `COMANDO_MIGRACIONES`, default `dotnet ArsDocendi.Host.dll --migrate`).

Respeta la frontera de módulos (invariante #1): cada módulo expone su rutina de migración vía la interfaz `IMigradorModulo` (en `ArsDocendi.Shared`) con una implementación **interna**; el Host resuelve todas las implementaciones por DI y nunca referencia los `DbContext` internos. La operación es idempotente.

El orden de ejecución es el orden de registración en `Program.cs`. `Modules.Asistente` va **último** a propósito: no tiene entidades propias, y su migrador concede privilegios de lectura sobre tablas de otros schemas. Si corriera antes, cada `GRANT` fallaría con «relation does not exist». Por lo mismo, su migrador no envuelve un `DbContext`: ejecuta SQL que converge por construcción (`CREATE ... IF NOT EXISTS`, `GRANT` y `ADD COLUMN IF NOT EXISTS`, que repetidos son no-op) sin historial de migraciones que llevar.

Sin historial, nada garantiza que esa convergencia se haya escrito: un `CREATE TABLE IF NOT EXISTS` contra una base que ya tiene la tabla no agrega la columna nueva. Por eso el migrador del asistente **verifica al final** contra `information_schema.columns` que la base tenga las columnas que el módulo escribe, y **falla nombrando la que falta** en vez de dejar arrancar. Es la red debajo de los `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` del DDL: sin ella, una columna faltante hace reventar el `INSERT` del registro en cada turno, el fallo se traga para no tumbar el servicio y el registro deja de guardar en silencio.

## Diagnóstico de integridad de datos

El modo one-shot `dotnet ArsDocendi.Host.dll --diagnosticar-integridad` compone
lecturas locales mediante `IDiagnosticoIntegridadModulo`. Cada módulo consulta
su propio schema; Identity aporta destinos canónicos. El Host compone referencias,
no accede a DbContexts de módulos ajenos. Las consultas usan transacciones
`REPEATABLE READ READ ONLY`, sin saneamiento ni eventos de negocio.

La salida identifica ambiente, release, historial observado y categorías/conteos
con hasta diez IDs técnicos por categoría; no muestra UPN, nombres, contenido o
credenciales. Un consumidor faltante, lectura fallida o incompatibilidad produce
exit no cero. Las lecturas locales no constituyen una fotografía transaccional
global entre módulos: ejecutar con escritores conocidos detenidos para preflight.
No certifica ausencia de drift ni protege frente a un DBA o escritor externo que
omita el protocolo. Desactivación conserva identidades históricas; borrado físico
no debe dejar referencias huérfanas. Saneamiento de datos reales requiere una
operación manual aprobada, respaldo y verificación posterior, nunca auto-merge,
reseed o reasignación para superar una garantía.

## Entidades por schema

### Identity (`schema: identity`) — dueño: `ArsDocendi.Shared`

| Tabla           | Descripción                                                                                                                                                                                                                                                                                            | PII                                            |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------- |
| `personas`      | Entidad canónica de una persona. Existe **con o sin cuenta**: un Alta refiere a alguien que nunca se logueó y todavía no tiene legajo (por eso `legajo` es nullable, BR-designaciones-018)                                                                                                             | **Sí** — documento, CUIL, teléfono, fecha nac. |
| `users`         | Cuenta de acceso dada de alta por la administración. `azure_oid` y `azure_tid` identifican la cuenta Microsoft: vacíos hasta el primer ingreso, que los vincula (`CHECK` ambos o ninguno); desde entonces el usuario se reconoce por ellos y no por el mail. El ingreso nunca crea usuarios            | Parcial — UPN, display name                    |
| `roles`         | Catálogo **abierto**. Los 7 originales llevan `es_sistema` y están protegidos por trigger; los personalizados conservan su código estable y usan `is_active` para baja lógica                                                                                                                          | No                                             |
| `permisos`      | Catálogo **cerrado**, sembrado por SQL versionado. Cada `code` lo lee un check del backend (`Permisos.cs`)                                                                                                                                                                                             | No                                             |
| `rol_permisos`  | Membresía rol → permiso. La parte editable del modelo de autorización; se conserva al desactivar un rol                                                                                                                                                                                                | No                                             |
| `user_roles`    | Asignación de rol a usuario, acotada por materia/carrera según el `scope` del rol. Soft-delete; las relaciones sobreviven a la baja del rol para auditoría                                                                                                                                             | No                                             |
| `carreras`      | Catálogo. Vive acá por ser destino de ámbito de las asignaciones                                                                                                                                                                                                                                       | No                                             |
| `planes`        | Plan de estudios de una carrera: `carrera_id`, `codigo` único por carrera, `vigente`/`activo`. Catálogo **informativo**: ninguna FK de negocio depende de esta tabla                                                                                                                                   |
| `materias_plan` | Pertenencia de una materia a un plan: `plan_id`, `materia_id`, `activo`. Única por par `(plan_id, materia_id)`. Catálogo **informativo** — qué materias dicta cada carrera y con qué vigencia; `user_roles`/`pedidos`/`designaciones` llevan `materia_id` + `carrera_id` directos, no una FK hacia acá |
| `materias`      | Catálogo canónico: una fila por código de 5 dígitos (único global). Es la unidad de "cátedra" para el Jefe de Cátedra                                                                                                                                                                                  | No                                             |

`identity.roles.is_active` es el estado operativo del rol. Las consultas de catálogo, creación,
edición, roles base, asignaciones nuevas y resolución de permisos sólo consideran roles activos.
`DELETE /api/administracion/roles/{id}` no elimina filas: exige la `version` vigente, marca
`is_active = false`, conserva `rol_permisos` y `user_roles`, y nunca permite desactivar roles de
sistema. El código generado de un rol personalizado no cambia al renombrarlo; la combinación del
nombre entre roles activos debe ser única. Los permisos de los roles de sistema sí son mutables
porque integran la autorización persistida de las pantallas.

Un Alta puede insertar una fila en `identity.personas` sin fila asociada en
`identity.users` ni legajo. La operación pública `IAdministracionIdentity` de
`ArsDocendi.Shared.Identity.Administracion` delega esa escritura en
`ServicioPersonas`; la unicidad de `documento` y el trigger de auditoría siguen
siendo autoridad. La administración vincula la cuenta con `persona_id` antes del
ingreso; `ServicioIngreso` sólo reconoce usuarios existentes y vincula su identidad
Microsoft (`azure_oid` y `azure_tid`) en el primer ingreso, sin crear personas ni usuarios.

### Designaciones (`schema: designaciones`)

El baseline incluye el catálogo auditado
`dedicaciones`: UUID, código único 1–6, nombre, orden único, activo y `created_at`.
`pedidos.dedicacion_solicitada_id` y `designaciones.dedicacion_id` lo referencian
mediante FK nullable. Los textos históricos Categoría 0 y los snapshots se
conservan como parte del modelo, sin backfills de alpha en instalaciones nuevas.
Los triggers de selección impiden insertar texto libre
o cambiar los textos legados. Nuevas designaciones y solicitudes Alta/Cambio
requieren una referencia activa; una actualización sin cambio de dedicación
conserva el valor histórico, incluso si su categoría fue desactivada.

`pedidos` conserva por separado las horas solicitadas de materia,
investigación y externas, además del `snapshot` congelado al enviar. En
`designaciones`, `horas` es la carga de materia y `horas_investigacion` /
`horas_externas` son cargas complementarias nullable: `NULL` significa que el
valor vigente es desconocido. Las designaciones resultantes de un pedido
aprobado recuperan esas dos cargas cuando el origen está identificado;
continuidades y cargas administrativas pueden conservarlas en `NULL`.

Cada pedido lleva `materia_id` (canónica) y `carrera_id` directos, elegidos juntos por quien lo
carga: una materia dictada en dos carreras exige elegir una, y genera pedidos distintos según la
carrera elegida. Esa combinación se valida contra el catálogo informativo `identity.materias_plan`
(que la materia se dicte, vigente, en esa carrera), pero ninguna FK de negocio depende de él. La
cátedra del Jefe de Cátedra se valida contra la materia canónica. Para Alta, las opciones salen de
las materias a cargo del `jefe_catedra`; para Baja y Cambio, la intersección con las designaciones
vigentes del docente. El backend vuelve a validar el id y, para Baja/Cambio, exige la designación
vigente de la misma pareja `(persona_id, materia_id)` (materia canónica) antes de crear, editar o enviar.

Las designaciones guardan `materia_id` (canónica) y `carrera_id` directos. El baseline
auditado no tenía FK simple de materia; el incremental
`20261013170855_IntegridadMateriaDesignaciones` la agrega con preflight de huérfanos,
sin tocar los bytes históricos ni imponer una relación con planes/carreras informativas.
Una persona tiene a lo sumo una
designación vigente por materia canónica, sin importar la carrera (`EXCLUDE` sobre
`(persona_id, materia_id)`).

| Tabla              | Descripción                                                                                                                     | PII |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------- | --- |
| `cargos`           | Catálogo único de cargos docentes. `orden` registra la jerarquía institucional                                                  | No  |
| `periodos`         | Ventana de carga + rango de impacto. A lo sumo uno activo (índice único parcial)                                                | No  |
| `dedicaciones`     | Catálogo activo de Categorías 1–6; conserva referencias históricas si una categoría se desactiva                                | No  |
| `pedidos`          | **El trámite.** Cubre exactamente una materia; dedicación y las tres horas solicitadas; `snapshot` congela al enviar            | No  |
| `pedido_adjuntos`  | Documentación respaldatoria. Qué es obligatorio lo decide la novedad                                                            | No  |
| `pedido_historial` | Historial del trámite. Dato de dominio, **no** derivado de `audit.change_log` (ver abajo)                                       | No  |
| `designaciones`    | **El estado vigente** `(persona, materia, cargo, dedicación, tres horas)` con vigencia; `origen_pedido_id` NULL = carga directa | No  |

Pedido y Designación usan `xmin` como token EF; no requieren agregar una columna
ordinaria. Administración y circuito comparten exclusión transaccional por persona,
y el circuito identifica además el par persona–materia. Snapshot y materialización
leen la vigente dentro del guard; las lecturas anteriores se rechazan o revalidan
antes de confirmar estado, vigencias e historial. Un Cambio en su fecha de inicio
conserva ID/inicio y actualiza cargo, dedicación, tres horas y origen; un Cambio
posterior cierra/abre atómicamente sin retirar EXCLUDE. El criterio de vigente sigue
siendo `vigente_hasta IS NULL`.

Listado y detalle son consultas distintas: grilla sin documentos y con un resumen
de hasta cuatro eventos reales por pedido (sin comentarios), detalle completo con
orden `(created_at,id)`. Relectura administrativa por persona y resolución de
personas/actores por lote evitan materialización global innecesaria.
Ver [evidencia Designaciones 3.6–3.8](../../openspec/changes/archive/2026-10-20-corregir-integridad-y-rendimiento-post-auditoria/evidence/designaciones-3.6-3.8.md).

### Portal (`schema: portal`)

`perfiles` vincula una persona canónica con `contactos`, `cvs`, `experiencias`,
`educaciones`, `certificaciones`, `proyectos`, `proyecto_documentos`,
`habilidades` y `docente_habilidades`. La identidad institucional se lee desde
`identity`; Portal no la modifica. CV y documentos almacenan metadata y una
referencia lógica `archivo_id` a `storage.archivos`; las URI históricas se
conservan solo como metadata de transición y nunca se convierten en destinos
descargables. Todas las tablas tienen `created_at` y `audit.attach`.

Los accesos por `perfil_id` a experiencias, educaciones, certificaciones y proyectos
usan índices SQL incrementales, no propiedades EF generadoras de DDL. La evidencia
reproducible de planes, buffers y escrituras con dataset sintético está en
[Portal post-auditoría](../../openspec/changes/archive/2026-10-20-corregir-integridad-y-rendimiento-post-auditoria/evidence/portal-storage-tareas-post-auditoria.md).

### Tareas (`schema: tareas`)

Estados, prioridades y tipos se guardan como IDs numéricos con FK al catálogo; la
regla de Solución consulta el código `resuelta`, y los triggers impiden renombrar
un código de estado ya referenciado. `responsable_id`, `creado_por_id` y los
actores de comentarios/historial son referencias lógicas a `identity.users`, sin
FK interschema: sus destinos se validan coordinadamente en la transacción de
escritura, manteniendo la identidad histórica de usuarios inactivos.

Toda hija conserva el `proyecto_id` de su padre, incluido `NULL`; una raíz puede
no pertenecer a proyecto. Trasladar la raíz propaga el proyecto a los descendientes
en una transacción, preservando estado, avance y responsables. Triggers diferidos
rechazan ciclos y proyectos incoherentes, incluso escrituras SQL concurrentes.
Tareas y proyectos exponen `xmin` como token de versión: mutaciones sin versión se
validan, obsoletas responden conflicto y el historial se confirma con la mutación.
Relaciones usan un par canónico e idempotencia para altas/bajas repetidas.

Ver [dominio y API de Tareas](domains/tareas.md), [contratos API](api-contracts.md)
y [evidencia post-auditoría](../../openspec/changes/archive/2026-10-20-corregir-integridad-y-rendimiento-post-auditoria/evidence/portal-storage-tareas-post-auditoria.md)
para pruebas HTTP/SQL y cobertura de consultas selectivas.

### Storage (`schema: storage`)

`storage.archivos` es dueño de la metadata de los bytes que viven en SeaweedFS mediante S3:

| Columna                                                                         | Función                                                                 |
| ------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `proposito`                                                                     | `cv`, `dni_frente`, `dni_dorso`, `justificativo` o `documento_proyecto` |
| `ambiente`, `bucket`, `clave_objeto`                                            | ubicación interna; la clave nunca usa PII                               |
| `nombre_original`, `mime_declarado`, `mime_detectado`, `tamano_bytes`, `sha256` | metadata validada                                                       |
| `estado`                                                                        | `pendiente`, `cuarentena`, `disponible`, `rechazado` o `eliminado`      |
| `propietario_id`, timestamps y `motivo_revision`                                | autorización, trazabilidad y retención                                  |

El cliente solo conoce `archivoId`. `designaciones.pedido_adjuntos`, `portal.cvs`
y `portal.proyecto_documentos` mantienen referencias lógicas, sin FK cross-schema.
Los bytes no viven en PostgreSQL. La reserva, retención y coordinación de sus consumidores
se describen en [dominio de almacenamiento](domains/almacenamiento.md) y
[runbook](../operations/storage-runbook.md). El baseline conserva claves y metadata
legadas; incrementales no reescriben objetos ni fabrican bytes.

`perfiles.persona_id` **referencia `identity.personas.id`** y es el único camino del
portal hacia un nombre y un legajo. Sin `ON DELETE`, a propósito: las FK internas de
portal cascadean porque un perfil sin dueño no significa nada, pero borrar una
persona con perfil cargado tiene que fallar y que alguien lo mire.

#### Lo que el asistente ve de portal, y lo que no

Seis de las diez tablas se le conceden al asistente —`perfiles`, `educaciones`,
`certificaciones`, `experiencias`, `habilidades` y `docente_habilidades`— con
`GRANT` por columna y `ENABLE ROW LEVEL SECURITY`. El predicado es una **disyunción**
y no conjuga el ámbito, porque el portal está archivado por persona y el ámbito no
dice nada sobre un dato de persona:

```sql
persona_id = identity.asistente_persona()
OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena')
```

Ese permiso **nace concedido a nadie**: con el `GRANT` puesto, cada actor ve
exactamente su propio perfil hasta que Secretaría decida a quién dárselo.

Las otras cuatro **no se conceden**. `contactos` guarda el teléfono y el mail
personales, y el contacto institucional ya sale de `identity.personas.telefono` e
`identity.users.upn`: es todo el riesgo y ninguna pregunta nueva. `cvs`, `proyectos`
y `proyecto_documentos` no las pide ninguna pregunta del catálogo, y una tabla
expuesta que nadie consulta es prefijo de prompt que se paga en cada llamada.

Cuatro columnas quedan afuera con motivo escrito en `manifiesto-privilegios.json`:
`habilidades.usos` es un contador agregado sobre todo el padrón que una policy por
fila no puede acotar; `sugerido` y `canonica_id` son curaduría del vocabulario; y
`experiencias.descripcion` es texto libre autodeclarado que el enmascarador no puede
proteger, porque toda expresión sobre una columna reporta OID 0 y se trata como
pública.

### Por qué el historial no sale de `audit.change_log`

`pedido_historial` es una tabla de dominio y no una vista sobre el log, por cuatro razones:

1. **El rol con el que se actuó no es derivable.** El log guarda `changed_by` (un usuario), pero un usuario puede tener varios roles.
2. **El comentario es dato de negocio**, exigido por BR-designaciones-005 y visible en la UI.
3. **`changed_by` es nullable** — queda NULL si el claim no parsea como UUID. Un registro probatorio no lo tolera.
4. **El log está pensado para purgarse** (índice BRIN sobre `changed_at`). El historial de un trámite no se purga.

Igual hace `audit.attach`: que alguien edite el historial a mano tiene que dejar rastro.

## Trazabilidad de cambios

Toda tabla de negocio queda auditada via **un único trigger AFTER** definido en el schema `audit` ([`database/audit/001_audit_schema.sql`](../../database/audit/001_audit_schema.sql)):

- `audit.log_change` (AFTER INSERT/UPDATE/DELETE) escribe una fila por evento en `audit.change_log`. `old_row`/`new_row` pasan por `audit.safe_snapshot`, con allowlist explícita por tabla; una tabla/campo no clasificado queda fuera del snapshot. `changed_columns` se calcula sobre la fila completa y registra nombres de columnas, junto con `changed_by`, `changed_at` y `request_id`. UPDATEs no-op (donde nada cambió) no se loggean.

El log es la **fuente única de verdad** para metadata de auditoría. Las tablas NO denormalizan `created_by` / `updated_at` / `updated_by` / `deleted_by` — esos campos se obtienen de `audit.change_log` via `audit.row_history(...)`.

### Columnas de soporte (mínimas)

| Columna      | Tipo          | Default | Cuándo agregarla                                                                                                                               |
| ------------ | ------------- | ------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `created_at` | `TIMESTAMPTZ` | `now()` | **Siempre** en tablas de negocio auditadas. Es la única denormalización tolerada: barata, indexable, se usa para ORDER BY y filtros por rango. |
| `deleted_at` | `TIMESTAMPTZ` | NULL    | **Sólo** cuando la tabla necesita soft-delete como decisión de dominio (ej. `identity.user_roles` para permitir re-otorgar el mismo grant).    |

Cualquier otra columna de auditoría (`created_by`, `updated_at`, `updated_by`, `deleted_by`) está **prohibida** — se consulta desde el log.

### Convención para nuevas tablas

1. Incluir `created_at TIMESTAMPTZ NOT NULL DEFAULT now()` en el `CREATE TABLE`.
2. Si la tabla necesita soft-delete, agregar `deleted_at TIMESTAMPTZ NULL` y todo unique index que deba sobrevivir un re-alta debe ser parcial: `WHERE deleted_at IS NULL`.
3. Terminar el archivo SQL con `SELECT audit.attach('schema.tabla');`. Si la PK no se llama `id`, pasarla: `SELECT audit.attach('schema.tabla', 'mi_pk');`.

Catálogos cerrados (ej. `identity.roles`) usan exactamente la misma llamada — `audit.attach` no requiere ninguna forma particular de la tabla.

### Soft delete

Es **per-tabla, opcional**. Sólo las tablas que declaran `deleted_at` participan; el backend no debe asumir soft-delete genérico. Para tablas con `deleted_at`:

- "Borrar" = `UPDATE ... SET deleted_at = now()`.
- Los repositorios filtran `WHERE deleted_at IS NULL` por default en queries.
- Para tablas sin `deleted_at`, `DELETE` físico es válido (queda registrado igual en `change_log`).

### Propagación del usuario actual

`ArsDocendi.Shared.Auditing.AuditDbConnectionInterceptor` ejecuta `set_config('app.current_user_id', ...)` y `set_config('app.request_id', ...)` cada vez que EF Core abre una conexión del pool. Se registra automáticamente al llamar `AddArsDocendiShared()` y se atacha en cada `Add<Modulo>Module()`. Requisito: el `ICurrentUser.UserId` debe ser un UUID que matche `identity.users.id` — si no parsea como UUID (ej. claim sin mapear todavía) el GUC se setea vacío y el trigger loguea `changed_by = NULL`.

### Consultar la historia

Historial completo de una fila:

```sql
SELECT changed_at, action, changed_by, changed_columns, old_row, new_row
  FROM audit.change_log
 WHERE schema_name = 'identity'
   AND table_name  = 'user_roles'
   AND row_pk      = '<uuid>'
 ORDER BY changed_at;
```

Sólo el resumen (created_at/by, updated_at/by, deleted_at/by) — equivalente a leer las columnas que solíamos denormalizar:

```sql
SELECT * FROM audit.row_history('identity', 'user_roles', '<uuid>');
```

## Consideraciones PII

Los datos personales del sistema (documento, CUIL, teléfono, fecha de nacimiento) se concentran en **`identity.personas`**, no en `Portal`. Portal guarda lo suyo en su propio schema, pero la identidad de la persona vive en un solo lugar. Requisitos:

- **Encriptación at-rest**: PostgreSQL con encrypted volume en la VM (TBD en `infrastructure.md`).
- **Encriptación in-transit**: TLS obligatorio para conexiones a Postgres en producción.
- **Logs sin PII**: no loggear cuerpos de request/response con datos personales. Si es necesario, hashear o redactar.
- **Backup encriptado**: dumps de Postgres deben estar encriptados antes de salir de la VM.
- **Borrado**: tener procedimiento para honrar bajas de docentes (GDPR-like aunque no aplique directamente, es buena práctica institucional).

## Privilegios de lectura del asistente

El asistente conversacional no lee con la conexión de la aplicación. Tiene **dos roles de PostgreSQL propios, de solo lectura**, con sufijo de ambiente (`asistente_ro_prod`, `asistente_ro_pii_pr_123`):

| Rol                           | Alcance                                             |
| ----------------------------- | --------------------------------------------------- |
| `asistente_ro_<ambiente>`     | Lectura sin columnas de datos personales            |
| `asistente_ro_pii_<ambiente>` | Lectura incluyendo las columnas de datos personales |

**El límite lo impone el motor, no el código.** Los privilegios se conceden **columna por columna** con `GRANT SELECT (lista) ON tabla`, nunca sobre todas las tablas de un schema de una vez: esa forma entregaría cada tabla nueva por default y en silencio. Consecuencia visible: con el rol básico, `SELECT * FROM identity.personas` **falla** con `permission denied`, porque la tabla tiene columnas no concedidas.

Fuera de alcance, con motivo escrito:

| Objeto                                | Por qué                                                                                           |
| ------------------------------------- | ------------------------------------------------------------------------------------------------- |
| Schema `audit` completo               | `change_log.old_row/new_row` guardan la fila entera en JSON; un JSONB no admite GRANT por columna |
| Schema `asistente` completo           | Son los registros del propio asistente: el analítico tiene el texto de las preguntas de todos     |
| `designaciones.idempotencia_comandos` | `response_body` guarda el cuerpo HTTP completo de cada comando                                    |
| `designaciones.pedidos.snapshot`      | JSONB de forma arbitraria que puede cambiar sin que nadie revise el manifiesto                    |
| `designaciones.pedido_adjuntos.uri`   | Ubicación del archivo: referencia a un recurso, no dato de consulta                               |
| `identity.users.azure_oid`            | Identificador opaco del directorio externo                                                        |
| `identity.user_roles.granted_by`      | Rastro de una acción administrativa sobre otra persona                                            |

Las columnas personales de `identity.personas` —`documento`, `cuil`, `fecha_nacimiento`, `telefono`— y `identity.users.upn` van **solo** al rol con datos personales.

### Los tres permisos del asistente

| Permiso                          | Qué habilita                                                     | Concedido a                                                                  |
| -------------------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| `asistente.consultar`            | Usar el asistente (incluye el historial propio)                  | Los seis roles de sistema no `docente`                                       |
| `asistente.ver_consulta`         | Ver la consulta SQL que el asistente generó (propia o histórica) | **Ningún rol.** Se concede desde `/membresia-roles`                          |
| `asistente.leer_historial_ajeno` | Leer, con razón y auditoría, el historial de OTRO usuario        | **Ningún rol**, ni siquiera `sys_admin`. Se concede desde `/membresia-roles` |

Los tres nacen vacíos a propósito, cada uno por su propio motivo escrito en su migración: `asistente.ver_consulta` porque la consulta generada es superficie de diagnóstico y su `WHERE` puede llevar un documento, un legajo o un nombre; `asistente.leer_historial_ajeno` porque habilita leer las preguntas y consultas de OTRA persona, no las propias — superficie de soporte, no de uso (asistente-acceso-de-soporte-al-historial). Quién necesita cada uno es una decisión del Departamento, no de quien escribe la migración. Un permiso concedido de arranque es difícil de quitar; uno vacío se concede en treinta segundos cuando alguien lo pide.

Ninguno de los tres es una lista de roles en código, por el mismo motivo: `identity.roles` no es un catálogo cerrado, y una lista embebida falla **abierta** con cualquier rol que no conozca.

### El schema `asistente`: dos registros que no se cruzan

Es el único schema que el asistente escribe, y lo escribe con la **conexión dueña**. Sus propios roles de solo lectura lo tienen revocado entero.

| Tabla                               | Guarda                                                                                                                                                                                                                               | No guarda                                                        |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------- |
| `asistente.registro_operativo`      | `actor_id`, `ocurrido_en`, carril, estado, llamadas al modelo, tokens de entrada, de salida y `tokens_de_cache`, latencia, reintento, truncado, `proveedor`, `motivo_rechazo`                                                        | El texto de la pregunta, y la credencial del proveedor           |
| `asistente.registro_analitico`      | `pregunta`, categoría, estado, `dia` (tipo `date`), `causa` (vocabulario cerrado de 22 códigos, `CHECK`), `modelo` (`empresa/modelo`) y `reemplazado` (`boolean`)                                                                    | El actor y la hora exacta                                        |
| `asistente.retroalimentacion_turno` | `analitico_id` (FK a `registro_analitico.id`, `ON DELETE CASCADE`), `voto`, `razones` (`text[]`, `CHECK` restringe cada elemento a los 4 valores vigentes), `comentario` (`text`, `CHECK` de hasta 500 caracteres), `actualizado_en` | El actor, y cualquier columna que exista en `registro_operativo` |

**`retroalimentacion_turno` es la tercera fila que no se cruza, no una excepción.** Su única clave es la del analítico: sin `actor_id` propio, sin FK hacia `registro_operativo`, y con la misma denegación de esquema heredada — ninguna de las dos hereda un `GRANT` propio, así que no hace falta tocar `manifiesto-privilegios.json` para mantenerla afuera de los dos roles de solo lectura. Feature completa en [Retroalimentación del turno (domains/asistente.md)](domains/asistente.md#retroalimentación-del-turno) y TD-012.

**`razon` (un solo valor) se retiró en favor de `razones` (lista) + `comentario` (asistente-rediseno-v3, design.md D7, PO-changed 2026-09-26).** Nada shippeó a producción con la forma vieja, así que no hay ninguna fila que preservar: `lento` desaparece del todo, sin ninguna provisión de "legacy". `003_asistente_retroalimentacion.sql` declara la forma final en el `CREATE` (sin columna `razon`) y agrega `razones`/`comentario` a una base que ya tenía la tabla (la única real hoy, `arsdocendi_pr_140`) con dos `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` — una columna por sentencia, sin `CHECK` inline (el literal del array de cuatro valores lleva comas, que la forma permitida no acepta) — y dos bloques `DO` guardados que agregan cada `CHECK` sólo cuando falta por nombre. Postgres no tiene `ADD CONSTRAINT IF NOT EXISTS`, así que ese guardado hace falta para la idempotencia aunque ninguno de los dos `ALTER` haga un `DROP`: la única forma de `ALTER TABLE` que `ArquitecturaAsistenteTests` permite sin ratificar es `ADD COLUMN IF NOT EXISTS`, así que un `ADD CONSTRAINT` nuevo también necesita su entrada en `ReemplazosDeCheckRatificados` (`retroalimentacion_turno_razones_validas`, `retroalimentacion_turno_comentario_longitud`). La vieja entrada `retroalimentacion_turno_razon_valida` se retiró con ella: ningún archivo la toca. La columna `razon` y su `CHECK` original quedan intactos y sin uso en las bases que ya los tenían — nada los lee ni los escribe.

**Ninguno guarda las filas devueltas ni la consulta generada.** Ni por defecto ni detrás de un flag.

**`tokens_de_cache` es un subconjunto de `tokens_de_entrada`**, no un sumando aparte: el total de entrada es idéntico con caché y sin ella, y sin esta columna no hay forma de saber si la caché del prefijo está pegando.

**`proveedor` y `tokens_de_cache` llegaron después de la tabla**, así que además del `CREATE TABLE` van como `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`: sin eso, una base que ya la tenía no las recibe nunca. Las dos son **anulables**, y nulo significa «esta fila es anterior a la columna». Un `ADD COLUMN ... NOT NULL` sin `DEFAULT` lo rechaza PostgreSQL cuando la tabla tiene filas, y ponerle `DEFAULT` sería peor: `tokens_de_cache` con default `0` diría «la caché no pegó» de turnos donde nadie midió. La aplicación nunca escribe nulo en ellas, y el chequeo de arranque garantiza que las columnas existan.

**`motivo_rechazo` (`asistente-rechazos-dinamicos`) sigue el mismo patrón, pero nulo significa otra cosa y su `CHECK` va aparte.** La columna llegó después de la tabla —mismo `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`— y también es anulable sin `DEFAULT`. El `CHECK` con nombre que restringe el valor a `fuera_de_tema`/`otro_sistema`/`muy_general`/`no_cubierto` va inline en el `CREATE TABLE` y, contra una base que ya tenía la tabla sin la columna, en un bloque `DO` guardado por nombre en `pg_constraint` — mismo patrón que los dos `CHECK` de `retroalimentacion_turno`, y no en el propio `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`: ese detector exige una sola acción sin comas antes del `;`, y la lista de cuatro motivos necesita comas. La diferencia frente a las otras dos columnas es que en ésta nulo NO marca una fila anterior a la columna: es el valor normal de cualquier turno que no terminó en un rechazo declarado por el modelo (respondido, rechazo del validador, error del motor, resultado vacío, generación cortada por el techo de tokens). Va sólo al operativo — `registro_analitico` no la recibe nunca, mismo motivo (TD-012): lo que sostiene la desvinculación es la ausencia de la columna.

**`intencion_sombra` sigue en el schema, sin escritor, desde el retiro del enrutador de dominio en modo sombra (2026-10-27, `asistente-retirar-enrutador-sombra`).** Guardaba la intención que ese enrutador habría tomado; el enrutador nunca cambió una respuesta y se eliminó. La columna **no se borra**: `002_asistente_registros.sql` está publicada, la declara (`CREATE TABLE` y `ADD COLUMN IF NOT EXISTS`) y no se edita, y como el migrador aplica todos los recursos en cada pasada, un `DROP COLUMN` posterior se alternaría con ese `ADD COLUMN` en cada despliegue, dejando un atributo borrado por pasada (cuentan contra el límite de 1600 columnas) y un lock `ACCESS EXCLUSIVE` sobre `registro_operativo`. Es anulable y sin `DEFAULT`, así que dejarla sin escribir es inocuo: todas las filas nuevas quedan en nulo. Quitarla requiere una futura consolidación del baseline. `ColumnasQueEscribe` ya no la incluye.

Tres decisiones de esquema que sostienen la desvinculación, y las tres están escritas en [`002_asistente_registros.sql`](../../database/asistente/002_asistente_registros.sql):

1. **`dia` es de tipo `date`, no `timestamptz`.** Con alrededor de treinta usuarios, un timestamp preciso en las dos tablas permitiría reidentificar al autor de cada pregunta con un join por tiempo. El tipo es lo que garantiza la pérdida: aunque el código mandara la hora, el motor la trunca.
2. **La clave del analítico es un `uuid` aleatorio, no una identidad.** Con autoincremento en las dos, la fila _n_ de una y la fila _n_ de la otra serían el mismo turno: el orden de inserción sería, él mismo, la clave del join. Queda un residual —el orden físico— declarado como TD-012.
3. **No se les aplica `audit.attach`, y está declarado en el archivo con el motivo.** Es lo contrario de la convención del repositorio, a propósito: `audit.change_log` guarda la fila entera en JSON y no tiene política de retención, así que el texto de cada pregunta sobreviviría a la purga en otro lado. Hay un test que falla si a alguna de las dos le aparece el disparador.

Retención de 90 días configurable, con purga automática en el proceso (`PurgaDeRegistros` + un servicio hospedado) y test de retención en las dos direcciones. Una retención sin un mecanismo que borre es una frase en un documento.

**Dónde vive qué**: el alta de los roles está en `infra/scripts/provision-db.sh` (corre antes que las tablas); los `GRANT` están en `database/asistente/001_asistente_grants.sql`, que ejecuta el migrador del módulo con las tablas ya creadas.

### El historial propio y la auditoría de soporte

Tres tablas más, en el mismo schema `asistente` — heredan la denegación wholesale sin ningún `GRANT` ni cambio de manifiesto propio, mismo precedente que `retroalimentacion_turno` (asistente-historial-conversaciones):

| Tabla                                  | Guarda                                                                                                                                                                                          | No guarda                                                                |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `asistente.hilo_historico`             | `id` (propio, minteado por la aplicación), `actor_id`, `titulo`, `creado_en`, `ultima_actividad`, `archivada_en` (nullable), `borrado_pendiente_desde` (nullable), `lote_de_borrado` (nullable) | Cualquier dato del propio turno                                          |
| `asistente.turno_historico`            | `id`, `hilo_id` (FK a `hilo_historico`, `ON DELETE CASCADE`), `pregunta`, `sql_resuelto` (nullable), `estado`, `ocurrido_en`, `referencias` (jsonb, nullable)                                   | Las filas que devolvió la consulta, y el texto redactado de la respuesta |
| `asistente.auditoria_acceso_historial` | `id`, `lector_id`, `sujeto_id`, `hilo_historico_id` (nullable, **sin FK**), `razon`, `ocurrido_en`                                                                                              | — (es, ella misma, el registro de auditoría)                             |

**`hilo_historico` es, a propósito, la tabla actor-vinculada que `registro_analitico`/`registro_operativo` no son.** «Historial propio» sólo significa algo si es atribuible a su dueño: es un canal de privacidad nuevo y deliberado, contenido por el mismo deny-by-default de esquema, un permiso propio para cualquier acceso más allá del dueño, y auditoría permanente en todo lo demás. Ver TD-012 más abajo.

**`hilo_historico.id` es independiente del id efímero de `HiloConversacional`.** El hilo en memoria vive 120 minutos y se pierde en cada redespliegue; la conversación persistida tiene que sobrevivir 180 días y ser resumible mucho después. `Reanudar` mintea un hilo efímero **nuevo** con los turnos persistidos ya cargados, y ese hilo nuevo sigue extendiendo la misma conversación persistida.

**Ninguna de las tres referencia `registro_analitico`, `registro_operativo` o `retroalimentacion_turno`, ni al revés.** El historial es un canal enteramente separado: nunca guarda ni deriva el token de retroalimentación de un turno, para no reabrir por otro lado el cruce que TD-012 cierra.

**`auditoria_acceso_historial.hilo_historico_id` no lleva clave foránea, a propósito.** La fila de auditoría tiene que sobrevivir a que el propio dueño borre esa conversación (borrado permanente, sin papelera); sin FK, un id que ya no existe en `hilo_historico` sigue siendo una fila de auditoría perfectamente legible — quién leyó, a quién, cuándo, por qué.

**Retención de 180 días para el historial** (`Asistente__RetencionDeHistorialDias`), contada desde `ultima_actividad` y no desde `creado_en` — una conversación retomada nueve meses después no pierde sus turnos más viejos mientras siga activa. **365 días para la auditoría de soporte** (`Asistente__RetencionDeAuditoriaDeSoporteDias`), en una ventana **independiente**: la auditoría no se purga por el estado de la conversación que describe. `PurgaDeRegistros` suma estos dos barridos a los dos que ya tenía, mismo mecanismo, mismo criterio de fallar sin tumbar el proceso.

**`archivada_en`, `borrado_pendiente_desde` y `lote_de_borrado`** llegaron después (asistente-rediseno-v3, design.md D3/D4): archivar no toca `ultima_actividad`, así que no cambia la retención de arriba, y una conversación archivada se desarchiva sola al recibir un turno nuevo. Un borrado marca las dos últimas en vez de ejecutar un `DELETE` — toda consulta propia filtra `borrado_pendiente_desde IS NULL`, así que la conversación desaparece de inmediato para su dueño sin que la fila deje de existir todavía. `BarridoDeBorradosPendientes` (`BackgroundService` propio, cada `Asistente__PeriodoDeBarridoDeBorradosSegundos`, default 60 s) la borra de verdad — cascadeando sus turnos — en cuanto supera `Asistente__VentanaDeDeshacerSegundos` (default 15 s); `PurgaDeRegistros` corre la misma sentencia como red diaria. El índice parcial `ix_hilo_historico_borrado_pendiente` cubre sólo las filas efectivamente pendientes, que son las únicas que ese barrido y las lecturas de soporte dentro de la ventana tocan. `IConsultasDeAuditoriaDeSoporte` es la única lectura que ve una conversación pendiente, marcada, mientras su ventana no venza.

**`turno_historico.referencias`** (jsonb, nullable): marcador → tipo e id de cada mención de ese turno (`asistente-menciones`, design.md D11). Nula en todo turno sin menciones. **No implica que `sql_resuelto` las use** (decisión 15 del PO, 2026-09-26): cuando el carril SQL corrió, los marcadores son los que la consulta liga de verdad; cuando el turno nunca entró al carril SQL — rechazo, aclaración, degradación, respuesta social o de capacidades —, `CapaConversacional` numera igual las menciones que el pedido declaró (ya revalidadas por el controller) y las persiste solas, con `sql_resuelto` en `null` — ningún marcador de esa fila corresponde a nada ejecutable, sólo sirven para que `GET /historial/{id}` arme sus chips. «Volver a consultar» sigue exigiendo `sql_resuelto IS NOT NULL` antes de mirar esta columna, así que un turno sin SQL nunca la re-ejecuta por error.

**No hay endpoint que le diga al sujeto quién leyó su historial** (design.md D11 de asistente-historial-conversaciones). Es la decisión final del cliente, no una pendiente: el trade-off aceptado es el de los tres precedentes citados en el diseño (ChatGPT Enterprise, Claude Enterprise, Microsoft Purview) — el log de acceso queda del lado del administrador, no del sujeto.

**TD-012, actualizado.** La anonimidad de `registro_analitico` sigue intacta frente al asistente y a cualquiera sin acceso al historial: nada de lo de arriba lo toca. Lo que cambió es que ahora existe, a propósito, una tabla que responde exactamente la pregunta que `registro_analitico` se niega a responder — "qué preguntó fulano" — pero atribuida a su dueño y a un lector de soporte permisionado y auditado. La garantía de TD-012 hoy depende también de que nadie tenga, además, acceso al historial.

**Deny-by-default verificable**: `database/asistente/manifiesto-privilegios.json` enumera toda tabla de los schemas expuestos y toda columna de las concedidas. Un test compara ese manifiesto contra los privilegios efectivos en tres direcciones y falla si divergen: privilegio efectivo no declarado, privilegio declarado inexistente, y tabla o columna sin clasificar. Una tabla nueva rompe el CI en vez de quedar concedida en silencio.

**Qué sale hacia afuera** es una pregunta distinta de quién puede leer qué, y tiene su propio manifiesto: `database/asistente/manifiesto-sensibilidad.json` clasifica cada columna concedida en `publica`, `sensible-valor` —al proveedor del modelo va un marcador, el valor real va al llamador— o `sensible-texto`, que no viaja en absoluto. Las cuatro columnas personales de `identity.personas` y el correo institucional de `identity.users` son `sensible-valor`; las tres columnas de texto libre del trámite son `sensible-texto`. Un test falla si una columna concedida queda sin clasificar, y otro si el manifiesto clasifica una que nadie concede.

### Administración de uso (presupuestos, tope, mantenimiento, auditoría)

Ocho tablas más, en el mismo schema `asistente` — mismo criterio que las tres de arriba: heredan la denegación wholesale (`REVOKE ALL ON SCHEMA asistente` de `002_asistente_registros.sql`) sin ningún `GRANT` ni cambio de manifiesto propio. Viven todas en `database/asistente/006_asistente_administracion.sql`; el índice único parcial de `presupuesto_usuario` está en `008_asistente_presupuesto_usuario_vigente_unico.sql`.

| Tabla                                      | Guarda                                                                                                                                           | No guarda                                                          |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------ |
| `asistente.presupuesto_rol`                | `rol_code` (PK), `cupo_diario_turnos` (0 desactiva), `acceso_habilitado` (default `true`), `actualizado_en`                                      | Historial de valores anteriores (va en `auditoria_administracion`) |
| `asistente.acceso_usuario_revocado`        | `actor_id` (PK), `revocado_por`, `revocado_en` — la fila existe mientras el acceso está quitado                                                  | Un acceso «concedido»: a un usuario sólo se le puede quitar        |
| `asistente.presupuesto_usuario`            | `id`, `actor_id`, `cupo_diario_turnos`, `vigente_desde`, `vigente_hasta` (nulo = vigente; a lo sumo una vigente por actor, índice único parcial) | —                                                                  |
| `asistente.tope_organizacional`            | `id`, `tope_mensual_usd` (0 desactiva), `vigente_desde`                                                                                          | —                                                                  |
| `asistente.consumo_organizacional_mensual` | `anio`, `mes` (PK compuesta), `costo_estimado_acumulado`                                                                                         | Costo por usuario o por turno (eso lo deriva el panel de uso)      |
| `asistente.tabla_de_precios`               | `proveedor`, `modelo`, precio por token de entrada/salida/caché, `version`, `vigente_desde`, `vigente_hasta`                                     | —                                                                  |
| `asistente.modo_mantenimiento`             | Fila única (`id = 1`, `CHECK`): `activo`, `razon`, `actor_id`, `actualizado_en`                                                                  | —                                                                  |
| `asistente.auditoria_administracion`       | `id`, `actor_id`, `ocurrido_en`, `accion`, `antes`/`despues` (JSON serializado por la aplicación)                                                | — (es, ella misma, el registro de auditoría)                       |
| `asistente.auditoria_revision_de_calidad`  | `id`, `lector_id`, `ocurrido_en`, `filtros` (texto), `filas` (cuántas devolvió la lectura)                                                       | — (es, ella misma, el registro de auditoría)                       |

**Modelo en caliente** (`asistente-modelo-en-caliente`, `database/asistente/009_asistente_modelo.sql`): tres tablas más en el mismo schema, con la misma denegación wholesale (inalcanzables para los dos roles de solo lectura; `PrivilegiosLecturaTests` lo comprueba y ningún manifiesto lleva entradas).

| Tabla                               | Guarda                                                                                                                                                                         | No guarda                                                                     |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| `asistente.credencial_de_proveedor` | `empresa` (PK), `clave_cifrada` (`v1:{kid}:{base64(iv\|tag\|texto)}`, AES-GCM con AAD `asistente:{empresa}`), `pista` (últimos 4), `validada_en`, `actor_id`, `actualizado_en` | La clave en claro, ni la clave maestra; los servidores locales no tienen fila |
| `asistente.modelo_vigente`          | Fila única (`id = 1`, `CHECK`): `empresa`, `modelo`, `actor_id`, `actualizado_en`. **Sin fila = configuración del servidor**                                                   | Una FK al catálogo: se valida al aplicar, no al leer                          |
| `asistente.catalogo_de_modelos`     | `(empresa, modelo)` (PK compuesta), sembrado por el SQL; agregar un modelo es un PR                                                                                            | Listados en vivo de los proveedores                                           |

Ninguna lleva clave foránea a `identity`, igual que el resto del schema. `modelo_vigente` se lee en cada turno, sin caché de proceso.

**`presupuesto_usuario` y `tabla_de_precios` son versionadas, nunca editadas en el lugar** (design.md D6 de asistente-administracion-de-uso): editar un override o publicar un precio nuevo cierra la fila vigente y abre una nueva. Para `tabla_de_precios` hay un único escritor, `RepositorioDePrecios` (`POST /api/asistente/administracion/precios`, `asistente-modelo-en-caliente` D9): cierra la fila abierta del modelo con `vigente_hasta` = el inicio de la versión nueva —así los rangos `[vigente_desde, vigente_hasta)` quedan contiguos y exactamente uno contiene cualquier instante— y abre la nueva con `version` = máxima + 1, todo en una transacción bajo un candado consultivo por (proveedor, modelo). Los tres precios se guardan **por token**; la pantalla carga US$ por millón y el backend divide con `decimal`. Así, costear un turno ya ocurrido siempre usa el precio que estaba vigente cuando ocurrió, sin que un cambio posterior lo reescriba. `presupuesto_rol` y `tope_organizacional` no necesitan esa disciplina: sólo importa su valor **actual**, y el histórico de ediciones ya queda en `auditoria_administracion`.

**`0` desactiva**, mismo convenio que el resto del módulo (`CupoDeLlamadasPorActor` en su momento): un cupo de rol en `0`, un override en `0` o un tope organizacional en `0` significan «sin tope», nunca «tope cero turnos». Los siete roles de sistema se siembran en `0` y el tope organizacional también, hasta que el Departamento confirme los números reales (design.md, Open Questions) — ningún precio se siembra en `tabla_de_precios`, para no costear con un número adivinado.

**`consumo_organizacional_mensual` se resetea solo**: no hay ninguna acción de purga ni de admin — un mes nuevo simplemente no tiene fila todavía, y una fila ausente vale `0`.

**`auditoria_administracion` es append-only por ausencia de código, no por trigger** — mismo criterio que `auditoria_acceso_historial`: el puerto `IAuditoriaDeAdministracion` sólo tiene un método (`RegistrarAsync`), y ningún endpoint del módulo actualiza ni borra una fila. Retención propia de 365 días (`Asistente__RetencionDeAuditoriaDeAdministracionDias`), purgada por el mismo `PurgaDeRegistros`.

**`auditoria_revision_de_calidad` (`asistente-metricas-de-calidad`, `010_asistente_calidad.sql`) registra cada lectura exitosa de la lista de problemas de calidad**: quién leyó, el momento (reloj de la base), los filtros como texto y cuántas filas devolvió. Se escribe ANTES de responder (si falla, la lectura falla), es append-only por ausencia de código —`IAuditoriaDeRevisionDeCalidad` sólo tiene `RegistrarAsync`—, no lleva claves foráneas y tiene retención propia de 365 días (`Asistente__RetencionDeAuditoriaDeCalidadDias`), purgada por `PurgaDeRegistros`. Una lectura rechazada con `403` no escribe nada. `registro_analitico.causa`, `modelo` y `reemplazado` son atributos gruesos del turno, no de la persona (TD-012): `modelo` comparte su valor con `registro_operativo.proveedor`, aceptado en el diseño D3 de ese change.

**Ninguna de las siete lleva clave foránea hacia `identity`** — mismo motivo que el resto del schema: tienen que poder purgarse y conservarse con independencia del padrón.

### Row Level Security sobre el trámite

`designaciones.pedidos`, `designaciones.designaciones`, `pedido_historial` y `pedido_adjuntos` llevan **`ENABLE ROW LEVEL SECURITY`** con una policy `FOR SELECT` cada una. El predicado conjunta dos condiciones:

```sql
identity.asistente_tiene_permiso('designaciones.ver')
AND identity.asistente_alcanza_ambito(materia_id, carrera_id)
```

**Las dos, no una.** RLS decide qué filas ve una consulta, no si quien pregunta tiene derecho a la tabla — y acá no coinciden: el rol `docente` tiene ámbito de materia, pero sus permisos son `portal.ver` y `portal.editar`. Una policy que mirara solo el ámbito le abriría pedidos, historial y justificativos de rechazo que la API le niega con `403`. El asistente no ampliaría un permiso: **crearía acceso donde no hay ninguno**. Y un `[Authorize]` en el endpoint no cubre el hueco: cuando la SQL ya está corriendo, el `[Authorize]` es pasado.

El predicado es **uno solo** para los tres ámbitos: la fila trae su par materia + carrera y `asistente_alcanza_ambito()` aplica la misma regla que `MaquinaEstadosPedido.AlcanzaAmbito` (BR-designaciones-009): global ve todo, un rol de carrera ve su carrera, un rol de materia ve esa materia —el Jefe de Cátedra en cualquier carrera, el Docente sólo en la carrera de su asignación—. No ramificar por ámbito es lo que evita que un ámbito nuevo caiga en un `ELSE` permisivo.

**`ENABLE`, nunca `FORCE`.** Con `ENABLE`, el dueño de la tabla queda exento: la aplicación conecta como `app_<ambiente>` y sigue viendo y escribiendo todo. `FORCE` somete también al dueño, y como estas policies son `FOR SELECT` y están escritas para el actor del asistente, la aplicación dejaría de ver sus propias filas. Ahí `FORCE` no endurece nada: tira el backend.

Las policies **no llevan cláusula `TO`**. Es una frontera de módulos, no una decisión de seguridad: este DDL pertenece a `Modules.Designaciones` y se embebe en su assembly, mientras que los nombres de rol llevan sufijo de ambiente y solo los conoce la configuración del asistente. La restricción real la impone el predicado, que falla cerrado: sin el ajuste `app.asistente_user_id` no hay actor, sin actor no hay permiso, y sin permiso no hay filas.

### Resolución del actor

Cinco funciones en `identity`, todas `SECURITY DEFINER` y `STABLE`, responden en vivo sobre la base:

| Función                                   | Devuelve                                             |
| ----------------------------------------- | ---------------------------------------------------- |
| `identity.asistente_actor()`              | El actor del turno, leído de `app.asistente_user_id` |
| `identity.asistente_es_global()`          | Si tiene alguna asignación vigente de alcance global |
| `identity.asistente_materias_visibles()`  | Las materias que puede ver (catálogo)                |
| `identity.asistente_alcanza_ambito(m, c)` | Si alcanza una fila con ese par materia + carrera    |
| `identity.asistente_tiene_permiso(code)`  | Si la matriz vigente le da ese permiso               |

**Materias de una carrera, desde el modelo académico canónico.** `identity.materias` ya no tiene `carrera_id`: una materia canónica puede dictarse en varias carreras. Para un actor con ámbito de carrera, `asistente_materias_visibles()` relaciona materias con carreras a través de `identity.materias_plan` JOIN `identity.planes`, restringido a los planes `vigente AND activo` de la carrera del ámbito. Es la única lectura del asistente que pasa por el catálogo informativo de planes; `designaciones.pedidos` y `designaciones.designaciones` se filtran con `asistente_alcanza_ambito` sobre su propio `carrera_id`, sin derivarlo de los planes: con materias canónicas, filtrar sólo por materia le mostraría a un coordinador las designaciones de otra carrera que comparte la materia.

**Ninguna lleva un código de rol.** La matriz rol → permiso es editable desde `/membresia-roles` sin migración, e `identity.roles` no es un catálogo cerrado. Una lista negra (`code <> 'docente'`) **falla abierta**: cualquier rol nuevo pasaría por default. Se pregunta por el permiso, que es lo que el cliente administra.

`SECURITY DEFINER` con `SET search_path = ''` y todos los nombres calificados: sin eso, una función definer es un vector de escalada. `PUBLIC` no tiene `EXECUTE` sobre ninguna; el `GRANT` a los dos roles del asistente vive en la migración del módulo, que es la que conoce sus nombres con sufijo de ambiente.

`STABLE` y no `VOLATILE` no es estilo: con `VOLATILE`, un predicado sin columnas deja de ser pseudo-constante y el ejecutor lo reevalúa **fila por fila** en vez de resolverlo una vez por consulta. Hay un par de tests que compara los dos planes.

**Propagación del actor**: conexión y transacción nuevas por turno, y `set_config('app.asistente_user_id', <id>, true)` — transaction-local, así que el ajuste muere en el `COMMIT` y no sobrevive al pool. La fuente del id es `ICurrentUser.UserId`, nunca el `oid` de Azure AD: si llega el equivocado, la función **rompe** en vez de devolver cero filas, porque un vacío en silencio se lee como «no hay datos» y eso es una respuesta falsa.

### Comentarios de esquema: parte del contrato con el asistente

Toda tabla y toda columna que el manifiesto declara concedida lleva un `COMMENT ON` en español. Viven en el DDL de **cada módulo dueño** —`database/identity/20261020000200_ComentariosAsistenteIdentity.sql` y `database/designaciones/20261020001100_ComentariosAsistenteDesignaciones.sql`—, por el mismo criterio con que las policies RLS viven en el DDL de `designaciones`: el dueño del bounded context escribe el DDL de sus objetos.

**No son documentación.** El proveedor de esquema del asistente los lee del catálogo y los inyecta en el prompt de sistema, así que una columna sin comentar le llega al modelo como un nombre pelado y un tipo. Por eso incluyen a propósito los sinónimos con que el Departamento nombra cada cosa —«docente/profesor/agente», «materia/asignatura/cátedra», «pedido/trámite/solicitud»— que en el esquema no aparecen, y por eso advierten las dos colisiones del dominio: los nombres de materia se repiten entre códigos —y una misma materia canónica se dicta en varias carreras, según `identity.materias_plan`— y los apellidos se repiten entre personas.

También registran cómo se resuelve «ahora» sin tocar el reloj: `designaciones.periodos.activo` y `designaciones.designaciones.vigente_hasta IS NULL`.

Las dos tablas denegadas **no** se comentan: describir algo que el asistente no puede leer solo sirve para que lo pida y choque con `permission denied` en vez de abstenerse. Hay un test por cada dirección —concedida sin comentario, denegada con comentario—.

### El prefijo del prompt se deriva de los privilegios efectivos

El bloque de esquema del prompt de sistema no sale de una lista en el código: sale de preguntarle a la base **qué puede leer esta conexión** (`has_column_privilege` contra `current_user`), junto con los comentarios de arriba y las claves foráneas cuyos dos extremos son legibles.

Una lista embebida se desincroniza en silencio y falla en las dos direcciones. Si alguien concede una columna, el prompt sigue describiendo el esquema viejo. Si alguien la revoca —la dirección peligrosa—, el prompt se la sigue ofreciendo al modelo, que la pide, y el turno falla con `permission denied` en vez de abstenerse.

Consecuencia buscada: **los dos roles tienen prefijos distintos**, con huellas distintas, cacheados por separado. Compartir prefijo exigiría describirle al rol básico columnas que no puede leer.

El prefijo se calcula perezosamente —construirlo al arrancar rompería el invariante #3— y **no se invalida solo**: una migración de esquema exige reiniciar el proceso. Es lo correcto para lo que se optimiza, porque un prefijo que cambiara entre dos turnos consecutivos es lo que RNF-14 prohíbe y cada invalidación pagaría escritura de caché sobre el bloque más grande del prompt.

### Exposición de snapshots de auditoría

Desde `sistema-seccion-unificada` (ARS-157) la API administrativa **fusiona dos fuentes**:
`audit.change_log` (consultado directamente, como antes) y `asistente.auditoria_administracion`
(consultado **exclusivamente** a través de `Modules.Asistente.Contracts.
IConsultasDeAuditoriaDeAdministracion` — el Host nunca abre SQL directo contra el schema
`asistente`; ver [dependency-graph.md](./dependency-graph.md#administración-de-estado-y-auditoría)).
Pagina los resultados combinados (50 por defecto, máximo 100), con timeout de consulta de 5
segundos para `audit.change_log` y un cupo de lectura de 2000 filas para el rastro del asistente
(365 días de retención a ritmo humano de edición, así que en la práctica nunca se alcanza; si se
alcanza o esa fuente falla por cualquier otro motivo, la página se sirve igual sólo con
`audit.change_log`, marcada `parcial: true`).

Resuelve el nombre visible del actor con joins opcionales a `identity.users` y
`identity.personas` — la MISMA resolución para las dos fuentes, ya que el rastro del asistente
también estampa un `actor_id uuid NOT NULL`. Usa **orden natural** «Nombre Apellido» (no
«Apellido, Nombre» como antes de este cambio), recurre a `display_name` si no hay persona
vinculada, y muestra «Actor no identificado» si no existe una cuenta resoluble. Para
`audit.change_log`, cuyo `changed_by` puede ser NULL, un actor nulo se distingue en dos casos: si
tampoco hay `request_id` es «Proceso automático» (migración, seed o proceso de fondo, sin ningún
contexto HTTP); si hay `request_id` mostraba un request real cuyo usuario no se pudo identificar,
también «Actor no identificado». El rastro del asistente nunca produce «Proceso automático»
—su `actor_id` es `NOT NULL`— así que sólo puede resolver a persona o a «Actor no identificado».

El permiso `auditoria.ver` habilita ese nombre mínimo necesario; UPN, correo, documento, CUIL, teléfono y otros datos personales no forman parte de la respuesta, de ninguna de las dos fuentes. El resumen usa metadata histórica, nombres legibles de objeto/campo y no consulta el estado actual de las filas. Nunca envía `old_row` ni `new_row` crudos, ni el JSON `antes`/`despues` sin normalizar del rastro del asistente. Sólo los valores de campos explícitamente aprobados se serializan; los campos personales/secretos y cualquier campo no clasificado se devuelven ocultos con valores nulos. Para `audit.change_log` la aprobación es **por tabla** (`PoliticaValoresAuditoria`: un campo se muestra sólo si está clasificado para su `schema.tabla`, nunca por coincidencia de nombre o sufijo). El rastro del asistente no es una tabla de `audit.change_log` y usa su propia lista blanca, pequeña y explícita (`cupo`, `tope_mensual_usd`, `activo` son seguros; `razon` queda sin clasificar y por lo tanto enmascarada); en ambas fuentes la lista de campos personales/secretos gana ante cualquier solapamiento. `client_ip` existe en el DDL de auditoría, pero no forma parte del DTO ni se expone. La UI presenta este detalle como consulta de solo lectura bajo `auditoria.ver`.

**Búsqueda por etiqueta (`q`) y `unaccent`.** El filtro `q` (reemplaza a `actor`) compara contra
etiquetas — módulo, objeto, campo, tabla, clave de fila y el actor tal como se muestra— nunca
contra un valor de columna, para que un dato enmascarado o sin clasificar no sea buscable ni
indirectamente. La comparación normaliza acentos con la extensión PostgreSQL `unaccent`
(`database/audit/20261020000800_AuditBusquedaIdentity.sql`, `CREATE EXTENSION IF NOT EXISTS unaccent`, aplicada por
una migración de `IdentityDbContext`) combinada con `ILIKE`. `unaccent` ya era una dependencia
del asistente (`database/asistente/001_asistente_grants.sql`, para sus propias columnas); ahora
es también una dependencia explícita de `audit`, declarada por su cuenta — las dos migraciones
piden la misma extensión de forma idempotente, sin que una dependa del orden de la otra.

## Relaciones cross-schema

PostgreSQL permite FKs cross-schema. **Política**: evitarlas. Si un módulo necesita referenciar un dato de otro módulo, usar:

- **Soft reference** por ID (sin FK física), con validación de existencia via interfaz pública del otro módulo (ver `module-anatomy.md`).
- **Excepción**: cuando el costo de inconsistencia es muy alto y la performance lo justifica, FK cross-schema con justificación documentada en este archivo.

### Excepciones vigentes

| From                             | To                  | Justificación                                                                                                    |
| -------------------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `designaciones.pedidos`          | `identity.personas` | Un pedido apuntando a una persona inexistente es un registro legal roto: el costo de la inconsistencia es máximo |
| `designaciones.pedidos`          | `identity.materias` | La materia canónica determina la cátedra del Jefe de Cátedra                                                     |
| `designaciones.pedidos`          | `identity.carreras` | La carrera determina el Coordinador competente (BR-designaciones-009)                                            |
| `designaciones.pedido_historial` | `identity.roles`    | El rol con el que se actuó es parte del registro probatorio del trámite                                          |
| `designaciones.pedido_historial` | `identity.users`    | Ídem, para el actor                                                                                              |
| `designaciones.designaciones`    | `identity.personas` | Ídem que pedidos                                                                                                 |
| `designaciones.designaciones`    | `identity.materias` | FK `ON DELETE RESTRICT` (`20261013170855_IntegridadMateriaDesignaciones.sql`)                                    |
| `designaciones.designaciones`    | `identity.carreras` | Ídem que pedidos                                                                                                 |
| `audit.change_log`               | `identity.users`    | Preexistente                                                                                                     |

Todas apuntan a `identity`, y eso no es casual: `identity` **no es un módulo de negocio** sino infraestructura transversal alojada en `ArsDocendi.Shared`. Una FK hacia ahí no cruza una frontera de módulo, así que la política de arriba —pensada para relaciones módulo ↔ módulo— no aplica en su espíritu.

Nota de orden: `audit.change_log` referencia `identity.users`, pero `identity.users` necesita `audit.attach()` para engancharse al log. El ciclo se rompe creando `identity.users` sin enganche, después el schema `audit` completo, y difiriendo el `attach` a `database/identity/007_identity_audit_attach.sql`.

## Migraciones

**El DDL se autora en SQL, no en C#.** Los archivos `.sql` versionados bajo `database/<schema>/` son la fuente autorizada; las migraciones EF sólo los ejecutan.

El motivo es que buena parte del schema EF Core no lo sabe generar:

| Construcción                                       | ¿EF la genera?   |
| -------------------------------------------------- | ---------------- |
| Funciones y triggers plpgsql                       | No               |
| `NULLS NOT DISTINCT` en índices de unicidad        | No               |
| Constraints `EXCLUDE` (vigencias sin solapamiento) | No               |
| `SELECT audit.attach('schema.tabla')` por tabla    | No               |
| Índices parciales (`WHERE ...`)                    | Sí (`HasFilter`) |

### Cómo se aplican

Los `.sql` se embeben como **recursos del assembly** (`<EmbeddedResource>` en el `.csproj`, apuntando a `database/` con `Link`). Cada migración declara `[RecursosMigracionSql(...)]` y `Up` llama `migrationBuilder.AplicarRecursosSql(typeof(...))`, que los lee con `ArsDocendi.Shared.Persistencia.RecursosSql`. Nunca se leen del filesystem en runtime: la imagen no necesita el directorio `database/`.

El corte instala `20261005000000_BaselineIdentityYAudit`, `20261005000100_BaselineStorage`, `20261005000200_BaselineDesignaciones` y `20261005000300_BaselinePortal`. Sus recursos contienen directamente el schema final y catálogos, sin transiciones ni backfills alpha. Ver [database/README.md](../../database/README.md) para el orden y la evidencia de comparación en PostgreSQL 18. Los snapshots reflejan el modelo EF vigente, incluido Portal.

El **orden** lo fija la migración que los invoca, no el nombre del archivo — hay dependencias entre schemas que el orden alfabético no respeta.

### `database/` es un input de compilación

Como el DDL se embebe, `database/` tiene que estar dentro del **build context de Docker**. Por eso la imagen del backend se construye con el contexto en la **raíz del repo**, no en `backend/`:

```bash
docker build -f backend/Dockerfile -t <tag> .
```

Esto se aprendió por las malas: con el contexto en `backend/`, los globs del `.csproj` no matcheaban nada, MSBuild compilaba un assembly **sin recursos y sin error**, y el fallo aparecía recién al correr `--migrate` en el ambiente desplegado. Los `.csproj` ahora tienen un target `ValidarSqlEmbebido` que falla el build si el glob viene vacío, así que ese modo de falla ya no puede repetirse en silencio.

Al agregar un `.sql` nuevo no hace falta tocar nada del contexto — el glob por directorio ya lo cubre.

### Flujo para agregar una tabla

1. Escribir el `.sql` bajo `database/<schema>/`, con `created_at` y cerrando con `SELECT audit.attach(...)`.
2. Agregar la entidad al `DbContext` con `ToTable(..., t => t.ExcludeFromMigrations())`.
3. Crear una migración incremental nueva y declarar sus recursos ordenados en `[RecursosMigracionSql(...)]`; nunca editar un baseline o recurso ya aplicado.
4. Verificar que no queden cambios pendientes de modelo:

```bash
dotnet ef migrations has-pending-model-changes \
  --project backend/src/Modules.Designaciones \
  --startup-project backend/src/ArsDocendi.Host \
  --context DesignacionesDbContext
```

### Aplicar

```bash
# Todos los schemas, one-shot, sin levantar el web server
dotnet run --project backend/src/ArsDocendi.Host -- --migrate
```

Es idempotente (`Database.Migrate()`), así que re-ejecutarlo sobre una base ya migrada no produce cambios ni error.

## Seeds

El dataset de ejemplo no productivo vive en [`infra/scripts/seed-data/sintetico.sql`](../../infra/scripts/seed-data/sintetico.sql). Es una fuente transversal explícita, no una migración EF ni un inicializador de módulo. `infra/scripts/seed.sh <ambiente>` lo aplica sólo después de migrar la base.

- `public.seed_metadata` registra dataset, versión/origen e inicialización completada junto con los datos en la misma transacción; un marcador incompleto o una base poblada sin marca no autoriza sobrescribirla.
- `public.seed_identities` marca exactamente qué cuentas pueden usarse con la autenticación de desarrollo.
- UUIDs reservados relacionan personas, cuentas, roles y ámbitos con carreras, materias, cargos, períodos, pedidos, historial y designaciones vigentes.
- Una transacción y un advisory lock vuelven atómica la inicialización y serializan reintentos autorizados.
- El deploy ordinario no reejecuta el dataset: conserva modificaciones y adjuntos, incluso si cambia su versión. Los upserts del dataset no son autorización de reseed.
- Resetear fixtures requiere una operación explícita protegida no productiva, separada del deploy. SGA nunca se usa fuera de dev local.

Los módulos de negocio leen identidad mediante `IConsultasIdentity`. Sólo los servicios administrativos de `ArsDocendi.Shared.Identity.Administracion` escriben personas, cuentas, roles, permisos y ámbitos. Designaciones conserva la propiedad exclusiva de `designaciones.designaciones`; la administración docente la modifica únicamente mediante `IAdministracionDesignaciones`.
