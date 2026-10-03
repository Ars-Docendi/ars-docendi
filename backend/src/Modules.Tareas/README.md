# Modules.Tareas

Implementa **RF-04 — Seguimiento de Tareas**: tablero tipo Trello con semáforo de vencimiento.

Ver [dominio Tareas](../../../docs/architecture/domains/tareas.md).

## Endpoints

- `GET /api/tareas/ping` — smoke test.
- Tareas, relaciones, comentarios, candidatos a Responsable y Proyectos bajo `/api/tareas/**`: ver [contratos de API](../../../docs/architecture/api-contracts.md). Permisos `tareas.ver` / `tareas.gestionar` / `proyectos.gestionar`.

Sigue la anatomía de `Modules.Portal`: `Api` → `Application` → `Repositories` → `Domain`; DDL en `database/tareas/`. Lee identity solo a través de `IConsultasIdentity`.

## Schema PostgreSQL

`tareas`
