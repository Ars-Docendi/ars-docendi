# Module anatomy (.NET)

Cada **módulo** es un bounded context con una **única superficie pública**: el proyecto `Modules.<Modulo>.Contracts`.

## Layout de directorios

```
backend/src/
├── ArsDocendi.Host/                              # Composition root (referencia todos los Contracts + Modules)
├── ArsDocendi.Shared/                            # Utilidades puras transversales
├── Modules.<Modulo>/                             # INTERNO — implementación
│   ├── Modules.<Modulo>.csproj
│   ├── Controllers/
│   │   └── <Modulo>Controller.cs                 # Endpoints HTTP, validación, status codes
│   ├── Services/
│   │   └── <Modulo>Service.cs                    # Reglas de negocio, orquestación
│   ├── Repositories/
│   │   └── <Modulo>Repository.cs                 # Persistencia, queries
│   ├── Domain/
│   │   ├── Entities/                             # Entidades EF Core
│   │   └── ValueObjects/
│   ├── Infrastructure/
│   │   └── <Modulo>DbContext.cs                  # EF Core context del schema del módulo
│   ├── Internal/                                 # PRIVADO — mappers, helpers; NO importable desde fuera
│   └── ModuleRegistration.cs                     # IServiceCollection extension para registrar el módulo
└── Modules.<Modulo>.Contracts/                   # PÚBLICO — única superficie cross-module
    ├── Modules.<Modulo>.Contracts.csproj
    ├── DTOs/                                     # Public DTOs
    ├── Interfaces/                               # Service interfaces para DI cross-module
    └── Events/                                   # Domain events públicos (si aplica)
```

## Reglas de capas

Dirección permitida:

`Controller` → `Service` → `Repository`

- **Controllers**: HTTP, validación de DTOs, status codes, autorización por rol via `[Authorize(Roles = ...)]`.
- **Services**: reglas de negocio, orquestación, transacciones.
- **Repositories**: persistencia, queries EF Core.

**Prohibido**: controller → repository directo.

## Uso cross-module (interacción entre módulos)

1. Importar **solo** desde `Modules.<Otro>.Contracts` (interfaces y DTOs).
2. Resolver el servicio del otro módulo via DI usando la interfaz pública.
3. **Nunca** referenciar `Modules.<Otro>` directamente.

Ejemplo (Designaciones consume Portal para validar docente existente):

```csharp
// En Modules.Designaciones — REFERENCIA solo Modules.Portal.Contracts
using ArsDocendi.Modules.Portal.Contracts.Interfaces;

public class DesignacionesService(IPortalDocenteQuery portalQuery) {
    public async Task CrearDesignacionAsync(...) {
        var docente = await portalQuery.ObtenerDocentePorIdAsync(...);
        // ...
    }
}
```

## Código compartido

- **Utilidades puras transversales**: `ArsDocendi.Shared` (cosas como `Result<T>`, helpers de fechas).
- **Si un helper lo usa un solo módulo**: dentro de `Modules.<Modulo>/Internal/`.
- **DTOs públicos compartidos entre módulos**: no aplica — cada módulo expone los suyos en su `.Contracts`. Si dos módulos necesitan el mismo DTO, indica que pertenece a un tercer concepto.

## Smoke test obligatorio

Cada módulo expone `GET /api/{modulo}/ping` que retorna `200 OK` con el nombre del módulo y un timestamp. Sirve como health check y como verificación de que el módulo está registrado en el Host.

```csharp
[ApiController]
[Route("api/{modulo}")]
public class <Modulo>Controller : ControllerBase {
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { module = "<modulo>", timestamp = DateTimeOffset.UtcNow });
}
```

## Registración en el Host

Cada módulo expone un método de extensión `IServiceCollection.Add<Modulo>Module()` que el Host invoca:

```csharp
// En ArsDocendi.Host/Program.cs
builder.Services
    .AddDesignacionesModule(builder.Configuration)
    .AddAulasModule(builder.Configuration)
    .AddPortalModule(builder.Configuration)
    .AddTareasModule(builder.Configuration);
```

El método se define en cada `Modules.<Modulo>/ModuleRegistration.cs` y registra servicios, DbContext, configuración del módulo.
