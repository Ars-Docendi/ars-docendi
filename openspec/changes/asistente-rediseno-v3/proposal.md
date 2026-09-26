## Why

The assistant's surface grew by accretion: a page _and_ a modal, a history drawer that
overlays the thread, text buttons under every answer, follow-up suggestion chips after
answers and refusals, and no way to fix a question once sent. The Claude Design file
"Asistente v3 · menos ruido" and the product-owner decisions recorded in Linear epic
ARS-140 (2026-09-26) consolidate it into a single desktop modal with an integrated
history rail and fewer, better controls. AGENTS.md rule 5 requires this change to be
ready before any code (ARS-141).

## What Changes

- **Modal-only assistant with a history rail (ARS-142, ARS-151).** The modal becomes a
  1100×728 two-column layout: a fixed left rail (268 px expanded / 60 px collapsed,
  200 ms transition, state remembered per user) with «Nueva conversación», search and the
  grouped conversation list, and a main column with a 56 px header (active conversation
  title or «Asistente», «?» help, close). It replaces the overlay drawer
  (`ListaDeConversaciones` as overlay, `AbrirHistorial`). **BREAKING (UI):** the
  `/asistente` page, its route and the `AsistentePage` mount are removed; old links
  redirect to the home with the modal open. Support and administration screens keep their
  routes. Accepted debt: on phones the assistant becomes unreachable until the mobile epic
  (new TD-024, linked to TD-016).
- **Archive, deferred delete and undo (ARS-143, ARS-144).** New archived state for
  conversations (archive/unarchive endpoints, collapsible «Archivadas N» rail section).
  Deleting one or all conversations becomes a deferred deletion: the conversation
  disappears at once, a toast offers «Deshacer» for 10 seconds, and afterwards the deletion
  is final — enforced and purged server-side, independent of the client. The same toast
  covers archive/unarchive. **BREAKING (API):** `DELETE /api/asistente/historial[/{id}]`
  now returns `200` with a deletion-batch id instead of `204`.
- **Result table: sort and expand (ARS-145).** Header click sorts ascending/descending by
  type (number, date, text), on displayed values only; «Ampliar tabla» opens a full-modal
  view with the question as title, «Copiar tabla», «Exportar a CSV» and «Contraer». CSV
  exports follow the displayed order. Frontend only.
- **Answer action bar and new thumbs-down reasons (ARS-146).** Icon toolbar under each
  answer (copy answer, expand table, export CSV | 👍 👎) replaces the text buttons.
  Reasons become Datos incorrectos / No entendió la pregunta / Faltan datos / Otro:
  `lento` is no longer accepted, `faltan_datos` is added; existing `lento` rows are kept
  until the existing 90-day purge removes them. **BREAKING (API):** `razon: "lento"` is
  now rejected with `400`.
- **Edit and resend the last question (ARS-147).** Only the last question is editable
  inline; resending replaces that question and its answer, in the in-memory thread and in
  `turno_historico`, with no «N / M» versions. The replaced turn's feedback token stops
  being accepted; the resend is a new turn for quota and respects the per-actor in-flight
  lock.
- **Structured mentions @materia / #docente (ARS-148).** A profile-scoped search endpoint
  feeds a composer popover; a chosen entity travels to `POST /consultas` as a structured
  reference and the SQL lane uses it as an exact filter — bound as a parameter, never sent
  to the model as an internal id.
- **Suggestions removed everywhere except the welcome screen (ARS-149).** Follow-up
  suggestions after answers, suggestion chips on refusals and on the meta-question are
  removed backend and frontend. **BREAKING (API):** the `sugerencias` field leaves the turn
  response; clarification `opciones` are unchanged. The evaluation rule "a no-contestable
  item requires abstention plus suggestions" is replaced by "abstention alone". ARS-139's
  per-reason refusals will ship without chips.
- **Existing states restyled in the v3 language (ARS-150).** Welcome, «Consultando…» (no
  invented stages), «Dejar de esperar», error with «Reintentar», stopped, degraded,
  clarification, maintenance banner, quota indicator (A5) and the debug-only «Cómo lo
  interpreté» are kept and restyled; announcements keep using the existing live region.
