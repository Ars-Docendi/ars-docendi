## Context

See `proposal.md`. Stage 1 left the destinations ready (`/aulas/solicitudes/:id`, `rol_solicitante`). The team decisions, final and not reopened here: in-app only; best-effort emission after commit, no outbox; notices are never deleted; a notice is visible only under the active profile it was addressed to (Tareas notices have no profile and show under any); fan-out at event time with one row per user holding the role and scope, excluding the actor; several Jefes of a materia each get their own row and read state; a persona without usuario is skipped and logged; the unread count polls every 60 s and refetches on focus when stale (30 s), and the lists are fetched only while the panel is open.

The prototype (`notificaciones.html`) is the acceptance reference for copy, icons, states and behavior; the original design reference is the OSPESE notifications module. Autonomous work: every decision below was taken without review and is recorded here so it can be challenged.

## Goals / Non-Goals

**Goals:** a new module with a stable contract, a recipient model that never filters all users in memory, emitters that cannot hurt the business flow, and a panel with the prototype's behavior.

**Non-Goals:** email, an outbox or retries, deletion or archiving, preferences per user or per event, push/WebSocket delivery, validated origin for "Volver" (stage 3), notifying on `Enviar`/`Reenviar`/`Cancelar` of designations (the prototype does not).

## Decisions

### 1. Module shape and the notifier contract

`Modules.Notificaciones` follows `create-module` and `Modules.Aulas` as the minimal template, plus the layers it needs (`Services`, `Repositories`, `Domain`). The public surface in `Modules.Notificaciones.Contracts` is one interface, `INotificador`, and plain records:

- `AvisoNuevo`: `TipoEvento`, `TipoEntidad`, `EntidadId`, `RolDestino?`, `Titulo`, `Mensaje`, `Contexto` (string-keyed map). The emitting module builds the text with the prototype's exact wording, so the snapshot is immutable and the notifications module never reads another module's data.
- `DestinoNotificacion`, a closed set of selectors: a usuario, a persona, or a role in a scope (`materia`, `carrera` or global). The emitter says _who_, the notifier resolves _which users_.

Alternative: emitters resolve user ids themselves and pass a list. Rejected: three modules would each repeat persona-to-usuario resolution, inactive filtering, deduplication, actor exclusion and the "persona without usuario" log; any bug would exist three times. Contracts stays free of logic (records and one interface).

### 2. Best effort lives in the implementation, in its own scope

`Notificador.NotificarAsync` never throws: it resolves recipients and writes inside a fresh DI scope (`IServiceScopeFactory`), therefore with its own `NotificacionesDbContext` and `IdentityDbContext`. It does not reuse the caller's contexts, so a failed or disposed context or an aborted transaction of the caller cannot affect it, and its failures cannot leave the caller's context dirty. The actor is read from `ICurrentUser` before opening the scope. The write ignores the request's cancellation token and uses its own 10 s timeout: after the business commit, a client disconnecting must not drop the notice. Every failure is logged once with event type, entity id and error type; never with titles, messages, names or emails. A caller that is itself an `INotificador` replacement in a test is outside this guarantee; the guarantee belongs to the shipped implementation and is tested by breaking its storage.

### 3. Recipients are resolved in SQL

`IConsultasIdentity` gains two reads: `ObtenerUsuarioIdsConRolAsync(codigoRol, materiaId, carreraId)` (active users with an active system role assigned in that scope; both null means global) and `ObtenerUsuarioIdsDePersonasAsync(personaIds)`. Both are single queries over `user_roles`/`roles`/`users` honoring the soft-delete query filter. Unlike `AsignacionesDe`, they never narrow to the session's active assignment: a recipient is a third party and what matters is that they _hold_ the role; whether they see the notice is decided later by the profile filter. Scope semantics follow #53's assignment model: materia roles carry `materia_id`, carrera roles `carrera_id`, global roles neither.

### 4. Storage

`notificaciones.notificaciones(id, destinatario_usuario_id, rol_destino, tipo_evento, tipo_entidad, entidad_id, titulo, mensaje, contexto jsonb, leida_en, creada_en)` with one index, `(destinatario_usuario_id, leida_en, creada_en DESC)`, as requested: the unread count and both lists filter on the recipient and on `leida_en` and order by `creada_en`, so a second index was not added.

