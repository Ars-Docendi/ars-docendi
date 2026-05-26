---
name: create-module
description: Scaffold de un nuevo módulo .NET (Modules.X + Modules.X.Contracts) en el monorepo siguiendo la convención del proyecto. Crea proyectos, agrega a la solution, referencia desde el Host, expone endpoint /api/{x}/ping obligatorio, y crea slice frontend si aplica.
argument-hint: [<NombreEnPascalCase>]
---

# Create module

**Source of truth:** [docs/workflows/create-module.md](../../../docs/workflows/create-module.md). Leerlo.

## Cuándo usar

- `/add-feature` o `/plan-feature` reveló necesidad de un bounded context backend nuevo.
- No encaja en los 4 módulos existentes (Designaciones, Aulas, Portal, Tareas).

## Produce

1. `backend/src/Modules.<X>/` con estructura (Controllers, Services, Repositories, Domain, Infrastructure, Internal, ModuleRegistration).
2. `backend/src/Modules.<X>.Contracts/` con DTOs + interfaces (sin lógica).
3. Referencias agregadas a `backend/ArsDocendi.slnx`.
4. Referencia desde `ArsDocendi.Host` + invocación de `Add<X>Module()` en `Program.cs`.
5. Endpoint `GET /api/<x>/ping` funcional.
6. `frontend/src/features/<x>/` slice mínimo (si tiene UI).
7. `docs/architecture/domains/<x>.md` desde `_template.md`, llenado.
8. `docs/business-rules/<x>.md` desde `_template.md` (vacío, listo para BR futuras).

## Reglas duras

- **Nunca** referenciar `Modules.<Otro>` directamente — usar siempre `Modules.<Otro>.Contracts`.
- `Modules.<X>.Contracts`: **sin lógica**, solo DTOs/interfaces/tokens.
- Endpoint `/api/<x>/ping` es **obligatorio**.
- `ModuleRegistration.cs` debe existir y registrar al menos el DbContext + el service principal.
- Documentar en `dependency-graph.md` cualquier edge nuevo cross-module.

## Comandos clave

```bash
# Crear proyectos
cd backend/src
dotnet new classlib -n Modules.<X>.Contracts -o Modules.<X>.Contracts
dotnet new classlib -n Modules.<X> -o Modules.<X>

# Referencias
cd Modules.<X>
dotnet add reference ../Modules.<X>.Contracts/Modules.<X>.Contracts.csproj
dotnet add reference ../ArsDocendi.Shared/ArsDocendi.Shared.csproj

# Solution
cd backend
dotnet sln ArsDocendi.slnx add src/Modules.<X>/Modules.<X>.csproj
dotnet sln ArsDocendi.slnx add src/Modules.<X>.Contracts/Modules.<X>.Contracts.csproj

# Host reference
cd backend/src/ArsDocendi.Host
dotnet add reference ../Modules.<X>/Modules.<X>.csproj
dotnet add reference ../Modules.<X>.Contracts/Modules.<X>.Contracts.csproj
```

## Arguments

`$ARGUMENTS` — nombre del módulo en PascalCase (e.g. `Examenes`).
