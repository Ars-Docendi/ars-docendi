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
dotnet add reference ../ArsDocendi.Migraciones/ArsDocendi.Migraciones.csproj
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
```

El Host referencia `Modules.<X>.Contracts` directamente sólo si consume una de sus interfaces (como `Modules.Designaciones.Contracts`).

### 6. Estructura interna mínima

Copiar la forma de `Modules.Aulas`, el módulo mínimo vigente, y crear sólo lo que se usa (sin carpetas vacías):

```
Modules.<X>/
├── Api/<X>Controller.cs           # ping obligatorio
├── Infrastructure/
│   ├── <X>DbContext.cs            # schema "<x>"
│   └── Migrador<X>.cs             # IMigradorModulo
└── ModuleExtensions.cs            # Add<X>Module(IConfiguration)
```

`Application/` o `Services/`, `Repositories/` y `Domain/` se agregan con la primera feature real (ver `Modules.Tareas`).

### 7. Endpoint `/api/<x>/ping` obligatorio

Igual a `Modules.Aulas/Api/AulasController.cs`:

```csharp
[HttpGet("ping")]
public IActionResult Ping() => Ok(new { module = "<x>", status = "ok" });
```

### 8. Registración

`ModuleExtensions.cs` replica `Modules.Aulas/ModuleExtensions.cs`: DbContext con interceptor de auditoría, `IMigradorModulo` y `AddApplicationPart`. Después, en `ArsDocendi.Host/Program.cs`, encadenar `.Add<X>Module(builder.Configuration)`.

Registrar el contexto nuevo en `contextos` de `scripts/migrations/comun.mjs` y crear el schema inicial con `node scripts/migrations/scaffold.mjs <X> <Nombre>` ([database/README.md](../../../database/README.md)). No usar `dotnet ef migrations add`.

### 9. Frontend slice (si aplica)

```bash
mkdir -p frontend/src/features/<x>
```

Estructura mínima:

```
frontend/src/features/<x>/
├── api/                     # funciones tipadas sobre shared/api/client.ts
├── components/
├── hooks/                   # React Query hooks
└── routes.tsx               # rutas del feature
```

### 10. Documentar el módulo

- Crear `docs/architecture/domains/<x>.md` desde `_template.md`.
- Llenar Purpose, Roles, Bounded context, dependencies.
- Agregar entrada al edge registry en `dependency-graph.md` si tiene dependencias cross-module.

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

Debe retornar `{ "module": "<x>", "status": "ok" }`.

No agregues un test automatizado del ping: lo verifica en cada despliegue `infra/scripts/smoke.sh`. Sumá `<x>` a la lista de módulos de ese script y de su test (`infra/tests/smoke.test.mjs`). Los tests del módulo nuevo siguen los criterios de [golden-principles → Tests](../../../docs/quality/golden-principles.md#tests).

### 13. Commit + PR

Branch `feature/<x>`. Ver [open-pr.md](../../../docs/workflows/open-pr.md).

## Reglas duras

- **Nunca** referenciar otros `Modules.<Otro>` (no Contracts) — usar siempre el `.Contracts` de los otros.
- **Sin código** en `Modules.<X>.Contracts` — solo DTOs, interfaces, tokens.
- **Endpoint `/api/<x>/ping`** es obligatorio.
- **`ModuleExtensions.cs`** debe existir aunque al principio registre poco.
- Documentar en `dependency-graph.md` cualquier edge nuevo cross-module.

## Arguments

`$ARGUMENTS` — nombre del módulo en PascalCase (e.g. `Examenes`).
