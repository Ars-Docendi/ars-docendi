## Context

See `proposal.md` for the motivation. State before this change: `Modules.Aulas` has a request lifecycle without a per-request read endpoint or a way to leave `aprobada`; the frontend resolves requests in `ModalAsignarAula` / `ModalDetalleSolicitud` opened by double click, and all dialogs use the ui-lib `Modal`. The app root renders with `zoom: var(--ui-zoom)` (1.125) and the ui-lib already divides viewport units by `--ui-zoom` in its modal stage; a dialog that does not do the same overflows. Receipt: this change is stacked on `reserva-aulas` (#41) and on the active-role work (#53), where `ICurrentUser.Roles` carries a single element, the active role of the session.

This is stage 1 of 3. Stage 2 adds the notifications themselves (using `rol_solicitante` and the detail route as link target); stage 3 adds validated origin ("Volver a ...") and system-wide navigation. Nothing here is a stub for them: every added column, route and component is used by this stage.

## Goals / Non-Goals

**Goals:**

- A stable, permission-checked address for one request, and the missing `aprobada -> cancelada` transition with a stored reason.
- Persist the creating role now, so stage 2 does not need a backfill.
- Fix the stale "Nueva solicitud" form and the nav/route permission mismatch.
- A reusable dialog shell and masked date/time fields with tested behavior.

**Non-Goals:**

- Notifications, their copy, bell or inbox (stage 2). The cancel-reservation dialog therefore does not promise a notification.
- Validated origin for "Volver" and list state in the URL (stage 3): "Volver a Reserva de aulas" always goes to `/aulas`.
- Publishing the shell and fields in `@ars-docendi/ui` (see decision 5).
- Reopening a cancelled reservation, or cancelling a reservation as its owner.

## Decisions

### 1. Read access is decided in the service, answered as 404

`GET /api/aulas/solicitudes/{id}` carries `[Authorize]` but no permission policy: the caller can be an owner with `aulas.solicitar` or an approver with `aulas.aprobar`, and the rule "owner of this row" cannot be expressed by a policy. The service loads the row and returns the same `solicitud-not-found` when the caller is neither (a 403 would confirm that the id exists). The owner also needs `aulas.solicitar` at that moment, so revoking the permission closes the door. A role with neither permission therefore gets 404, not 403, on this single route.

Alternative: a policy accepting either permission plus an ownership check afterwards. Rejected: it would answer 403 to a role without Aulas permissions on an existing id and 404 on a missing id, which discloses existence.

### 2. `EsPropia` is computed by the backend; `Docente` is always filled in the detail

The frontend must decide between "Tu solicitud" (owner) and the resolver panel. Inferring ownership client-side from names or ids would depend on identity data the page does not have, so the DTO carries `EsPropia`. The detail always returns `Docente` (the owner sees her own name, harmless and consistent). `Mias` marks `EsPropia = true`; `Todas` computes it per row; the mutations that return a DTO (`asignar`, `rechazar`) leave the default `false`, because their callers are approvers and the page refetches the detail anyway.

### 3. Cancelling a reservation clears the classroom

`solicitudes_reserva_aula_si_aprobada` already forces `aula_asignada IS NULL` outside `aprobada`, so the classroom is released as a consequence of the existing CHECK, not as an extra rule. The released classroom is shown in the confirmation dialog before confirming and is no longer stored afterwards (stage 2 will take it from the request before the change if the copy needs it).

The new CHECK `solicitudes_reserva_motivo_cancelacion_si_cancelada` allows `motivo_cancelacion` only with `estado = 'cancelada'` and never blank. A cancellation by the owner of a pending request leaves it `NULL`: that cancellation carries no reason today and this change does not add one.

### 4. `rol_solicitante` comes from the first role of the session

`rol_solicitante TEXT NULL` stores `usuarioActual.Roles.FirstOrDefault()` on creation. Under #53 the session has exactly one role, so the first one is the active one. Existing rows stay `NULL`; stage 2 must treat `NULL` as "unknown role" and use a neutral link target. No backfill is invented. The column has no CHECK against the role catalog: roles are data and can be renamed, and the value is only a hint for routing.

### 5. The dialog shell and fields live in the app, not in the ui-lib

The library is not republished tonight, so the shell is `frontend/src/shared/ui/ModalDeFormulario.tsx` and fields `CampoFecha`, `CampoHora`, `camposFechaHora.ts`. They reuse the library classes and tokens (`adoc-modal-stage`, `adoc-modal`, `adoc-field`, `adoc-input`, `adoc-btn`, `--color-*`, `--neutral-*`) and refine them through the `--app` modifier (`adoc-modal--app`), so the library's own modals keep their look. **Upstreaming these to `@ars-docendi/ui` is a follow-up**; the CSS of the shell and the form layout was written to move as-is.

Zoom: viewport-dependent sizes are divided by `--ui-zoom` (`min(600px, 100vw / var(--ui-zoom, 1) - 32px)`, `90vh / var(--ui-zoom, 1)`), exactly as the library's stage does, because `vw`/`vh` are not zoomed and the box is.

Behavior details:

- The shell is mounted only while open (the parent renders it conditionally). Each opening is a fresh instance, which is also what resets forms (decision 6).
- Esc and the Tab trap are handled by a single document listener that acts on the top of a stack of layers (`capas.ts`), instead of one listener per dialog, so a dialog below never reacts. The ui-lib `Modal` keeps its own listener and is not mixed with this one on the same screen.
- Focus return goes to the opener captured at mount; if it has left the DOM, to the page `h1` (given `tabindex="-1"` on demand). Page-level focus on route change is not handled here.
- Visibility for the Tab trap uses `checkVisibility()` when the browser has it, so the trap does not depend on layout APIs jsdom lacks.
- "Dirty" is decided by the caller (`sucio`); the shell only owns the inline question.

Alternative: extend the library `Modal` through its `className`. Rejected: it cannot own the focus return, the dirty question, the pending lock or the overlay press/release rule, which are props of the shell, not of a style.

### 6. "Nueva solicitud" is mounted per opening and owns its mutation

The stale-form bug came from a permanently mounted component whose state survived `open = false` (the reset ran only on an `open = true` that never reached it). The regression test (`IndexPage.test.tsx`) was written first and failed for that reason. The fix is structural: the page renders the dialog only while open, and the dialog owns its `useCrearSolicitud` mutation, so neither the fields nor a previous server error can outlive a closing. Pure validation lives in `validacionNuevaSolicitud.ts` with its own unit tests; the component test covers only wiring.

### 7. Reason dialogs share one component

"Rechazar solicitud" and "Cancelar reserva" are the same interaction (a destructive confirmation with a mandatory reason, dirty protection and focus on the field). They are two thin wrappers over `ModalConMotivo` that only differ in copy. The cancel-reservation dialog shows an info alert "Qué pasa al cancelar" whose text is limited to what happens in this stage: the classroom is released and the teacher sees the reason.

### 8. Table navigation reuses `propsFilaClickeable`; the link is a router `Link`

Rows use the existing `propsFilaClickeable` (click on the row or Enter with the row focused; clicks on links, buttons and inputs inside are ignored). The primary cell is a `Link` with an explicit accessible name, so keyboard, middle-click and the status bar work. This supersedes decision 8 of `reserva-aulas/design.md` (double click, different modal per state, no action column): there is now one gesture for all rows and the resolution happens on the detail page.

### 9. Sidebar uses the permissions of the route

`/aulas` is guarded by `aulas.solicitar` or `aulas.aprobar`; the sidebar entry required `aulas.ver`, so a coordinator with only `aulas.ver` saw a link that redirected. The entry now declares the same two permissions (`NavItem.permiso` already accepts a list). `aulas.ver` stays in the catalog for the future calendar and is not used by any screen today.

### 10. Prototype behaviors deliberately left out of this stage

Filters and sort in the URL, the highlight of an entity reached from a notification, the unsaved-changes guard on route change and the validated "Volver" origin belong to stages 2 and 3; none is a visible control in this stage, so nothing in the UI is a placeholder.

## Risks / Trade-offs

- [Two Esc/focus systems coexist (this shell and the library `Modal`)] -> The Aulas screens use only the shell; the library modal keeps working on the screens that use it. Moving them is part of the upstream follow-up.
- [404 instead of 403 on the detail route for roles without Aulas permissions] -> Intended (no existence leak); the route guard already redirects them in the UI. Documented in `api-contracts.md`.
- [`rol_solicitante` is NULL for old rows] -> Stage 2 treats NULL as unknown; documented in `data-model.md`.
- [`EsPropia` is false in the DTO returned by `asignar`/`rechazar`] -> Their only caller refetches the detail; if a future caller needs it, compute it there.
- [Shell CSS duplicates a few library rules] -> Scoped by `--app`; collapses when upstreamed.

## Migration Plan

1. Apply `20261102030524_MotivoCancelacionYRolSolicitanteAulas` (two nullable columns, one CHECK). Existing rows already satisfy the CHECK (their `motivo_cancelacion` is NULL).
2. Deploy backend and frontend together; the previous frontend ignores the new fields and the previous backend ignores the new columns.
3. Rollback: redeploy the previous build. The columns stay (additive); there is no automatic down-migration, as for every other migration of the project.
