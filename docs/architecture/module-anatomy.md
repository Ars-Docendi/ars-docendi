# Anatomía de un módulo .NET

Cada contexto vive en `backend/src/Modules.<Modulo>/`. Cuando otro módulo necesita consumirlo, la superficie pública vive en `Modules.<Modulo>.Contracts`; un módulo sin consumidores puede no tener contratos todavía.

## Estructura vigente

```text
Modules.<Modulo>/
├── Api/                 # controllers y DTOs HTTP internos
├── Application/         # casos de uso (Portal)
├── Services/            # orquestación (Designaciones)
├── Domain/              # entidades y reglas puras
├── Repositories/        # consultas y persistencia EF
├── Infrastructure/      # DbContext y migrador
├── ModuleExtensions.cs  # registro en DI y MVC
└── Modules.<Modulo>.csproj

Modules.<Modulo>.Contracts/
├── Dtos/
├── Queries/ o Administracion/
└── Modules.<Modulo>.Contracts.csproj
```

No todos los módulos necesitan todas las carpetas. Aulas y Tareas, por ejemplo, sólo exponen hoy su ping y su infraestructura mínima; no se crean clases vacías para completar el dibujo.

## Flujo

La dirección habitual es `Controller → Service/Application → Repository → DbContext`. Los controllers traducen HTTP y aplican `[Authorize(Policy = ...)]`; las reglas y validaciones autoritativas viven en servicios o dominio. Un controller no consulta un repositorio directamente.

`ArsDocendi.Shared` contiene utilidades transversales y la persistencia común de `identity` y `audit`. Los módulos leen identidad mediante `IConsultasIdentity`; sólo la administración escribe sus catálogos.

## Registro

Cada módulo expone `Add<Modulo>Module(IConfiguration)`. El método registra DbContext, migrador, servicios concretos y sus contratos reales, y agrega el assembly a MVC. Las interfaces internas con una sola implementación no aportan una frontera y se evitan.

## Ping

Cada módulo expone `GET /api/<modulo>/ping`, que responde:

```json
{ "module": "<modulo>", "status": "ok" }
```

El endpoint no requiere una política de autorización y sirve como smoke test de composición. No incluye timestamp.

## Dependencias entre módulos

Un proyecto `Modules.X` nunca referencia la implementación `Modules.Y`. Si necesita una capacidad pública de Y, referencia `Modules.Y.Contracts`; el Host conecta las implementaciones. El grafo vigente está en [dependency-graph.md](./dependency-graph.md).
