---
name: architecture-proposal
description: Bootstrap los docs de arquitectura (stack, module-anatomy, dependency-graph, api-contracts, data-model, infrastructure, domains/) desde la descripción del sistema. Correr después de /init-project, antes de /create-module o /add-feature. Una sola vez por proyecto.
argument-hint: [<descripción libre de la arquitectura>]
---

# Architecture proposal

**Source of truth:** [docs/workflows/architecture-proposal.md](../../../docs/workflows/architecture-proposal.md). Leerlo.

## Produce

| Archivo                                  | Rol                                    |
| ---------------------------------------- | -------------------------------------- |
| `docs/architecture/stack.md`             | Apps + packages + decisiones           |
| `docs/architecture/module-anatomy.md`    | Layout de módulo .NET                  |
| `docs/architecture/dependency-graph.md`  | Mermaid DAG + edge registry            |
| `docs/architecture/api-contracts.md`     | Base URL, auth, error shape, endpoints |
| `docs/architecture/data-model.md`        | ORM, entidades, ER, PII                |
| `docs/architecture/infrastructure.md`    | Ambientes, deploy, monitoring          |
| `docs/architecture/domains/<dominio>.md` | Uno por bounded context                |

## Flujo

1. **Pre-condición**: `docs/product/brief.md` debe tener contenido real (post `/init-project`). Si no, parar y pedir `/init-project` primero.
2. **Aclarar** arquitectura: runtime topology, stack, dominios, API pública, persistencia, infra.
3. **Redactar** los docs. Sin placeholders sin completar. Marcar gaps con `_(needs owner input)_`.
4. **Mostrar al equipo** + iterar feedback.
5. **Sobre aprobación**:
   - Branch `chore/architecture-proposal`
   - Commit `docs/architecture/` completo
   - PR a `develop`

## Hard rules

- **Solo** editar `docs/architecture/`. Sin código, sin otros paths.
- **Una vez por proyecto**. Si `stack.md` tiene contenido real, confirmar antes de sobreescribir.
- Cada edge en `dependency-graph.md` debe estar justificado por la descripción.

## Arguments

`$ARGUMENTS` — descripción de la arquitectura propuesta.
