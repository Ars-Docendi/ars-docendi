# Modules.Asistente

Módulo del **asistente conversacional**: responde preguntas en lenguaje natural
sobre datos que ya viven en la base del sistema, en modo solo lectura.

Definición funcional y arquitectura objetivo en
[docs/product/designs/asistente-conversacional-definicion.md](../../../docs/product/designs/asistente-conversacional-definicion.md).
Change de planning: `openspec/changes/asistente-fundaciones/`.

## Estado

**Scaffold.** El módulo existe, el Host lo registra y no hace nada todavía. No
tiene controllers, servicios, `DbContext` ni migraciones.

Lo que falta y dónde va:

| Qué                                       | Dónde             |
| ----------------------------------------- | ----------------- |
| `GET /api/asistente/ping`                 | `Api/`            |
| Motor de consulta y validador de SQL      | `Application/`    |
| Cliente del proveedor de LLM y conexiones | `Infrastructure/` |

No hay carpeta `Domain/`, a diferencia de los otros módulos: el asistente no
tiene entidades propias — lee las de otros schemas y orquesta. Si algún día
aparece un agregado que le pertenezca, se crea entonces.

## Dependencias

Solo `ArsDocendi.Shared`. No referencia ningún otro módulo ni su propio
`.Contracts` (que nace vacío: ver
[Modules.Asistente.Contracts/README.md](../Modules.Asistente.Contracts/README.md)).

Tampoco declara EF Core, Npgsql ni MediatR: llegan cuando haya código que los use.

## Schema PostgreSQL

Ninguno propio. El asistente **lee** los schemas de otros módulos a través de dos
roles de solo lectura, con privilegios enumerados columna por columna.

> **Para quien implemente los `GRANT`**: el DDL de `database/asistente/*.sql` se
> embebe como recurso de **este** assembly, igual que
> `database/designaciones/*.sql` en `Modules.Designaciones.csproj`. Por eso ese
> trabajo depende de que este proyecto exista. El `ItemGroup` de embebido y su
> `Target` de validación se agregan en el mismo cambio que traiga el primer
> `.sql`, no antes: el `Target` falla el build si el glob no encuentra nada.
