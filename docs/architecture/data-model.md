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
