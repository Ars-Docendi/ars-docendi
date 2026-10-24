---
name: dotnet-modules-guide
description: Convenciones .NET para módulos backend de Ars Docendi — fronteras por Contracts, capas Controller → Service/Application → Repository, autorización por permiso, SQL versionado y endpoint /api/{x}/ping obligatorio.
paths:
  - "backend/src/Modules.**"
  - "backend/src/ArsDocendi.Host/**"
  - "backend/src/ArsDocendi.Shared/**"
user-invocable: false
---

# .NET modules guide

Path-scoped: se auto-activa al tocar paths backend. La estructura vigente es la del código; esta guía sólo fija las reglas que el código no expresa. Antes de crear carpetas o archivos, imitá un módulo existente (`Modules.Portal` o `Modules.Tareas`) y leé [module-anatomy.md](../../../docs/architecture/module-anatomy.md).

## Reglas duras

- **Fronteras**: `Modules.X` referencia sólo `ArsDocendi.Shared`, `ArsDocendi.Migraciones` y proyectos `*.Contracts`. Nunca otro `Modules.Y`. Los edges vigentes están en [dependency-graph.md](../../../docs/architecture/dependency-graph.md) y `ArquitecturaIdentityTests` los verifica.
- **Contracts**: sólo DTOs, interfaces y tokens públicos; sin lógica ni I/O.
- **Identity**: se lee mediante `IConsultasIdentity`; sólo la administración la escribe.
- **Capas**: Controller (en `Api/`) → Service/Application → Repository → DbContext. Un controller nunca consulta un repositorio.
- **Autorización**: `[Authorize(Policy = Permisos.<...>)]` por permiso efectivo. **Nunca** `[Authorize(Roles = ...)]`. Sólo los pings y las rutas de ingreso (`/api/auth/*`) son anónimas.
- **Registro**: cada módulo expone `Add<Modulo>Module(IConfiguration)` en `ModuleExtensions.cs`, invocado desde `ArsDocendi.Host/Program.cs`.
- **Persistencia**: un schema por módulo. El DDL es SQL versionado embebido: se genera con `node scripts/migrations/scaffold.mjs <Contexto> <Nombre>`. **No usar `dotnet ef migrations add`.** Ver [database/README.md](../../../database/README.md).
- **Logging**: Serilog con templates; nunca `Console.WriteLine` ni PII (DNI, nombres, mails).
- **Tests**: en `backend/tests/ArsDocendi.IntegrationTests/<Modulo>/`, con los criterios de [golden-principles → Tests](../../../docs/quality/golden-principles.md#tests). Los tests de BR-\* se registran en `docs/business-rules/<modulo>.md`.

## Comandos

```bash
dotnet build backend/ArsDocendi.slnx
dotnet test backend/ArsDocendi.slnx
dotnet format backend/ArsDocendi.slnx
dotnet run --project backend/src/ArsDocendi.Host --no-build -- --validar-recursos
```
