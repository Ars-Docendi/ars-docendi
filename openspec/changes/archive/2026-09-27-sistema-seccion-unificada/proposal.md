## Why

System administration is spread across three unrelated screens — «Dashboard del sistema»
(`/sistema`), «Registros de auditoría» (`/auditoria`) and «Uso del asistente»
(`/asistente/administracion`) — in two different nav groups, and none of them answers
"is everything working and what changed lately?" at a glance. The audit list ignores the
assistant's own administrative log (`asistente.auditoria_administracion`), so a budget or
maintenance change is invisible where administrators look for changes. The Claude Design
file `Sistema.dc.html` and Linear epic ARS-152 consolidate the three screens into one
«Sistema» section with permission-gated tabs; AGENTS.md rule 5 requires this change to be
ready before any code (ARS-153).

## What Changes

- **One «Sistema» section (ARS-154).** Single route `/sistema` with header «Sistema»,
  subtitle «Estado de los servicios, uso del asistente y registros de auditoría.», a
  secondary «Actualizar» button that refetches the active tab, and tabs Estado · Asistente ·
  Auditoría with a status dot each. Each tab is shown only with its own permission
  (`sistema.estado.ver`, `asistente.administrar`, `auditoria.ver`); the active tab lives in
  the URL hash. **BREAKING (UI):** `/auditoria` and `/asistente/administracion` stop being
  pages and redirect to `/sistema#auditoria` and `/sistema#asistente`; the three nav
  entries collapse into one «Sistema» entry visible with any of the three permissions.
- **Estado tab (ARS-155).** Summary banner (all available / N unavailable / all available
  with the assistant in maintenance), «Última comprobación», and cards grouped «Módulos»
  (Aulas, Tareas, Designaciones, Portal, Asistente) and «Infraestructura» (PostgreSQL), each
  with its pill, response time, note and per-component «Reintentar». The Asistente card is
  new: availability from its anonymous ping, maintenance state from the system status
  endpoint.
- **«Cambios recientes» (ARS-158).** The last four audit events under the Estado cards,
  each opening its detail in the Auditoría tab; shown only with `auditoria.ver`.
- **Asistente tab (ARS-156).** The existing usage page, embedded without its own page
  header. Its behavior is unchanged.
- **Unified audit feed (ARS-157).** `GET /api/administracion/auditoria` merges
  `audit.change_log` with `asistente.auditoria_administracion` (module «Asistente») into one
  ordered, paginated feed with source-prefixed event ids, a free-text `q` search over
  labels (never over values), and actions labelled Alta / Cambio / Eliminación.
  `changed_by` null with no request context is shown as «Proceso automático». Summaries
  include before → after values only when every value shown is a safe field. If the
  assistant source fails, the feed still returns the other events and says the result is
  partial. **BREAKING (API):** event `id` becomes a string; `actor` is replaced by `q`;
  `accionEtiqueta` values change.
- **Auditoría tab (ARS-159, ARS-160).** Search, period segmented control (Hoy / 7 días
  default / 30 días / Todo), «Acción» and «Módulo» chips (Identidad, Designaciones, Portal,
  Asistente — Aulas and Tareas have no audited tables and get no chip), «Más filtros»
  (Desde, Hasta, Tabla, Clave de fila), filters in the query string, a day-grouped list and
  an inline detail panel with «Qué cambió» and «Datos técnicos». **Behavior change:** the
  tab opens on the last 7 days instead of the full history.
- **`GET /api/administracion/sistema/estado` gains the assistant's maintenance state**, read
  through a new query in `Modules.Asistente.Contracts`, so a holder of only
  `sistema.estado.ver` sees it without `asistente.consultar`.
- The Claude Design «Datos de ejemplo» scenario switcher is canvas-only and is not built.
  Scope is desktop only.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `administracion-sistema`: the health dashboard becomes the Estado tab of a unified,
  permission-gated «Sistema» section (with the Asistente card, summary banner, per-component
  retry and «Cambios recientes»); the audit query becomes a unified feed across
  `audit.change_log` and the assistant's administration log, with label-only search,
  «Proceso automático», value-bearing safe summaries, period filter and an inline detail
  panel; the usage page is embedded as a tab and the old routes redirect.

`navegacion-por-permisos` is **not** modified: its requirements are generic (links derived
from effective permissions, no empty groups, route guards by permission, never by role
name). An entry and a guard that accept any one of several permissions still satisfy them;
the concrete «Sistema» entry and `/sistema` guard are specified in `administracion-sistema`.
The usage page's own capability (`asistente-panel-de-uso`) lives in the un-archived change
`asistente-administracion-de-uso` and names no frontend route or nav placement, so its
new placement is specified here and no delta against it is needed.

## Impact

- **Backend.** `ArsDocendi.Host/Administracion` (`ServicioAuditoria`, `RepositorioAuditoria`,
  `ServicioEstadoSistema`, DTOs, `AuditoriaController`, `EstadoSistemaController`);
  `Modules.Asistente.Contracts` gets its first types (administration-log query and
  maintenance-state query) and `Modules.Asistente` implements them. New project edges
  **ArsDocendi.Host → Modules.Asistente.Contracts** and **Modules.Asistente →
  Modules.Asistente.Contracts**; `Modules.Asistente.Contracts` goes from `huerfano` to
  `activo`, closing the open decision in its README. The graph stays acyclic (Contracts
  references nothing). No cross-module consumer is affected: the only consumer of both
  endpoints is the frontend, updated in the same diff.
- **Database.** A versioned `database/audit/` script ensures the `unaccent` extension so the
  audit search does not depend on the assistant's migration order. No table or column
  changes; `asistente.*` stays revoked from the assistant's read-only roles.
- **Frontend.** `features/sistema` is rebuilt as the tabbed section; `features/asistente`
  exports its usage panel without a page header and turns its `administracion` route into a
  redirect; `app/router.tsx` composes the two; `app/shell/nav.ts` and `RequirePermission`
  accept any-of permissions.
- **Docs.** `docs/architecture/api-contracts-administracion.md`, `api-contracts.md` (the
  estado field), `dependency-graph.md`, `backend/manifiesto-de-aristas.json`,
  `data-model.md` (unaccent in `audit`), `Modules.Asistente.Contracts/README.md`, and
  `docs/product/designs/administracion-sistema-design-spec.md` rewritten for the tabbed
  section.
- **In-flight changes.** This delta builds on the version of «Consulta paginada y de solo
  lectura de auditoría» in `mejorar-dashboard-y-auditoria` and contradicts one of its
  scenarios on purpose (see design D6). Both `mejorar-dashboard-y-auditoria` and
  `asistente-administracion-de-uso` must be archived before this change is archived.
- **Rollback.** Revert backend and frontend together; no data is written or migrated. The
  `unaccent` script is idempotent and harmless to leave in place.
- No institutional regulation is involved: no BR-* rule is added or changed.
