# Anatomía de un módulo .NET

Cada contexto vive en `backend/src/Modules.<Modulo>/`. Cuando otro módulo necesita consumirlo, la superficie pública vive en `Modules.<Modulo>.Contracts`; un módulo sin consumidores puede no tener contratos todavía.

## Estructura vigente

```text
Modules.<Modulo>/
├── Api/                 # controllers y DTOs HTTP internos
├── Application/         # casos de uso (Portal, Tareas)
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

No todos los módulos necesitan todas las carpetas: Aulas, por ejemplo, no expone todavía interfaces públicas en su `Contracts`. No se crean clases vacías para completar el dibujo.

`Modules.Notificaciones` es el caso inverso: su `Contracts` sí lleva una interfaz con comportamiento en runtime (`INotificador`) más records y constantes, y no tiene `Application/`; usa `Services/` (`Notificador`, `ServicioNotificaciones`), `Repositories/`, `Domain/` e `Infrastructure/`.

## Flujo

La dirección habitual es `Controller → Service/Application → Repository → DbContext`. Los controllers traducen HTTP y aplican `[Authorize(Policy = ...)]`; las reglas y validaciones autoritativas viven en servicios o dominio. Un controller no consulta un repositorio directamente.

`ArsDocendi.Shared` contiene utilidades transversales y la persistencia común de `identity` y `audit`. Los módulos leen identidad mediante `IConsultasIdentity`; sólo la administración escribe sus catálogos.

## Registro

Cada módulo expone `Add<Modulo>Module(IConfiguration)`. El método registra DbContext, migrador, servicios concretos y sus contratos reales, y agrega el assembly a MVC. Las interfaces internas con una sola implementación no aportan una frontera y se evitan.

## Efectos posteriores al commit

Cuando una acción de negocio debe avisar a alguien (una notificación, un correo), lo hace **después** de confirmar su transacción y de forma _best effort_: la falla del aviso se registra sin datos personales y nunca deshace ni altera la acción. `ArsDocendi.Shared.Aplicacion.EmisionPosterior.IntentarAsync` envuelve el efecto (incluidas las lecturas para armarlo) y `INotificador` abre su propio scope de DI, así que no reutiliza el `DbContext` ni la transacción de quien lo llama. Si la acción corre dentro de una transacción externa (como las transiciones idempotentes de pedidos), el aviso se emite después del commit más externo.

## Ping

Cada módulo expone `GET /api/<modulo>/ping`, que responde:

```json
{ "module": "<modulo>", "status": "ok" }
```

El endpoint no requiere una política de autorización y sirve como smoke test de composición. No incluye timestamp.

## Dependencias entre módulos

Un proyecto `Modules.X` nunca referencia la implementación `Modules.Y`. Si necesita una capacidad pública de Y, referencia `Modules.Y.Contracts`; el Host conecta las implementaciones. El grafo vigente está en [dependency-graph.md](./dependency-graph.md).
