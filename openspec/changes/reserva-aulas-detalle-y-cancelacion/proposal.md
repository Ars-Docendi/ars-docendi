## Why

Today the Aulas module resolves a request inside a double-click modal that differs by state, there is no way to cancel a reservation once it is approved, and a request cannot be linked or opened from anywhere else (the upcoming internal notifications need a stable address per request). The "Nueva solicitud" form also keeps stale data between openings, the shared dialog shell is the bare ui-lib modal, and the sidebar shows "Reserva de aulas" to people the route then redirects away. This is stage 1 of 3 of the internal notifications work: it lays the destination (a request detail page), the new transition and the data the notifications will need.

## What Changes

- **Backend (`Modules.Aulas`)**
  - New `GET /api/aulas/solicitudes/{id}`: visible to the owner (`aulas.solicitar`) or to `aulas.aprobar`; anyone else gets the same 404 `solicitud-not-found` as a missing id. The DTO gains `MotivoCancelacion` and `EsPropia` and carries `Docente`.
  - New `POST /api/aulas/solicitudes/{id}/cancelar-reserva` (`aulas.aprobar`, body `{ motivo }`): `aprobada -> cancelada`, releases the classroom, stores the reason. Blank reason is a 400 on `motivo`; any other state is a 409 `solicitud-no-aprobada`.
  - New incremental migration: `motivo_cancelacion TEXT NULL` (CHECK: only with `estado = 'cancelada'`, never blank) and `rol_solicitante TEXT NULL` (active role of the creating session, used by stage 2).
- **Frontend shared UI**: an app-level dialog shell (`ModalDeFormulario`) with focus management, dirty confirmation, pending lock and responsive sizing; shared form layout CSS; masked `CampoFecha` (`dd/mm/aaaa`) and `CampoHora` (`hh:mm`) with unit-tested helpers.
- **Frontend Aulas**
  - Route `/aulas/solicitudes/:id` and `DetalleSolicitudPage` (data card, actions panel, "Volver a Reserva de aulas").
  - Both tables: one click on any row opens the detail, the primary cell is a real link, the double click is removed. `ModalAsignarAula` and `ModalDetalleSolicitud` are removed; Rechazar, Cancelar solicitud and the new Cancelar reserva run on the new shell.
  - `ModalNuevaSolicitud` is rebuilt (sections, per-field validation, focus on the first invalid field) and mounted fresh on every open, which fixes the stale-form bug.
  - Sidebar: "Reserva de aulas" requires `aulas.solicitar` or `aulas.aprobar`, the same permissions as the route.
- **Supersedes** decision 8 ("Todas las solicitudes: doble click en la fila") of `openspec/changes/reserva-aulas/design.md`.

## Capabilities

### New Capabilities

- `detalle-solicitud-reserva-aula`: the request detail (endpoint and page), who can see it, the actions available per state and the cancellation of an approved reservation.
- `modal-formulario-app`: the behavior contract of the app dialog shell and of the masked date and time fields, plus the "Nueva solicitud" form that uses them.

### Modified Capabilities

- `navegacion-por-permisos`: the sidebar entry of a screen is shown under the same permissions as its route (motivated by "Reserva de aulas").

## Impact

- `backend/src/Modules.Aulas` (controller, service, DTOs, entity, DbContext mapping, snapshot), new SQL and wrapper under `database/aulas/` and `Infrastructure/Migrations/`.
- `frontend/src/features/aulas`, `frontend/src/shared/ui`, `frontend/src/app/shell/nav.ts`.
- Docs: `docs/architecture/api-contracts.md`, `data-model.md`, `domains/aulas.md`, `docs/product/designs/reserva-aulas-design-spec.md`.
- No cross-module change: `Modules.Aulas.Contracts` is untouched and the dependency graph keeps being a DAG. Rollback: the migration is additive (two nullable columns and one CHECK); the previous build ignores both columns.
- Upstreaming the shell and fields to `@ars-docendi/ui` is a follow-up; the library is not republished by this change.
