---
name: create-module
description: Scaffold de un nuevo módulo .NET (Modules.X + Modules.X.Contracts) en el monorepo siguiendo la convención del proyecto. Crea proyectos, agrega a la solution, referencia desde el Host, expone endpoint /api/{x}/ping obligatorio, y crea slice frontend si aplica.
argument-hint: [<NombreEnPascalCase>]
---

# Create module

Scaffold de un **nuevo módulo .NET** (`Modules.<X>` + `Modules.<X>.Contracts`) siguiendo la convención del proyecto.

## Cuándo usar

- `/add-feature` revela necesidad de un nuevo bounded context backend que no encaja en los 4 módulos existentes (Designaciones, Aulas, Portal, Tareas).
- La spec definió un módulo nuevo durante `/opsx:propose`.

## Pre-requisitos

- Leer [module-anatomy.md](../../../docs/architecture/module-anatomy.md) y [dependency-graph.md](../../../docs/architecture/dependency-graph.md).
- Confirmar que el nuevo módulo NO duplica responsabilidades de uno existente.

## Steps

### 1. Decidir nombre + ubicación

- Nombre: `Modules.<NombreEnPascalCase>` (e.g. `Modules.Examenes`).
- Path: `backend/src/Modules.<X>/` y `backend/src/Modules.<X>.Contracts/`.

### 2. Crear los dos proyectos .NET

```bash
cd backend/src
# Contracts (sin lógica)
dotnet new classlib -n Modules.<X>.Contracts -o Modules.<X>.Contracts
# Implementación
dotnet new classlib -n Modules.<X> -o Modules.<X>
```

### 3. Referenciar Contracts desde Module

```bash
cd Modules.<X>
dotnet add reference ../Modules.<X>.Contracts/Modules.<X>.Contracts.csproj
dotnet add reference ../ArsDocendi.Shared/ArsDocendi.Shared.csproj
```

### 4. Agregar a la solution

```bash
cd backend
dotnet sln ArsDocendi.slnx add src/Modules.<X>/Modules.<X>.csproj
dotnet sln ArsDocendi.slnx add src/Modules.<X>.Contracts/Modules.<X>.Contracts.csproj
```

### 5. Referenciar desde el Host

```bash
cd backend/src/ArsDocendi.Host
dotnet add reference ../Modules.<X>/Modules.<X>.csproj
dotnet add reference ../Modules.<X>.Contracts/Modules.<X>.Contracts.csproj
```

### 6. Estructura interna del módulo

Crear directorios + archivos esqueleto siguiendo [module-anatomy.md](../../../docs/architecture/module-anatomy.md):

```
Modules.<X>/
├── Controllers/
│   └── <X>Controller.cs       # con endpoint /api/<x>/ping mínimo
├── Services/
│   └── <X>Service.cs
├── Repositories/
│   └── <X>Repository.cs
├── Domain/
│   ├── Entities/
│   └── ValueObjects/
├── Infrastructure/
│   └── <X>DbContext.cs        # EF Core con schema "<x>"
├── Internal/                  # placeholder
└── ModuleRegistration.cs      # IServiceCollection extension
```

### 7. Endpoint `/api/<x>/ping` obligatorio

```csharp
[ApiController]
[Route("api/<x>")]
public class <X>Controller : ControllerBase {
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { module = "<x>", timestamp = DateTimeOffset.UtcNow });
}
```

### 8. Registración

En `ModuleRegistration.cs`:

```csharp
public static class <X>ModuleExtensions {
    public static IServiceCollection Add<X>Module(this IServiceCollection services, IConfiguration config) {
        services.AddDbContext<<X>DbContext>(opts => opts.UseNpgsql(config.GetConnectionString("ArsDocendi"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema: "<x>")));
        services.AddScoped<I<X>Service, <X>Service>();
        // ...
        return services;
    }
}
```

En `ArsDocendi.Host/Program.cs`:

```csharp
builder.Services.Add<X>Module(builder.Configuration);
```

### 9. Frontend slice (si aplica)

```bash
mkdir -p frontend/src/features/<x>
```

Estructura mínima:

```
frontend/src/features/<x>/
├── index.ts                 # exports públicos del feature
├── api/                     # llamadas axios al backend
├── components/
├── hooks/                   # React Query hooks
└── routes.tsx               # rutas del feature
```

### 10. Documentar el módulo

- Crear `docs/architecture/domains/<x>.md` desde `_template.md`.
- Llenar Purpose, Roles, Bounded context, dependencies.
- Agregar la fila del proyecto nuevo —y de cada arista que traiga— a `backend/manifiesto-de-aristas.json`, en el mismo PR. Un proyecto sin clasificar pone el CI en rojo; si nadie lo referencia todavía, va con estado `huerfano` y motivo escrito.

### 11. Crear `docs/business-rules/<x>.md` (si tendrá BRs)

Desde `_template.md`. Inicialmente vacío, se llena cuando se identifiquen reglas.

### 12. Smoke test

```bash
cd backend
dotnet build ArsDocendi.slnx
dotnet run --project src/ArsDocendi.Host
# En otra terminal
curl http://localhost:5000/api/<x>/ping
```

Debe retornar `{ "module": "<x>", "timestamp": "..." }`.

### 13. Commit + PR

Branch `feature/create-module-<x>`. Ver [open-pr.md](../../../docs/workflows/open-pr.md).

## Reglas duras

- **Nunca** referenciar otros `Modules.<Otro>` (no Contracts) — usar siempre el `.Contracts` de los otros.
- **Sin código** en `Modules.<X>.Contracts` — solo DTOs, interfaces, tokens.
- **Endpoint `/api/<x>/ping`** es obligatorio.
- **`ModuleRegistration.cs`** debe existir aunque al principio registre poco.
- Registrar en `backend/manifiesto-de-aristas.json` cualquier arista nueva cross-module, con su motivo.

## Arguments

`$ARGUMENTS` — nombre del módulo en PascalCase (e.g. `Examenes`).
