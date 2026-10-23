## Why

The Asistente currently forgets every conversation it has: `IAlmacenDeHilos` keeps
questions and the SQL that answered them in memory only, keyed by actor, for 2
hours, lost on every redeploy (`HiloConversacional`, `AlmacenDeHilosEnMemoria`).
A user cannot list, search, rename, or return to a past conversation, and
support staff have no lawful, audited way to see what a user asked when they
report a problem — today the only path would be reading `asistente.registro_analitico`
directly, which is deliberately unlinkable to any actor (TD-012) and therefore
cannot answer "what did this specific person ask." Both gaps block the
baseline a modern chatbot is expected to have (own history, search, resume) and
the baseline institutional support is expected to have (an auditable,
permissioned admin read, never a raw database query).

This change builds on top of the already-implemented (uncommitted)
`asistente-feedback-export-seguimiento` change — it reuses its module shape,
its `asistente` schema conventions, and does not contradict any of its
decisions (D1–D7): the feedback token's unlinkability to the actor is
untouched by this change, and history never stores it.

## What Changes

- **Own conversation history**: every turn is persisted — question text, the
  SQL that answered it, the turn's outcome/state, and timestamps. There is no
  per-conversation opt-out: the system needs to be able to answer
  "user X asked Y" (through the user's own history and through audited
  support access), and a mode that skipped recording would directly
  contradict that. **Never** persisted, regardless: the rows a query returned
  and the model's drafted answer text (both are derivable from re-running the
  stored SQL, and neither needs to survive the request that produced it).
  Users get a list of their own conversations (auto-titled from the first
  question), rename, full-text search over their own questions, delete one
  conversation, delete all conversations (hard delete), and resume a past
  conversation so follow-ups and coreference keep working.
- **Table-only re-execution on resume/view**: viewing a past answered turn
  offers an explicit "volver a consultar" action that re-runs the stored SQL
  under the **current** actor's live RLS/permissions and returns a table —
  never a new model call, never a new history entry. See design.md for why
  table-only (no re-drafting) is the right default.
- **180-day retention**, counted from the conversation's last activity, purged
  by extending the module's existing scheduled purge (`PurgaDeRegistros` /
  `ServicioDePurga`), configurable like every other retention window in
  `OpcionesAsistente`.
- **Audited support access**: a new permission, `asistente.leer_historial_ajeno`,
  seeded and granted to **no role by default** (same pattern as
  `asistente.ver_consulta`), lets a support admin read another user's history
  (question, SQL, outcome, timestamps only — never row data, never a
  re-execution, not even as an impersonation path). Every such read requires a
  mandatory free-text reason and writes an append-only, non-deletable audit
  record (reader, subject, conversation read if any, when, reason). The
  subject is **not** shown who accessed their history — there is no
  "who viewed my history" surface; see design.md for the trade-off.
- **New tables, in the already wholesale-denied `asistente` schema**: no new
  `GRANT`, no change to `manifiesto-privilegios.json` — the assistant's own
  two read-only database roles stay unable to read history or the access
  audit, same as they cannot read `registro_analitico` today.

**BREAKING**: none. `POST /api/asistente/consultas`'s request and response
contracts are unchanged; every new read/write surface is new endpoints.

## Capabilities

### New Capabilities

- `asistente-historial-conversaciones`: what gets persisted per turn (and what
  never does), the 180-day retention and its purge, and the user-facing
  operations on **own** history — list, auto-title, rename, search, delete
  one/all, resume, and table-only re-execution of a past turn's SQL.
- `asistente-acceso-de-soporte-al-historial`: the `asistente.leer_historial_ajeno`
  permission, the mandatory-reason gate, the append-only access audit and its
  own retention, and the explicit exclusions (no re-execution, no row data, no
  transparency to the subject).

### Modified Capabilities

- `asistente-superficie-frontend`: adds the conversation list/sidebar (own
  history), rename, search, delete one/all, resume, the "volver a consultar"
  action on a past turn, and the permission-gated support-read screen. All
  ADDED requirements.
- `asistente-accesibilidad`: adds keyboard operation, focus management, and
  live-region announcement requirements for the new list, its destructive
  actions (delete), and the rename/resume flows. ADDED requirements.

## Impact

- **Backend** (`Modules.Asistente`): two new versioned SQL migrations in
  `database/asistente/` (`004_asistente_historial.sql` for
  `hilo_historico`/`turno_historico`, `005_asistente_auditoria_soporte.sql`
  for `auditoria_acceso_historial`); new `Application`/`Infrastructure` pieces
  for the history write path (hooked where `CapaConversacional` already
  writes the two existing registers), the read/list/search/resume path, the
  re-execution action (reuses `IEjecutorDeConsulta`/masking, no model call),
  and the support-read + audit path; a new seeded permission
  `asistente.leer_historial_ajeno` via an `identity` migration, following the
  exact path `asistente.ver_consulta` used (rule 4: only the administración
  surface writes `identity.permisos`); two new `OpcionesAsistente` retention
  settings; `PurgaDeRegistros` extended with two more sweeps.
- **Database**: two new tables in the `asistente` schema, already denied
  wholesale to both read-only roles — no `GRANT`, no manifest change expected
  (mirrors `retroalimentacion_turno`'s precedent). New regression assertions
  alongside `ManifiestoPrivilegiosTests`/`PrivilegiosLecturaTests` confirming
  that stays true. `ArquitecturaAsistenteTests`'s `CadenaDuena` allowlist gains
  the new writer/reader class names.
- **Frontend** (`frontend/src/features/asistente/`): a conversation
  list/sidebar surfaced from both `/asistente` and the top-bar modal launcher,
  rename/search/delete UI, a resume action, a "volver a consultar" action on
  a past turn, and a new permission-gated support-read screen/route.
- **Docs**: `docs/architecture/api-contracts.md`, `docs/architecture/data-model.md`,
  `docs/architecture/domains/asistente.md`, `backend/src/Modules.Asistente/README.md`,
  `docs/business-rules/asistente.md` (new `BR-asistente-005`/`006` for
  retention and audited support access), `docs/quality/tech-debt.md` (TD-012
  update: its anonymity guarantee now has a documented dependency on this
  change's permission gate and audit, not a reopening of the channel it
  closed), and a new finalidad/purpose note in `docs/product/` mirroring the
  style of `docs/product/consulta-secretaria-portal-asistente.md`.
- **Tests**: new integration tests for the write hook, purge, list/search/
  rename/delete/resume endpoints, re-execution, the support-read + audit
  endpoints, and the privilege-denial regression; new Vitest tests for the
  new frontend surfaces.
