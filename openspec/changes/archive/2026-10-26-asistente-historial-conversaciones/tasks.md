## 1. Database: conversation history tables

- [x] 1.1 Write a failing integration test asserting `asistente.hilo_historico` and `asistente.turno_historico` do not exist yet, then add `database/asistente/004_asistente_historial.sql` (following `002_asistente_registros.sql`'s `IF NOT EXISTS`/idempotence conventions) creating: `asistente.hilo_historico (id uuid PRIMARY KEY, actor_id uuid NOT NULL, titulo text NOT NULL, creado_en timestamptz NOT NULL, ultima_actividad timestamptz NOT NULL)` and `asistente.turno_historico (id uuid PRIMARY KEY, hilo_id uuid NOT NULL REFERENCES asistente.hilo_historico(id) ON DELETE CASCADE, pregunta text NOT NULL, sql_resuelto text NULL, estado text NOT NULL, ocurrido_en timestamptz NOT NULL)`, plus an index on `hilo_historico(actor_id, ultima_actividad)` and a GIN index on `to_tsvector('spanish', turno_historico.pregunta)`. Verify: migration runs idempotently twice against a clean database.
- [x] 1.2 Add inline comments on the migration (mirroring `002_asistente_registros.sql`'s style) stating explicitly: (a) this table is intentionally actor-linked, unlike `registro_analitico`/`registro_operativo`, and why (design.md D1/D12); (b) never add a foreign key or join path from these tables back to `registro_analitico`/`registro_operativo`/`retroalimentacion_turno` (design.md D8); (c) `ultima_actividad`, not each turn's own timestamp, drives retention (design.md D7); (d) every conversation is recorded here — there is no per-conversation opt-out, by explicit product decision (design.md D2). Verify: reviewed as part of 1.1's diff, no separate test.
- [x] 1.3 Write an integration test (alongside `ManifiestoPrivilegiosTests`/`PrivilegiosLecturaTests`) asserting neither `asistente_ro` nor `asistente_ro_pii` can `SELECT` from `hilo_historico` or `turno_historico`. Verify: `dotnet test backend/ArsDocendi.slnx --filter PrivilegiosLecturaTests`.
- [x] 1.4 Run `ArquitecturaAsistenteTests` and confirm the two new tables' DDL shape passes the existing architecture guard unmodified (no manifest change needed, mirroring `retroalimentacion_turno`'s precedent). Verify: `dotnet test backend/ArsDocendi.slnx --filter ArquitecturaAsistenteTests`.

## 2. Database: support-access audit table

- [x] 2.1 Write a failing integration test asserting `asistente.auditoria_acceso_historial` does not exist yet, then add `database/asistente/005_asistente_auditoria_soporte.sql` creating `asistente.auditoria_acceso_historial (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, lector_id uuid NOT NULL, sujeto_id uuid NOT NULL, hilo_historico_id uuid NULL, razon text NOT NULL, ocurrido_en timestamptz NOT NULL)`, with an inline comment explaining `hilo_historico_id` carries **no foreign key** on purpose (design.md D10: the audit row must outlive a subject's own deletion of that conversation) and an index on `ocurrido_en` for the purge. Verify: migration runs idempotently twice.
- [x] 2.2 Write the same denial regression test as 1.3 for `auditoria_acceso_historial`. Verify: `dotnet test backend/ArsDocendi.slnx --filter PrivilegiosLecturaTests`.
- [x] 2.3 Run `ArquitecturaAsistenteTests` and confirm this table's DDL shape passes unmodified. Verify: `dotnet test backend/ArsDocendi.slnx --filter ArquitecturaAsistenteTests`.

## 3. Identity: the support-history permission

- [x] 3.1 Confirm against `develop` the next unused id in the seeded-permission sequence (`b2000000-0000-4000-8000-0000000000xx`; `...028` as of this planning pass — see design.md Context). Write a failing test asserting `identity.permisos` has no row with `code = 'asistente.leer_historial_ajeno'`, then add `database/identity/0NN_identity_permiso_leer_historial_ajeno.sql` (next free file number), following `014_identity_permiso_ver_consulta.sql`'s structure exactly: `INSERT ... ON CONFLICT (code) DO NOTHING`, an inline comment stating it is seeded to **no role, not even `sys_admin`**, and a `RAISE NOTICE` guard if it is ever already granted. Verify: migration idempotent twice; permission exists with `code = 'asistente.leer_historial_ajeno'` after running.
- [x] 3.2 Add the corresponding EF Core migration class in `ArsDocendi.Shared.Identity.Migrations` (mirroring `PermisoAsistenteVerConsulta`), running the SQL from 3.1 in `Up` and reverting it in `Down`. Verify: `dotnet ef database update`/test harness applies and reverts cleanly.
- [x] 3.3 Add `AsistenteLeerHistorialAjeno = "asistente.leer_historial_ajeno"` to `Permisos.cs` and include it in `Permisos.Todos`. Verify: a test enumerating `Permisos.Todos` includes it, matching the seeded code.
- [x] 3.4 Write a test asserting the permission is granted to zero roles immediately after seeding, including `sys_admin`. Verify: test passes against a freshly migrated database.

## 4. Backend: configuration and the extended purge

- [x] 4.1 Add `RetencionDeHistorialDias` (default `180`) and `RetencionDeAuditoriaDeSoporteDias` (default `365`) to `OpcionesAsistente`, documented the same way as `RetencionDeRegistrosDias`. Verify: `OpcionesDocumentadasTests` (extend it) passes against the new defaults.
- [x] 4.2 Write a failing test: `PurgaDeRegistros.PurgarAsync` deletes `hilo_historico` rows (cascading `turno_historico`) whose `ultima_actividad` is older than `RetencionDeHistorialDias`, and leaves more recently active conversations untouched even if `creado_en` is old. Implement the new sweep, reusing the existing `TimeProvider`-driven, log-and-continue-on-failure shape. Verify: test passes.
- [x] 4.3 Write a failing test: `PurgaDeRegistros.PurgarAsync` deletes `auditoria_acceso_historial` rows older than `RetencionDeAuditoriaDeSoporteDias`, independent of whether the conversation they reference still exists. Implement. Verify: test passes, including a case where the referenced `hilo_historico_id` was already deleted.
- [x] 4.4 Write a failing test: a purge failure on one of the new sweeps is logged and does not crash `ServicioDePurga`'s loop, matching the existing registers' failure behavior. Verify: test passes.

## 5. Backend: persisted-history write path

- [x] 5.1 Write a failing unit test: `HiloConversacional` exposes a settable `HiloHistorico : Guid?` (default `null`). Implement the property. Verify: unit test passes; existing `HiloConversacionalTests` stay green.
- [x] 5.2 Define `IRegistroDeHistorial` (`Application/Historial/`) with a method to record one turn (actor, conversation state, `TurnoDelHilo`-shaped question/SQL, `EstadoDelTurno`, timestamp) and mint/reuse the conversation's `hilo_historico` row, auto-titling from the first question. Write a failing unit test for the auto-title derivation (truncation/whitespace handling included). Implement. Verify: unit test passes.
- [x] 5.3 Implement `RegistroDeHistorial` (`Infrastructure/`) using the owner connection (`CadenaDuena`, added to `ArquitecturaAsistenteTests.PuedenUsarLaCadenaDelDueno` with the same documented reason as `RegistroDelTurno.cs`), upserting `hilo_historico` (insert on first write for that `HiloHistorico`, else update `ultima_actividad`) and inserting one `turno_historico` row. Write a failing integration test asserting a `Respondida` turn produces exactly one `hilo_historico` row and one `turno_historico` row with the question and resolved SQL. Verify: test passes.
- [x] 5.4 Write a failing integration test: a turn ending `NecesitaAclaracion`, `NoContestable`, or `ServicioDegradado` is persisted to history (question, null-or-present SQL as appropriate, correct state), and a turn ending `Fallo` is never persisted. Implement the guard in the write hook — this is the **only** exclusion from history; there is no per-conversation opt-out. Verify: all four cases pass.
- [x] 5.5 Write a failing integration test: the history write never fails the turn (simulate a write failure and assert the HTTP response is unaffected), matching `IRegistroDelTurno`'s existing discipline. Verify: test passes.
- [x] 5.6 Register `IRegistroDeHistorial`/`RegistroDeHistorial` in `ModuleExtensions.cs` with the same scoping rationale as `IRegistroDelTurno`. Verify: `dotnet build` succeeds; DI resolves without a captive-dependency warning.

## 6. Backend: own-history list, search, rename, delete

- [x] 6.1 Write a failing integration test for `GET /api/asistente/historial` (requires `asistente.consultar`): returns the actor's own conversations (id, título, creadoEn, ultimaActividad), never another actor's. Implement the endpoint and a read-side query class (added to the `CadenaDuena` allowlist with its own documented reason). Verify: test passes, including the cross-actor isolation case.
- [x] 6.2 Write a failing integration test: `GET /api/asistente/historial?q=<texto>` matches the actor's own past questions via PostgreSQL full-text search (`to_tsvector('spanish', ...)`), case/accent-insensitively where Spanish stemming applies, and returns no match from another actor's conversations. Implement. Verify: test passes.
- [x] 6.3 Write a failing integration test: `PATCH /api/asistente/historial/{hiloId}` renames one of the actor's own conversations; renaming another actor's conversation is rejected. Implement, with the ownership check as a reusable guard (reused by 6.4, 6.5, 7.x). Verify: both cases pass.
- [x] 6.4 Write a failing integration test: `DELETE /api/asistente/historial/{hiloId}` hard-deletes one conversation and its turns (cascade), leaves other conversations untouched, and is rejected for another actor's conversation. Verify: test passes.
- [x] 6.5 Write a failing integration test: `DELETE /api/asistente/historial` hard-deletes all of the actor's own conversations and leaves other actors' conversations untouched. Verify: test passes.
- [x] 6.6 Write a failing integration test: `GET /api/asistente/historial/{hiloId}` returns one conversation's turns (question, outcome, timestamps always; SQL only with `asistente.ver_consulta`), and is rejected for another actor's conversation. Verify: both the permission-gating and ownership cases pass.

## 7. Backend: resume

- [x] 7.1 Add a new `IAlmacenDeHilos.Sembrar(Guid actor, Guid hiloHistorico, IReadOnlyList<TurnoDelHilo> turnos)` method (design.md D3) that creates and stores a fresh ephemeral `HiloConversacional` with a new random id, the given `HiloHistorico`, and the given turns replayed via `Agregar`. Write a failing unit test asserting the returned thread's `HistorialVigente`/`ConsultasVigentes` reflect the seeded turns immediately. Implement. Verify: unit test passes; existing `AlmacenDeHilosEnMemoriaTests`/`HiloConversacionalTests` stay green.
- [x] 7.2 Write a failing integration test: `POST /api/asistente/historial/{hiloId}/reanudar` (ownership-checked) returns a new ephemeral `hilo` id plus the conversation's persisted turns, and that ephemeral id works immediately on `POST /api/asistente/consultas` for a follow-up. Implement the endpoint. Verify: test passes end-to-end (resume, then a coreference follow-up resolves against the restored context).
- [x] 7.3 Write a failing integration test: a turn sent on the resumed ephemeral thread appends to the **same** `hilo_historico.id`, not a new one. Verify: test passes.
- [x] 7.4 Write a failing integration test: resuming another actor's conversation is rejected. Verify: test passes.

## 8. Backend: table-only re-execution

- [x] 8.1 Write a failing integration test: `POST /api/asistente/historial/turnos/{turnoId}/reejecutar` (ownership-checked via the turn's parent conversation) re-runs the stored `sql_resuelto` under the current actor's live scope/RLS and returns columns/rows/sensitivity/truncation, reusing `IEjecutorDeConsulta` and the existing masking pipeline, with no model call. Implement. Verify: test passes; assert (via a counter/mock) zero model calls occurred.
- [x] 8.2 Write a failing integration test: re-execution is rejected for a turn whose `estado` is not `Respondida` or whose `sql_resuelto` is null. Verify: test passes.
- [x] 8.3 Write a failing integration test: re-execution reflects the actor's **current** privileges, not their privileges at the time of the original turn (e.g. a scope that has since shrunk). Verify: test passes.
- [x] 8.4 Write a failing integration test: re-executing SQL that no longer runs (simulate a `42501` or a schema-drift error) returns a friendly, non-technical outcome, never a raw database error. Verify: test passes.
- [x] 8.5 Write a failing integration test: re-execution never writes a new `turno_historico`/`hilo_historico` row and never writes to `registro_operativo`/`registro_analitico`. Verify: test passes.
- [x] 8.6 Write a failing integration test: re-execution is rejected outright for a turn that does not belong to the requesting actor (no such endpoint path exists for support access — see group 9). Verify: test passes.

## 9. Backend: support-read endpoints and audit

- [x] 9.1 Write a failing integration test: `POST /api/asistente/soporte/historial/{actorId}/listar` requires `asistente.leer_historial_ajeno` and a non-empty `razon` in the body; rejects a caller without the permission and rejects a missing/whitespace `razon`. Implement the endpoint (list only: id, título, creadoEn, ultimaActividad — no turns). Verify: all three rejection cases plus the success case pass.
- [x] 9.2 Write a failing integration test: the listing call writes exactly one `auditoria_acceso_historial` row (lector, sujeto, `hilo_historico_id = null`, razon, timestamp) before returning data, and returns no data if that write fails. Implement the write-before-respond ordering. Verify: test passes, including the simulated-write-failure case.
- [x] 9.3 Write a failing integration test: `POST /api/asistente/soporte/historial/{actorId}/{hiloId}/leer` returns one subject's conversation turns (question, SQL, outcome, timestamps — no row data), requires the same permission and mandatory `razon`, and writes one audit row naming that specific `hiloId`. Implement. Verify: test passes.
- [x] 9.4 Write a failing integration test: neither support endpoint exposes a re-execution action or path, and calling the own-history re-execution endpoint (8.1) against another actor's turn id is rejected regardless of holding `asistente.leer_historial_ajeno`. Verify: test passes.
- [x] 9.5 Write a failing integration test: no endpoint returns, to an ordinary actor, any record of another actor having read their history (i.e. there is no "who accessed my history" surface — design.md D11). Verify: test enumerates the module's routes and asserts none match that shape, and that the audit table is unreachable through any own-history endpoint.
- [x] 9.6 Write a failing test asserting the support endpoints' structured log entries never combine the subject's actor id with any question/SQL text in a single event beyond what the audit row itself already records, extending the discipline of `asistente-feedback-export-seguimiento`'s D3. Verify: test inspects emitted log events for both endpoints.
- [x] 9.7 Add `RegistroDeHistorial`'s support-side counterpart (e.g. `ConsultasDeAuditoriaDeSoporte`) to `ArquitecturaAsistenteTests.PuedenUsarLaCadenaDelDueno` with its own documented reason. Verify: `dotnet test backend/ArsDocendi.slnx --filter ArquitecturaAsistenteTests`.

## 10. Frontend: own conversation list

- [x] 10.1 Write a failing Vitest test for a new conversation-list component: renders the actor's conversations with title and last-activity, renders an empty state with none. Implement, calling the new list endpoint. Verify: test passes.
- [x] 10.2 Write a failing test: a search input narrows the rendered list using the search endpoint (debounced), and an empty result state is shown for no matches. Implement. Verify: test passes.
- [x] 10.3 Write a failing test: the list is reachable both from `/asistente` and from the top-bar launcher modal, backed by the same component/hook. Implement the wiring in both surfaces. Verify: test passes for both entry points.

## 11. Frontend: rename and delete

- [x] 11.1 Write a failing test: an inline rename control saves a new title via the rename endpoint and updates the list without a full reload. Implement. Verify: test passes.
- [x] 11.2 Write a failing test: deleting one conversation shows a confirmation step before calling the delete endpoint. Implement. Verify: test passes.
- [x] 11.3 Write a failing test: deleting all conversations shows a confirmation step before calling the delete-all endpoint, and the list is empty afterward. Implement. Verify: test passes.

## 12. Frontend: resume and re-execution

- [x] 12.1 Write a failing test: opening a listed conversation calls the resume endpoint, renders its past turns, and stores the returned ephemeral `hilo` id for the next request. Implement. Verify: test passes.
- [x] 12.2 Write a failing test: a follow-up question sent after resuming uses the new ephemeral `hilo` id and appends visibly to the same conversation. Verify: test passes.
- [x] 12.3 Write a failing test: a "volver a consultar" action is rendered on a past answered turn and absent on any other outcome; activating it calls the re-execution endpoint and renders the returned table (mock the call). Implement. Verify: test passes.
- [x] 12.4 Write a failing test: a re-execution that fails gracefully (friendly outcome from 8.4) renders that message instead of a table, without a client-visible crash. Verify: test passes.

## 13. Frontend: support-read screen

- [x] 13.1 Write a failing test: a new support-history route/screen renders only when the current actor's capabilities include `asistente.leer_historial_ajeno`, and is unreachable (no link, no route match) otherwise. Implement, reusing the existing user-lookup UI/endpoint for subject selection (no new user-search surface). Verify: test passes for both the gated and ungated cases.
- [x] 13.2 Write a failing test: the screen requires a non-empty reason before it will list or open a subject's history, calling the endpoints from group 9. Implement. Verify: test passes.
- [x] 13.3 Write a failing test: the screen renders question/SQL/outcome/timestamps only, with no result-row rendering and no re-execution control anywhere on it. Verify: test passes (assert the absence, not just the presence of the expected fields).

## 14. Accessibility

- [x] 14.1 Write a failing test: every action in the conversation list (open, rename, search, delete one, delete all) is reachable by Tab in a logical order and activates with Enter/Space. Implement. Verify: test passes.
- [x] 14.2 Write a failing test: a completed delete (one or all), a saved rename, and a completed resume each announce via the existing live-region mechanism without moving focus unexpectedly. Implement. Verify: test passes for all three flows.
- [x] 14.3 Write a failing test: the "volver a consultar" action is keyboard-operable and announces completion via the live region. Implement. Verify: test passes.

## 15. Docs (AGENTS.md rule 6, same diff as the code that changes each contract)

- [x] 15.1 Update `docs/architecture/api-contracts.md`: the new `GET/PATCH/DELETE /api/asistente/historial*`, `POST /api/asistente/historial/{hiloId}/reanudar`, `POST /api/asistente/historial/turnos/{turnoId}/reejecutar`, and `POST /api/asistente/soporte/historial/*` endpoints (method, path, permission, request/response shape, error cases). `POST /api/asistente/consultas`'s own contract is unchanged.
- [x] 15.2 Update `docs/architecture/data-model.md`: `asistente.hilo_historico`, `asistente.turno_historico`, and `asistente.auditoria_acceso_historial` — columns, the cascade from `hilo_historico` to `turno_historico`, the deliberate absence of any FK from the audit table, and the inherited schema-level deny (no new `GRANT`).
- [x] 15.3 Update `docs/architecture/domains/asistente.md`: describe the persisted-history flow (write hook, unconditional recording with no per-conversation opt-out, retention/purge), the resume mechanism (ephemeral-thread reseeding), the table-only re-execution action, and the support-access flow (permission, mandatory reason, audit, no transparency to the subject), cross-referencing TD-012.
- [x] 15.4 Update `backend/src/Modules.Asistente/README.md`: add the new endpoints to its endpoint table; extend "El schema `asistente`" to describe the new tables under the same "why" style as the existing three; document the two new `OpcionesAsistente` settings alongside the existing retention/purge table.
- [x] 15.5 Add `BR-asistente-005` (180-day history retention, purge mechanism, finalidad/proportionality rationale, and that every conversation is recorded with no opt-out) and `BR-asistente-006` (audited, permissioned support access; no transparency to the subject; its own audit retention) to `docs/business-rules/asistente.md`, each with a test mapping to the tasks above.
- [x] 15.6 Update `docs/quality/tech-debt.md`'s TD-012 entry with the short cross-reference described in design.md D12 (the analytic register's anonymity now holds only against a reader who also lacks history access) — do not rewrite TD-012's existing content about the analytic register itself. Add a **new TD entry** (next free number after TD-022, i.e. `TD-023` as of this planning pass — confirm against `develop` at implementation time) recording the `ConnectionId`/`RequestId` Serilog log-context cross-request correlation gap (design.md D12): with history now recording every conversation, this channel is a bypass of the `asistente.leer_historial_ajeno` permission gate and its audit trail (not a bypass of anonymity — there is none left to bypass for history). Cross-reference TD-012 and this change. Mitigation to record: strip `ConnectionId`/`RequestId` from the turn's and the feedback endpoint's log events if a structured sink is ever configured.
- [x] 15.7 Add a short finalidad/purpose note for own history and for audited support access to `docs/product/` (new file or a section near `docs/product/consulta-secretaria-portal-asistente.md`), in that document's style: what each surface is for, who can use it, that every conversation is recorded with no opt-out and why, and what it explicitly does not do (no sharing, no subject-visible access log).
- [x] 15.8 If a UX design spec exists for the assistant's surfaces (`docs/product/designs/asistente-conversacional-design-spec.md`), update it with the conversation list, resume, re-run action, and the support-read screen; if none of its existing sections cover chrome like this, add a new section rather than leaving the new surfaces undocumented there.

## 16. Final verification

- [x] 16.1 Run the full backend suite and confirm green: `dotnet test backend/ArsDocendi.slnx`.
- [x] 16.2 Run the full frontend suite and confirm green: `nvm use 22 && pnpm --filter frontend test:run`.
- [x] 16.3 Run frontend lint and build: `nvm use 22 && pnpm --filter frontend lint` and `nvm use 22 && pnpm --filter frontend build`.
- [x] 16.4 Run formatting check: `pnpm format:check`.
- [x] 16.5 Validate this change strictly: `pnpm exec openspec validate asistente-historial-conversaciones --strict`.
- [x] 16.6 Record in the PR description any command from this list the environment could not run to completion, and why.
