## 1. Database: feedback table

- [x] 1.1 Write a failing integration test asserting `asistente.retroalimentacion_turno` does not exist yet, then add a new versioned migration file (`database/asistente/003_asistente_retroalimentacion.sql`, following the numbering and `IF NOT EXISTS`/idempotence conventions of `002_asistente_registros.sql`) creating the table with: `analitico_id uuid PRIMARY KEY REFERENCES asistente.registro_analitico(id) ON DELETE CASCADE`, `voto boolean NOT NULL`, `razon text NULL` (constrained to the 4 allowed values via `CHECK`), `actualizado_en timestamptz NOT NULL`. No `audit.attach`, matching the existing declared exception for this schema, with an inline comment explaining why (mirroring `002_asistente_registros.sql`'s style). Verify: migration runs idempotently twice against a clean database.
- [x] 1.2 Add an inline comment on the migration explicitly warning against ever adding an actor or timestamp-precision column to this table "for consistency" with `registro_operativo`, and against ever writing the analytic id into `registro_operativo` for the same reason (design.md Risk). Verify: reviewed as part of 1.1's diff, no separate test.
- [x] 1.3 Write an integration test (alongside `ManifiestoPrivilegiosTests`/`PrivilegiosLecturaTests`) asserting neither `asistente_ro` nor `asistente_ro_pii` can `SELECT` from the new table. Verify: test fails if run against a hypothetically-granted table (flip the assertion locally to confirm it can fail), passes against the real migration.
- [x] 1.4 Run `ArquitecturaAsistenteTests` and confirm the new table's DDL shape (`ADD COLUMN IF NOT EXISTS` pattern, no `DROP`/`RENAME`/`ALTER COLUMN ... TYPE`) passes the existing architecture guard unmodified. Verify: `dotnet test backend/ArsDocendi.slnx --filter ArquitecturaAsistenteTests`.

## 2. Backend: feedback token minting and turn contract

- [x] 2.1 Write a failing unit test on `IRegistroDelTurno`/`TurnoParaRegistrar` asserting the analytic id is supplied by the caller (application-generated `Guid`) rather than left to the database default, then implement: generate the id before the insert, pass it explicitly in the `INSERT`. Verify: unit test passes; existing registration tests (`ManifiestoSensibilidadTests`, `EjecucionAcotadaTests`) stay green.
- [x] 2.2 Write a failing test on `ResultadoDelTurno` asserting a new `ClaveDeRetroalimentacion : Guid?` field is populated only when `Estado == Respondida`, then add the field and populate it from 2.1's generated id in `CarrilSql`'s success branch and `CapaConversacional`'s equivalent path. Verify: unit test passes for `Respondida`, and a companion test asserts the field is `null` for `NecesitaAclaracion`, `NoContestable`, `ServicioDegradado`.
- [x] 2.3 Write a failing test on `RespuestaDelAsistente.De` asserting the mapped DTO exposes the token as a new optional field, then map it. Verify: unit test passes; confirm via the existing test that lists fields NOT mapped to the DTO (`SqlEjecutado`) that this new field IS mapped (contrast test).
- [x] 2.4 Update `docs/architecture/api-contracts.md` (`POST /api/asistente/consultas` response shape) and `backend/src/Modules.Asistente/README.md` with the new response field, in the same diff. Verify: doc review; `pnpm exec openspec validate --all --strict` still passes (no dangling references).

## 3. Backend: feedback endpoint

- [x] 3.1 Write a failing integration test: `POST /api/asistente/retroalimentacion` with a valid token from a just-answered turn records a row keyed by that turn's analytic id. Implement a minimal in-memory token-validity store (same shape as `IIdempotencia`: `ConcurrentDictionary<Guid, DateTimeOffset>`, no persistence) populated when a token is minted (2.2) and consulted here, plus the endpoint itself (`[Authorize(Policy = Permisos.AsistenteConsultar)]`, request DTO with `voto: bool`, `razon: string?` restricted to the 4 values via model validation). Verify: test passes.
- [x] 3.2 Write a failing integration test: a token older than 2 hours, and a token that was never issued, both get the same rejection outcome (same status code and body shape). Implement the TTL check. Verify: both scenarios assert identical response shape.
- [x] 3.3 Write a failing integration test: submitting feedback twice with the same token upserts (final vote/reason only, one row). Implement `INSERT ... ON CONFLICT (analitico_id) DO UPDATE`. Verify: test asserts exactly one row and the latest values after two submissions with different votes.
- [x] 3.4 Write a failing integration test: submitting a thumbs-up with a `razon` present stores no reason. Verify: test passes after applying the rule server-side (do not trust the client to omit it).
- [x] 3.5 Write a failing test asserting the turn's request log entry and the feedback endpoint's request log entry never both contain the actor id and the feedback token in the same structured log event (design.md D3). Configure/verify the Serilog enrichers/scopes accordingly. Verify: inspect emitted log events in the test (e.g. a test sink) for the two endpoints and assert the fields present in each.
- [x] 3.6 Write an integration test asserting a token minted for a turn that ended `NoContestable`/`NecesitaAclaracion`/`ServicioDegradado` does not exist (endpoint 404s for it) — there is nothing to look up because no token was minted. Verify: test passes given 2.2.

## 4. Backend: follow-up suggestions after a successful answer

- [x] 4.1 Write a failing unit test: `Sugerencias` gains a new selection function that, given a category and a set of catalog examples, returns up to 3 examples of that category excluding one whose `Sql` textually matches the executed query, and implement it (new function alongside `Sugerencias.Para`, not replacing it — the rejection path keeps using lexical similarity). Verify: unit test passes, including the "excludes the just-answered query" case.
- [x] 4.2 Write a failing integration test: on a successful turn, the response's suggestions are filtered so that only catalog examples the current actor's privileges can execute appear, reusing `CatalogoDeCapacidades.EjecutableAsync`. Wire the category-filtered candidate set from 4.1 through that check on the actor's open reading connection. Verify: integration test with a scoped-privilege actor asserts an excluded example never appears; an unscoped actor sees it.
- [x] 4.3 Write a failing integration test: a successful turn in a category with zero executable catalog matches returns an empty suggestions list, not a fallback. Verify: test passes (no generic-fallback branch is wired for this path, unlike the rejection path).
- [x] 4.4 Wire the result of 4.1–4.3 into `CarrilSql`'s success branch as the `Sugerencias:` argument (currently omitted there). Verify: existing tests that assert the success branch has empty `Sugerencias` today are updated to reflect the new behavior (find and update them; do not leave a stale assertion in place).

## 5. Frontend: feedback UI

- [x] 5.1 Write a failing Vitest test for a new `VotoDeRetroalimentacion` component: renders nothing when the turn has no feedback token, renders two buttons when it does. Implement the component. Verify: test passes.
- [x] 5.2 Write a failing test: `aria-pressed` on each button reflects the current vote state, toggles on click, and both buttons are reachable and operable via keyboard (Tab, Enter/Space). Implement. Verify: test passes; manual keyboard check noted in the PR description per project convention.
- [x] 5.3 Write a failing test: picking thumbs-down shows the 4 reason options before sending, and submitting without a reason still sends. Implement the reason picker. Verify: test passes.
- [x] 5.4 Write a failing test: submitting a vote calls the new API client method against `POST /api/asistente/retroalimentacion` and announces confirmation via the existing live-region mechanism without moving focus. Implement the API client method and the announcement. Verify: test passes.
- [x] 5.5 Write a failing test: after a vote is recorded, viewing the turn again shows that vote as active, and the user can change it (fires a new submission). Verify: test passes.

## 6. Frontend: CSV export

- [x] 6.1 Write a failing unit test for a new `tablaComoCsv` function in `portapapeles.ts`: quotes per RFC 4180, neutralizes cells starting with `=`, `+`, `-`, `@`, tab, or CR (including negative numbers) with a leading apostrophe, and emits UTF-8 BOM + CRLF. Implement it next to `tablaComoTsv`. Verify: unit tests cover a comma cell, a quote cell, a formula-like cell, a negative-number cell, and an accented-character cell.
- [x] 6.2 Write a failing unit test: when the table is flagged truncated, the generated CSV has a trailing row stating truncation in words and no row states a count; when not truncated, no trailing row is added. Implement. Verify: unit tests for both cases.
- [x] 6.3 Write a failing unit test: the file name follows the `asistente-resultado-<hilo-corto>-<fecha-ISO>[-truncado].csv` pattern and contains only ASCII-safe characters. Implement the naming helper. Verify: unit test passes.
- [x] 6.4 Write a failing component test: `TablaDeResultado.tsx` renders an export action when there is at least one row, none when there are zero rows, and the action triggers a file download built from `tablaComoCsv` (mock the download trigger). Implement the button and wiring. Verify: test passes.
- [x] 6.5 Write a failing test: the export action is keyboard-operable and announces completion via a live region without moving focus. Implement. Verify: test passes.
- [x] 6.6 Write a failing test asserting the export never reads any value other than the already-rendered cell array (i.e. it cannot regress into un-masking): construct a fixture where the rendered array is masked and assert the exported CSV matches the rendered array byte-for-byte on the sensitive column. Verify: test passes.

## 7. Frontend: follow-up suggestions on success

- [x] 7.1 Write a failing test: `Sugerencias.tsx` (or the turn-rendering component that wires it) renders suggestions coming from a successfully answered turn the same way it renders a rejection's suggestions, and renders nothing when the successful turn has none. Wire the existing component to the success path's `sugerencias` field. Verify: test passes.
- [x] 7.2 Write a failing test: choosing a post-success suggestion starts a new question the same way choosing a post-rejection suggestion does (reuses the existing `onElegir` contract, no new interaction pattern). Verify: test passes.

## 8. Docs (AGENTS.md rule 6, same diff as the code that changes each contract)

- [x] 8.1 Update `docs/architecture/api-contracts.md`: new `POST /api/asistente/retroalimentacion` endpoint (method, path, permission, request/response shape, error cases for invalid/expired token); the new `sugerencias`-on-success and feedback-token fields on `POST /api/asistente/consultas`'s response (if not already fully covered by 2.4).
- [x] 8.2 Update `docs/architecture/data-model.md`: the new `asistente.retroalimentacion_turno` (or chosen name) table, its FK/cascade relationship to `registro_analitico`, and its schema-level deny inheritance (no new GRANT).
- [x] 8.3 Update `docs/architecture/domains/asistente.md`: describe the feedback flow (token minting, TTL-bound validity, upsert semantics) and the post-success suggestions flow (category match + executability filter), cross-referencing TD-012 for why the token carries no actor.
- [x] 8.4 Update `backend/src/Modules.Asistente/README.md`: add the new endpoint to its endpoint table, and a short note on the new table under "El schema `asistente`: dos registros que no se cruzan" (now three tables that don't cross), preserving that section's existing "why" style.
- [x] 8.5 Update `docs/quality/tech-debt.md` only if this change surfaces a new accepted residual risk beyond what design.md's Risks section already states verbatim (e.g. if the token-possession authorization model is judged worth its own TD entry during review). Otherwise, no change here — do not duplicate design.md's Risks into tech-debt.md.

## 9. Final verification

- [x] 9.1 Run the full backend suite and confirm green: `dotnet test backend/ArsDocendi.slnx`.
- [x] 9.2 Run the full frontend suite and confirm green: `pnpm --filter frontend test:run`.
- [x] 9.3 Run frontend lint and build: `pnpm --filter frontend lint` and `pnpm --filter frontend build`.
- [x] 9.4 Run formatting check: `pnpm format:check`.
- [x] 9.5 Validate this change strictly: `pnpm exec openspec validate asistente-feedback-export-seguimiento --strict`.
- [x] 9.6 Record in the PR description any command from this list the environment could not run to completion, and why.