- **FK**: `destinatario_usuario_id REFERENCES identity.users(id) ON DELETE RESTRICT`. Same documented precedent as `aulas.solicitudes_reserva` (identity is transversal infrastructure, rule 4). `entidad_id` has no FK: it points to three different modules' tables.
- **CHECKs**: `tipo_entidad` is one of the three entity kinds, title/message/event type are not blank, `rol_destino` is not blank when present and `contexto` is a JSON object.
- **No `audit.attach`** (decision). `audit.change_log` records who changed what in business tables; here (a) every row is derived data whose cause is already audited in the emitting module, (b) the only mutation after insert is the read instant, which would add one audit row per click and several per bulk read, and (c) the message snapshots contain names and free text that the audit would copy a second time. Cost: no trail of who read what, which is not a requirement.
- `rol_destino` is free text validated by the emitters (role codes are data and can be renamed), exactly like `rol_solicitante`.

### 5. API and visibility

All routes carry `[Authorize]` and take the user from `ICurrentUser`; none reads a user id from the request. The active profile is the first element of `ICurrentUser.Roles` (single element under #53, same rule as `rol_solicitante`). The visibility predicate `destinatario = yo AND (rol_destino IS NULL OR rol_destino = perfil)` is one reusable query filter applied by every read and write, so "another profile" and "another user" are indistinguishable from "missing" (404). Bulk marking runs as one `UPDATE ... WHERE visible AND leida_en IS NULL AND selection` and returns the affected count; ids that are not visible are silently ignored. Pagination is `OFFSET/LIMIT` (page sizes are small, ordering `creada_en DESC, id DESC` is total).

Error contract: validation errors use the existing Problem Details mapping (`ExcepcionAplicacion` with `TipoErrorAplicacion.Validacion`), code `notificaciones-seleccion-invalida` for bulk selection, the prototype's messages verbatim.

### 6. Where each module emits

After `GuardarCambiosAsync`/`CommitAsync` and never inside a transaction. Copy builders sit next to the emitter (`Modules.<X>` owns its wording), as plain functions with the prototype's strings. Aulas reads the released room and the previous room _before_ mutating. Designaciones emits after `unidadDeTrabajo.EjecutarEnTransaccionAsync` returns (which commits). Tareas emits after `transaccion.CommitAsync`.

Recipients and `rol_destino`, from `MaquinaEstadosPedido`: `Aceptar` goes to the next stage role (Secretaría or Decanato, global; `en_lote` has no reviewer and sends nothing); `Rechazar` goes to the Jefes of `pedido.MateriaId`; `Devolver` goes to the owner role of the stage returned from (`PropietarioDeDevolucion`): Jefe by materia, Coordinador by `pedido.CarreraId`, Secretaría global. `rol_destino` is that role.

### 7. Frontend data layer

`features/notificaciones` follows `frontend-remote-data.md` (api adapter, hooks with a key factory, `types.ts`). Every key includes the active role code (`["notificaciones", rol, ...]`), so a role change is a different query. The count uses `refetchInterval: 60_000` and `refetchOnWindowFocus` with `staleTime: 30_000`; the two lists are infinite queries enabled only while the panel is open on that tab. The count query keeps running with the panel closed (it is the bell).

**Why this optimism differs from "no optimism for transitions".** That rule exists because a business transition (accept, return, activate) changes what other people can do and a wrong optimistic state misrepresents a confirmed fact. Marking a notification as read changes only the viewer's own reading state, is idempotent and reversible by the server on the next refetch, affects no other user and has no business consequence if it is briefly wrong. The cost of waiting (a badge and a list that lag 300 ms behind the click, in a list people clear in bulk) is real; the cost of being wrong is a toast and a rollback. So `marcarLeida` and `marcarLeidas` are optimistic with snapshot, rollback and toast; every query is cancelled on mutate and refetched on settle. Everything else in the product stays pessimistic.

### 8. Shell integration

The panel is rendered by `AppLayout` (one instance) and positioned `absolute` over `.adoc-app` with `left` equal to the sidebar width, so it opens beside the sidebar and covers everything on narrow screens; `.adoc-app` becomes `position: relative`. The sidebar width is read from the collapsed state that `AppLayout` already owns. The bell is a button in `Sidebar` (foot, between help and user card) and in `TopBar`; both share one component and one hook. Esc and the Tab trap go through `capas.ts`, so the panel is one more layer on the same stack as the form dialogs; the panel closes on an outside press (the bell toggles it by itself). Focus goes to the close button on open and back to the bell on close; if the user navigated away because of a click, focus is not stolen back from the new page (the stage-1 rule: page focus on route change belongs to the page).

### 9. Toast host

The ui-lib `Toast` renders a card only. `shared/ui/Toaster.tsx` is a tiny external store plus a fixed host (`role="alert"` for errors, `status` otherwise, auto-dismiss 7 s, close button) mounted once in `AppLayout`. It lives in `shared/ui` because it is not notification-specific; the CSS follows the library tokens.

### 10. Navigation and highlight

Click: mark read (optimistic), close the panel, `navigate(path)`, then highlight the entity for three seconds. The highlight is a small store (`id`) that the three detail pages consume through `useResaltado(entidadId)`, which also scrolls the element into view once. A plain link would lose the "mark read" side effect, hence a button with a click handler; the item exposes its destination in the accessible description. Stage 3 adds validated origin.

### 11. Emission sites and the idempotent transitions

`ArsDocendi.Shared.Aplicacion.EmisionPosterior.IntentarAsync` wraps every emission, including the lookups needed to build the text (materia name, persona name): a failure anywhere is logged (event name and exception type only) and swallowed. It is a few lines of generic code with no notification concept, so it lives in Shared rather than being copied into three modules (Contracts cannot hold logic). The notifier is also best effort on its own; the wrapper additionally protects against a replacement `INotificador` that throws.

Designaciones transitions called through `AplicarAccionIdempotenteAsync` run inside an outer transaction opened by the idempotency wrapper; the inner `ServicioPedidos.AplicarAccionAsync` therefore returns before the commit. Emitting there would notify before (or without) the commit. The emission was moved to `ServicioPedidosApi`: it runs after the outermost transaction block and only when the transition was really applied (a replayed idempotency key emits nothing). `ServicioPedidos` itself stays free of notification concerns.

### 12. Deviations from the prototype

- **Task number**: the prototype writes `T-0001`; the real task page shows `N° {numero}` (an integer), so the copy says "la tarea N° 7: ...".
- **Highlight** is applied to the data card of the three detail pages, not to the list rows (the click always navigates to the detail). The store and hook are generic, so rows can adopt it later.
- **`Volver a ...`** (stage 3) is not implemented: navigation is a plain `navigate(path)`.
- **Docs** (`docs/architecture`, domains, design specs) are written in Spanish like the surrounding documents (AGENTS rule 10); OpenSpec artifacts, code comments, tests and fixtures are in English as instructed.
- The assignment mentioned `backend/manifiesto-de-aristas.json` and `ArquitecturaAsistenteTests`; neither exists in this branch (they belong to the Asistente work). The dependency graph document was updated and `ArquitecturaIdentityTests` gained a test that fixes Notificaciones as a leaf of the module graph.

### 13. Security pass (add-feature, step 7)

- Authorization: every route except the ping requires a session; no route accepts a user id; visibility is one reusable predicate (user and active profile) applied to all reads and writes; foreign, other-profile and missing notices are indistinguishable (404); bulk ids that are not visible are ignored.
- No new public endpoint without documentation: the ping is the mandatory smoke endpoint; all routes are in `api-contracts.md`.
- Data exposure: the DTO never carries the recipient; logs carry event name, entity id and exception type only (no titles, messages, names, emails or database error details); persona-without-usuario logs only a count.
- Injection: no raw SQL; bulk selection is parsed from `JsonElement` with explicit validation, bounded to 100 ids.
- No secrets, no new packages; no environment variables; infrastructure change limited to the smoke script listing the new ping.
- Boundaries: Notificaciones references only Shared, Migraciones and its own Contracts; emitters reference only the Contracts (tested).