- **Docs.** Design spec v3 section (rule 9), API contracts, data model, domain doc,
  module README, BR-`asistente`-005 note, evaluation README and TD-024 (rule 6).

Out of scope: mobile/narrow layouts, question versions, free-text feedback comments,
per-reason refusal templates (ARS-139), the deterministic lane consuming references.

## Capabilities

### New Capabilities

- `asistente-tabla-de-resultado`: sorting the result table by column and the expanded
  table view.
- `asistente-edicion-de-la-ultima-pregunta`: editing and resending only the last
  question, replacing it and its answer end to end.
- `asistente-menciones`: @materia/#docente mentions — scoped search, structured
  references on the turn, exact filtering in the SQL lane, and the composer popover.

### Modified Capabilities

- `asistente-superficie-frontend`: modal-only mount with redirect; rail layout, header,
  archived section, undo toast, action bar; delete without per-row confirmation; new
  reasons; no suggestions rendering.
- `asistente-conversacion`: welcome as example cards; in-flight button slot; storage rule
  allows only the rail preference; single owner; copy actions as icons; v3 tokens.
- `asistente-accesibilidad`: keyboard operation of the rail, archive and undo; undo
  replaces the delete confirmation; icon-only controls named.
- `asistente-historial-conversaciones`: archived state; deferred deletion with a
  server-enforced undo window; archived conversations in search and retention.
- `asistente-acceso-de-soporte-al-historial`: archived and pending-deletion conversations
  are visible to support, marked, until final deletion.
- `asistente-contrato-de-respuesta`: no `sugerencias`; response names its persisted
  conversation.
- `asistente-retroalimentacion`: new reason set with legacy `lento`; replaced turn's token
  stops being accepted.
- `asistente-exportacion-csv`: export follows the displayed (sorted) order.
- `asistente-abstencion`: refusals no longer carry suggestions.
- `asistente-eje-social`: no-contestable items pass on abstention alone.

All ten modified capabilities exist only in un-archived changes today (see design.md
§ "Dependency on in-flight changes").

## Impact

- **Backend (`Modules.Asistente` only).** `HistorialController` (archive, unarchive,
  undo, delete response), `ConsultasDeHistorial`, `RegistroDeHistorial` (replace last
  turn, unarchive on new activity), `PurgaDeRegistros` + a short-period sweep for expired
  pending deletions, `AsistenteController`/`ModelosAsistente` (references, `reemplaza`,
  `conversacion`, no `sugerencias`), `CapaConversacional`/`HiloConversacional` (replace,
  references), `CarrilSql`/`GeneradorDeSql`/`ValidadorDeSql`/`EjecutorDeConsulta`
  (reference markers), new mention search (read-only role + actor preamble), removal of
  `SugerenciasDeSeguimiento`, `ISugerenciasDeSeguimiento`, `Sugerencias`,
  `RazonesDeRetroalimentacion` update, `IValidezDeRetroalimentacion.Revocar`, evaluation
  `RunnerSocial` and `social.json`. No other module is touched; no `*.Contracts` change;
  the dependency graph is unchanged (reads of `identity`/`designaciones` go through the
  existing read-only roles under AGENTS.md rule 11).
- **Database.** `database/asistente/004_asistente_historial.sql` (archive and
  pending-deletion columns, `referencias` on turns) and `003_asistente_retroalimentacion.sql`
  (reason CHECK), following the module's in-file idempotent `ALTER … IF NOT EXISTS`
  convention. No new GRANT; the `asistente` schema stays denied to both read-only roles;
  privilege and sensitivity manifests unchanged (verified by tests).
- **Frontend (`features/asistente`, `app/shell`, `app/router.tsx`).** New rail, header,
  toast, action bar, table sort/expand, inline edit, mention popover; removal of
  `AsistentePage`, `AbrirHistorial`, `Sugerencias`, the `/asistente` index route.
- **Rollback.** Frontend and backend ship together; the schema change is additive
  (nullable columns, widened CHECK), so rolling back code leaves a compatible schema.
  Rolling back after deletions were marked pending leaves marked rows that the old code
  would still list — the rollback step is to run the final purge once before deploying the
  old version (design.md § Migration Plan).
