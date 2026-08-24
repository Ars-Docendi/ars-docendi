# Data model

Modelo de datos del sistema. **Un schema PostgreSQL por módulo** para aislar bounded contexts.

## ORM

**Entity Framework Core 10** con migraciones por módulo. Cada módulo tiene su propio `DbContext` apuntando a su schema:

- `IdentityDbContext` → schemas `identity` y `audit` (vive en `ArsDocendi.Shared`, ver más abajo)
- `DesignacionesDbContext` → schema `designaciones`
- `AulasDbContext` → schema `aulas`
- `PortalDbContext` → schema `portal`
- `TareasDbContext` → schema `tareas`

### Dueño de `identity` y `audit`

No son de ningún módulo: viven en **`ArsDocendi.Shared`**, porque son infraestructura transversal de la que dependen los 4 módulos. Es la única I/O admitida en ese proyecto — ver invariante #4 en [CLAUDE.md](../../CLAUDE.md), enmendado en el change `modelo-datos-identity-designaciones`.

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

Single connection string compartida (mismo Postgres), pero cada DbContext usa `MigrationsHistoryTable("__EFMigrationsHistory", schema: "<modulo>")` para no mezclar historial de migraciones.

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

El `ArsDocendi.Host` soporta un arranque **one-shot** de migraciones: con el argumento `--migrate` aplica las migraciones pendientes de cada módulo y termina con exit 0 **sin** levantar el web server. Lo invoca la infra de deploy (`infra/scripts/spin-up.sh`, variable `COMANDO_MIGRACIONES`, default `dotnet ArsDocendi.Host.dll --migrate`).

