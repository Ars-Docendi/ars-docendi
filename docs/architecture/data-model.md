# Data model

Modelo de datos del sistema. **Un schema PostgreSQL por módulo** para aislar bounded contexts.

## ORM

**Entity Framework Core 10** con migraciones por módulo. Cada módulo tiene su propio `DbContext` apuntando a su schema:

- `DesignacionesDbContext` → schema `designaciones`
- `AulasDbContext` → schema `aulas`
- `PortalDbContext` → schema `portal`
- `TareasDbContext` → schema `tareas`

## Conexión

Single connection string compartida (mismo Postgres), pero cada DbContext usa `MigrationsHistoryTable("__EFMigrationsHistory", schema: "<modulo>")` para no mezclar historial de migraciones.

```csharp
optionsBuilder.UseNpgsql(connectionString, npgsql =>
    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema: "designaciones"));
```

La clave del connection string es **`ArsDocendi`** (`ConnectionStrings:ArsDocendi`). Es el nombre que la infra de deploy inyecta por ambiente como `ConnectionStrings__ArsDocendi` (ver `infra/compose/compose.base.yml`), apuntando a la base aislada de cada ambiente (`arsdocendi_<env>`). Los 4 `ModuleExtensions.cs` y el `appsettings.json` leen esa misma clave.

## Migraciones en deploy

El `ArsDocendi.Host` soporta un arranque **one-shot** de migraciones: con el argumento `--migrate` aplica las migraciones pendientes de los 4 módulos y termina con exit 0 **sin** levantar el web server. Lo invoca la infra de deploy (`infra/scripts/spin-up.sh`, variable `COMANDO_MIGRACIONES`, default `dotnet ArsDocendi.Host.dll --migrate`).

Respeta la frontera de módulos (invariante #1): cada módulo expone su rutina de migración vía la interfaz `IMigradorModulo` (en `ArsDocendi.Shared`) con una implementación **interna** que envuelve su `DbContext`; el Host resuelve todas las implementaciones por DI y nunca referencia los `DbContext` internos. La operación es idempotente (`Database.Migrate()`).

## Entidades por módulo (skeleton)

### Designaciones (`schema: designaciones`)

| Tabla                 | Descripción | PII |
| --------------------- | ----------- | --- |
| _(a definir en spec)_ | ...         | ... |

### Aulas (`schema: aulas`)

| Tabla                 | Descripción | PII |
| --------------------- | ----------- | --- |
| _(a definir en spec)_ | ...         | ... |

### Portal (`schema: portal`)

| Tabla                       | Descripción                                                          | PII                                  |
| --------------------------- | -------------------------------------------------------------------- | ------------------------------------ |
| `Docentes`                  | Datos personales del docente, áreas de experticia, horas disponibles | **Sí** — nombre, DNI, mail, teléfono |
| _(otras a definir en spec)_ | ...                                                                  | ...                                  |

### Tareas (`schema: tareas`)

| Tabla                 | Descripción | PII |
| --------------------- | ----------- | --- |
| _(a definir en spec)_ | ...         | ... |

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

El módulo `Portal` maneja datos personales de docentes. Requisitos:

- **Encriptación at-rest**: PostgreSQL con encrypted volume en la VM (TBD en `infrastructure.md`).
- **Encriptación in-transit**: TLS obligatorio para conexiones a Postgres en producción.
- **Logs sin PII**: no loggear cuerpos de request/response con datos personales. Si es necesario, hashear o redactar.
- **Backup encriptado**: dumps de Postgres deben estar encriptados antes de salir de la VM.
- **Borrado**: tener procedimiento para honrar bajas de docentes (GDPR-like aunque no aplique directamente, es buena práctica institucional).

## Relaciones cross-schema

PostgreSQL permite FKs cross-schema. **Política**: evitarlas. Si un módulo necesita referenciar un dato de otro módulo, usar:

- **Soft reference** por ID (sin FK física), con validación de existencia via interfaz pública del otro módulo (ver `module-anatomy.md`).
- **Excepción**: cuando el costo de inconsistencia es muy alto y la performance lo justifica, FK cross-schema con justificación documentada en este archivo.

## Migraciones

Comandos por módulo:

```bash
# Crear nueva migration
dotnet ef migrations add <Nombre> \
  --project backend/src/Modules.Designaciones \
  --startup-project backend/src/ArsDocendi.Host \
  --context DesignacionesDbContext

# Aplicar migrations
dotnet ef database update \
  --project backend/src/Modules.Designaciones \
  --startup-project backend/src/ArsDocendi.Host \
  --context DesignacionesDbContext
```

(repetir cambiando proyecto y context para cada módulo)

## Seeds

Datos seed mínimos para desarrollo (roles, parámetros del sistema) viven en cada módulo bajo `Infrastructure/Seeds/<NombreSeed>.cs` y se ejecutan en `ModuleRegistration` cuando `ASPNETCORE_ENVIRONMENT=Development`.
