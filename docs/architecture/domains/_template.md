# Domain: &lt;modulo&gt;

## Propósito

Una o dos oraciones sobre qué problema institucional resuelve este módulo.

## Roles que interactúan

- &lt;Rol&gt; — qué hace en este módulo

## Bounded context

- **Pertenece**: qué conceptos viven dentro de este módulo y son canónicos acá.
- **No pertenece**: qué conceptos NO viven acá, dónde viven (otro módulo, externo).

## Entidades principales

| Entidad | Descripción | Schema/Tabla       |
| ------- | ----------- | ------------------ |
| ...     | ...         | `<schema>.<tabla>` |

## API pública (contract)

Lo que expone `Modules.<Modulo>.Contracts` para otros módulos:

| Interfaz         | Métodos      | Consumido por   |
| ---------------- | ------------ | --------------- |
| `I<Modulo>Query` | `Obtener...` | (otros módulos) |

## Endpoints HTTP

Lista resumida (detalle en `../api-contracts.md`):

| Método | Path                 | Rol       | Descripción  |
| ------ | -------------------- | --------- | ------------ |
| GET    | `/api/<modulo>/ping` | (anónimo) | Health check |

## Reglas de negocio (BR-\*)

Linkear a `docs/business-rules/<modulo>.md`.

## Dependencias

- **Hacia adentro**: qué necesita de otros módulos (vía Contracts).
- **Hacia afuera**: quién consume este módulo (vía sus Contracts).
- **Externas**: servicios externos (Azure AD, API Guaraní, etc.).

## Specs activas

Listar specs en `openspec/specs/` que tocan este módulo. (Autogenerable a futuro.)

## Decisiones registradas

Decisiones técnicas/producto del módulo que no son obvias del código.

- _(decisión)_ — _(motivo)_