Respeta la frontera de módulos (invariante #1): cada módulo expone su rutina de migración vía la interfaz `IMigradorModulo` (en `ArsDocendi.Shared`) con una implementación **interna**; el Host resuelve todas las implementaciones por DI y nunca referencia los `DbContext` internos. La operación es idempotente.

El orden de ejecución es el orden de registración en `Program.cs`. `Modules.Asistente` va **último** a propósito: no tiene schema ni entidades propias, y su migrador solo concede privilegios de lectura sobre tablas de otros schemas. Si corriera antes, cada `GRANT` fallaría con «relation does not exist». Por lo mismo, su migrador no envuelve un `DbContext`: ejecuta SQL idempotente por construcción (`CREATE EXTENSION IF NOT EXISTS` y `GRANT`, que repetido es un no-op) sin historial de migraciones que llevar.

## Entidades por schema

### Identity (`schema: identity`) — dueño: `ArsDocendi.Shared`

| Tabla          | Descripción                                                                                                                                                                                | PII                                            |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------- |
| `personas`     | Entidad canónica de una persona. Existe **con o sin cuenta**: un Alta refiere a alguien que nunca se logueó y todavía no tiene legajo (por eso `legajo` es nullable, BR-designaciones-018) | **Sí** — documento, CUIL, teléfono, fecha nac. |
| `users`        | Cuenta de Azure AD. Sólo autenticación; `persona_id` se resuelve en el primer login                                                                                                        | Parcial — UPN, display name                    |
| `roles`        | Catálogo **abierto**. Los 7 originales llevan `es_sistema` y están protegidos por trigger                                                                                                  | No                                             |
| `permisos`     | Catálogo **cerrado** de 20. Cada `code` lo lee un check del backend                                                                                                                        | No                                             |
| `rol_permisos` | Membresía rol → permiso. La parte editable del modelo de autorización                                                                                                                      | No                                             |
| `user_roles`   | Asignación de rol a usuario, acotada por materia/carrera según el `scope` del rol. Soft-delete                                                                                             | No                                             |
| `carreras`     | Catálogo. Vive acá por ser destino de ámbito de las asignaciones                                                                                                                           | No                                             |
| `materias`     | Catálogo. Es también la unidad de "cátedra"                                                                                                                                                | No                                             |

### Designaciones (`schema: designaciones`)

| Tabla              | Descripción                                                                                                                   | PII |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------------- | --- |
| `cargos`           | Catálogo único de cargos docentes. `orden` registra la jerarquía institucional                                                | No  |
| `periodos`         | Ventana de carga + rango de impacto. A lo sumo uno activo (índice único parcial)                                              | No  |
| `pedidos`          | **El trámite.** Cubre exactamente una materia; `snapshot` congela los datos vigentes al enviar                                | No  |
| `pedido_adjuntos`  | Documentación respaldatoria. Qué es obligatorio lo decide la novedad                                                          | No  |
| `pedido_historial` | Historial del trámite. Dato de dominio, **no** derivado de `audit.change_log` (ver abajo)                                     | No  |
| `designaciones`    | **El estado vigente** `(persona, materia, cargo, horas)` con vigencia. `origen_pedido_id` NULL = carga administrativa directa | No  |

### Aulas, Portal, Tareas

Sin modelar todavía: sus `types.ts` en el frontend están vacíos y no hay pantalla que valide un modelo. Se definen cuando tengan superficie.

### Por qué el historial no sale de `audit.change_log`

`pedido_historial` es una tabla de dominio y no una vista sobre el log, por cuatro razones:

1. **El rol con el que se actuó no es derivable.** El log guarda `changed_by` (un usuario), pero un usuario puede tener varios roles.
2. **El comentario es dato de negocio**, exigido por BR-designaciones-005 y visible en la UI.
3. **`changed_by` es nullable** — queda NULL si el claim no parsea como UUID. Un registro probatorio no lo tolera.
4. **El log está pensado para purgarse** (índice BRIN sobre `changed_at`). El historial de un trámite no se purga.

Igual hace `audit.attach`: que alguien edite el historial a mano tiene que dejar rastro.

## Trazabilidad de cambios

Toda tabla de negocio queda auditada via **un único trigger AFTER** definido en el schema `audit` ([`database/audit/001_audit_schema.sql`](../../database/audit/001_audit_schema.sql)):

- `audit.log_change` (AFTER INSERT/UPDATE/DELETE) escribe una fila por evento en `audit.change_log` con `old_row`/`new_row` (JSONB), `changed_columns`, `changed_by`, `changed_at` y `request_id`. UPDATEs no-op (donde nada cambió) no se loggean.

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

Los datos personales del sistema (documento, CUIL, teléfono, fecha de nacimiento) se concentran en **`identity.personas`**, no en `Portal`. Cuando Portal tenga schema, sumará lo suyo (áreas de experticia, disponibilidad horaria), pero la identidad de la persona vive en un solo lugar. Requisitos:

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
| `designaciones.idempotencia_comandos` | `response_body` guarda el cuerpo HTTP completo de cada comando                                    |
| `designaciones.pedidos.snapshot`      | JSONB de forma arbitraria que puede cambiar sin que nadie revise el manifiesto                    |
| `designaciones.pedido_adjuntos.uri`   | Ubicación del archivo: referencia a un recurso, no dato de consulta                               |
| `identity.users.azure_oid`            | Identificador opaco del directorio externo                                                        |
| `identity.user_roles.granted_by`      | Rastro de una acción administrativa sobre otra persona                                            |

Las columnas personales de `identity.personas` —`documento`, `cuil`, `fecha_nacimiento`, `telefono`— y `identity.users.upn` van **solo** al rol con datos personales.

**Dónde vive qué**: el alta de los roles está en `infra/scripts/provision-db.sh` (corre antes que las tablas); los `GRANT` están en `database/asistente/001_asistente_grants.sql`, que ejecuta el migrador del módulo con las tablas ya creadas.

**Deny-by-default verificable**: `database/asistente/manifiesto-privilegios.json` enumera toda tabla de los schemas expuestos y toda columna de las concedidas. Un test compara ese manifiesto contra los privilegios efectivos en tres direcciones y falla si divergen: privilegio efectivo no declarado, privilegio declarado inexistente, y tabla o columna sin clasificar. Una tabla nueva rompe el CI en vez de quedar concedida en silencio.

## Relaciones cross-schema

PostgreSQL permite FKs cross-schema. **Política**: evitarlas. Si un módulo necesita referenciar un dato de otro módulo, usar:

- **Soft reference** por ID (sin FK física), con validación de existencia via interfaz pública del otro módulo (ver `module-anatomy.md`).
- **Excepción**: cuando el costo de inconsistencia es muy alto y la performance lo justifica, FK cross-schema con justificación documentada en este archivo.

### Excepciones vigentes

| From                             | To                  | Justificación                                                                                                    |
| -------------------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `designaciones.pedidos`          | `identity.personas` | Un pedido apuntando a una persona inexistente es un registro legal roto: el costo de la inconsistencia es máximo |
| `designaciones.pedidos`          | `identity.materias` | La materia determina la cátedra y, por derivación, el Coordinador competente (BR-designaciones-009)              |
| `designaciones.pedido_historial` | `identity.roles`    | El rol con el que se actuó es parte del registro probatorio del trámite                                          |
| `designaciones.pedido_historial` | `identity.users`    | Ídem, para el actor                                                                                              |
| `designaciones.designaciones`    | `identity.personas` | Ídem que pedidos                                                                                                 |
| `designaciones.designaciones`    | `identity.materias` | Ídem que pedidos                                                                                                 |
| `audit.change_log`               | `identity.users`    | Preexistente                                                                                                     |

Todas apuntan a `identity`, y eso no es casual: `identity` **no es un módulo de negocio** sino infraestructura transversal alojada en `ArsDocendi.Shared`. Una FK hacia ahí no cruza una frontera de módulo, así que la política de arriba —pensada para relaciones módulo ↔ módulo— no aplica en su espíritu.

Nota de orden: `audit.change_log` referencia `identity.users`, pero `identity.users` necesita `audit.attach()` para engancharse al log. El ciclo se rompe creando `identity.users` sin enganche, después el schema `audit` completo, y difiriendo el `attach` a `database/identity/009_identity_audit_attach.sql`.

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

Los `.sql` se embeben como **recursos del assembly** (`<EmbeddedResource>` en el `.csproj`, apuntando a `database/` con `Link`), y la migración los ejecuta con `migrationBuilder.Sql(...)` leyéndolos con `ArsDocendi.Shared.Persistencia.RecursosSql`. Nunca se leen del filesystem en runtime: la imagen no necesita el directorio `database/`.

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
3. Sumar el archivo al array `ArchivosEnOrden` de la migración correspondiente (o crear una migración nueva).
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

- `public.seed_metadata` registra `dataset_version` (`2026.08.1`), origen y última ejecución.
- `public.seed_identities` marca exactamente qué cuentas pueden usarse con la autenticación de desarrollo.
- UUIDs reservados relacionan personas, cuentas, roles y ámbitos con carreras, materias, cargos, períodos, pedidos, historial y designaciones vigentes.
- Una transacción y un advisory lock vuelven atómica la ejecución y serializan reintentos concurrentes.
- Los upserts restauran sólo las filas propiedad del dataset; no hay `TRUNCATE` ni eliminación de filas ajenas.
- Reejecutar la misma versión es seguro. Cambiar datos declarados restaura las fixtures y conserva registros creados fuera del rango reservado.

Los módulos de negocio leen identidad mediante `IConsultasIdentity`. Sólo los servicios administrativos de `ArsDocendi.Shared.Identity.Administracion` escriben personas, cuentas, roles, permisos y ámbitos. Designaciones conserva la propiedad exclusiva de `designaciones.designaciones`; la administración docente la modifica únicamente mediante `IAdministracionDesignaciones`.
