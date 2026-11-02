## Why

Nothing in the system tells a person that something happened to a request of theirs. A room is assigned, a designation request is returned, a task is reassigned, and the only way to find out is to open the right screen at the right time. Several dialogs already promise "se notifica (in-app)" without keeping the promise. This is stage 2 of 3 of the internal notifications work: stage 1 laid the destinations (request detail page, `rol_solicitante`); this stage adds the notifications themselves; stage 3 adds validated origin ("Volver a ...") and system-wide navigation. Email is another teammate's work and out of scope.

## What Changes

- **Backend, new module `Modules.Notificaciones` (+ `Modules.Notificaciones.Contracts`)**
  - Schema `notificaciones`, table `notificaciones.notificaciones` (one row per recipient user), `GET /api/notificaciones/ping`.
  - API scoped to the session user and active role: unread count, paginated list (`NO_LEIDAS` / `LEIDAS`), mark one as read (idempotent), mark several as read (`{ids}` or `{todas, hasta?}`).
  - Contract `INotificador`: emitting modules hand over recipients plus an immutable title and message; delivery is best effort and never reaches the business flow.
  - `IConsultasIdentity` gains efficient queries: active users holding a role in a scope, and persona to usuario.
- **Emitters, always after the business commit**
  - Aulas: room assigned, room updated (only when it changed), request rejected, reservation cancelled, request cancelled by the teacher (to `administrativo` and `secretaria`).
  - Designaciones: request passed to the next reviewer, rejected (to the Jefes of the materia), returned (to the owner role in scope).
  - Tareas: assigned or reassigned, state changed, commented.
- **Frontend, new feature `features/notificaciones`**
  - Bell in the sidebar foot (between "Centro de ayuda" and the user card) and in the mobile top bar, with unread badge; left drawer panel with "No leídas" / "Leídas" tabs, date groups, infinite scroll, bulk selection, optimistic mark as read with rollback and toast.
  - Clicking a notification marks it read, closes the panel, navigates to the entity and briefly highlights it.
  - App-level toast host (the ui-lib `Toast` is only a card).
- **Copy fixes**: the Cancelar reserva dialog states that the teacher is notified; the designaciones confirmation dialog promises become true and its "Devolver" copy stops saying "Borrador".

## Capabilities

### New Capabilities

- `notificaciones-internas`: persistence, visibility rules, API and emission rules (who is notified, when, with which profile).
- `campana-y-panel-notificaciones`: the bell, the panel and their behavior.

### Modified Capabilities

- `barra-superior`: the top bar may show the functional notifications bell.

## Impact

- New projects `backend/src/Modules.Notificaciones` and `Modules.Notificaciones.Contracts`; new edges `Aulas`, `Designaciones`, `Tareas` -> `Modules.Notificaciones.Contracts`, `Host` -> `Modules.Notificaciones`. The graph stays a DAG: `Notificaciones` references only `Shared`, `Migraciones` and its own Contracts.
- New migration (context `Notificaciones`, first one: it creates the schema). `ArsDocendi.Shared` (`IConsultasIdentity`) gets two read queries.
- `Modules.Aulas`, `Modules.Designaciones`, `Modules.Tareas`: services call the notifier after commit.
- Frontend: `features/notificaciones`, `app/AppLayout`, `app/shell`, `features/designaciones` (confirmation dialog copy), `features/aulas` (cancel dialog copy and highlight).
- Docs: `dependency-graph.md`, `module-anatomy.md`, `api-contracts.md`, `data-model.md`, new `domains/notificaciones.md`, `infrastructure.md` smoke list, design specs of reserva de aulas and designaciones.
- Rollback: the migration is additive (a new schema). Removing the module leaves an unused schema; the emitters are best effort, so reverting them cannot affect the business flows.
- Known limits, accepted by the team: no outbox (a failed emission is lost and only logged), notices are never deleted, no email.
