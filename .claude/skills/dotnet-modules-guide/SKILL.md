---
name: dotnet-modules-guide
description: Convenciones .NET para módulos backend de Ars Docendi — anatomía de módulo, contract-first cross-module, layer rules Controller → Service → Repository, registración en el Host, endpoint /api/{x}/ping obligatorio.
paths:
  - "backend/src/Modules.**"
  - "backend/src/ArsDocendi.Host/**"
  - "backend/src/ArsDocendi.Shared/**"
user-invocable: false
---

# .NET modules guide

Path-scoped: se auto-activa al tocar paths backend del proyecto.

## Layout de un módulo

```
backend/src/
├── Modules.<X>/                  # INTERNO — implementación
│   ├── Controllers/
│   ├── Services/
│   ├── Repositories/
│   ├── Domain/{Entities,ValueObjects}/
│   ├── Infrastructure/<X>DbContext.cs
│   ├── Internal/                 # NO importable desde fuera
│   └── ModuleRegistration.cs
└── Modules.<X>.Contracts/        # PÚBLICO — única superficie cross-module
    ├── DTOs/
    ├── Interfaces/
    └── Events/
```

## Reglas duras

### Cross-module

- **Importar SOLO desde `Modules.<Otro>.Contracts`** — NUNCA referenciar `Modules.<Otro>/*.csproj`.
- **NO importar** desde `Modules.<Otro>/Internal/`.
- Inyectar servicios de otros módulos via DI usando interfaces de `Modules.<Otro>.Contracts/Interfaces/`.

### Capas

- **Controller → Service → Repository** únicamente.
- Controller fino, validación de DTOs, status codes, `[Authorize(Roles = ...)]`.
- Service: lógica de negocio, orquestación, transacciones.
- Repository: persistencia, queries EF Core.
- **Prohibido**: controller → repository directo.

### Contracts

- `Modules.<X>.Contracts`: **solo** DTOs, interfaces, tokens públicos. **Sin lógica**.
- Cualquier helper privado vive en `Modules.<X>/Internal/`.

### Endpoints

- Cada módulo expone `GET /api/<modulo>/ping` con `[AllowAnonymous]` como smoke test.
- Toda acción mutativa requiere `[Authorize(Roles = ...)]` con los roles institucionales relevantes.

### Persistencia

- Un schema PostgreSQL por módulo (`designaciones`, `aulas`, `portal`, `tareas`).
- Cada DbContext con `MigrationsHistoryTable` apuntando al schema del módulo.
- Sin FKs cross-schema salvo justificación documentada.

### Registración

- Cada módulo expone `IServiceCollection.Add<X>Module()` en `ModuleRegistration.cs`.
- El Host invoca todas en `Program.cs`:
  ```csharp
  builder.Services
    .AddDesignacionesModule(builder.Configuration)
    .AddAulasModule(builder.Configuration)
    .AddPortalModule(builder.Configuration)
    .AddTareasModule(builder.Configuration);
  ```

### Logging

- **Serilog** para structured logging. Nunca `Console.WriteLine`.
- **No loggear PII** (DNIs, nombres, mails) en INFO/DEBUG. Si necesario, redactar o usar template-only logging.

### Tests

- xUnit para tests unitarios (path: `backend/tests/Modules.<X>.Tests/`).
- Tests de BR-\* deben linkearse en `docs/business-rules/<x>.md` sección "Test mapping".

## Comandos clave

```bash
# Build
dotnet build backend/ArsDocendi.slnx

# Run
dotnet run --project backend/src/ArsDocendi.Host

# Tests
dotnet test backend/ArsDocendi.slnx

# Format (también lo hace pre-commit)
dotnet format backend/ArsDocendi.slnx

# Add migration
dotnet ef migrations add <Nombre> \
  --project backend/src/Modules.<X> \
  --startup-project backend/src/ArsDocendi.Host \
  --context <X>DbContext
```

## Docs relevantes

- [docs/architecture/module-anatomy.md](../../../docs/architecture/module-anatomy.md)
- [docs/architecture/dependency-graph.md](../../../docs/architecture/dependency-graph.md)
- [docs/architecture/data-model.md](../../../docs/architecture/data-model.md)
- [docs/quality/golden-principles.md](../../../docs/quality/golden-principles.md)
