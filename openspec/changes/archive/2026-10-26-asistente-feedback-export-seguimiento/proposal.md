## Why

The Asistente (chatbot) module answers questions but gives users no way to close
the loop: they cannot tell the team an answer was wrong, cannot get the result
table out of the browser, and get no help discovering what else the assistant
can do after a successful answer. All three gaps are cheap to close without new
infrastructure: the module already has a privacy-preserving analytics log, a
clipboard export utility, and a verified example catalog with capability
filtering — this change wires user-facing behavior on top of what already
exists, rather than adding new subsystems.

## What Changes

- **Turn feedback**: thumbs up/down with an optional reason (incorrect data /
  didn't understand the question / slow / other) on an answered turn, linked
  only to the random UUID of `asistente.registro_analitico` — never to the
  actor and never to `asistente.registro_operativo`, preserving the
  deliberate non-joinability documented in TD-012. Requires a new backend
  endpoint and a versioned SQL migration for a new table in the `asistente`
  schema (already denied by default to both read-only roles).
- **CSV export**: exports the currently rendered result table to a `.csv` file,
  generated client-side from data already in memory, extending the existing
  clipboard/TSV utility (`frontend/src/features/asistente/utils/portapapeles.ts`).
  Sensitive columns export exactly as displayed (masked). Guards against
  CSV/formula injection, adds a UTF-8 BOM for Excel, and states in the file
  when the exported table was truncated.
- **Contextual follow-up suggestions**: after a successfully answered turn,
  the response may include up to 3 follow-up questions drawn exclusively from
  the existing verified example catalog (`ejemplos-sql.json`), related to the
  answer by category, and filtered through the same per-actor executability
  check (`EXPLAIN`) that `GET /api/asistente/capacidades` already applies —
  never suggesting a question the actor cannot execute. Shows nothing when no
  catalog example qualifies.

**BREAKING**: none. All three additions are backward-compatible extensions of
existing contracts (new optional response field, new endpoint, client-only
export). No existing field changes meaning; see design.md for the one
clarification this makes to `sugerencias`.

Explicitly out of scope: persisting quota/circuit-breaker state (TD-011),
streaming, charts, answer edit/regenerate.

## Capabilities

### New Capabilities

- `asistente-retroalimentacion`: turn-level feedback (thumbs + reason),
  its authorization model, persistence, and idempotency.
- `asistente-exportacion-csv`: client-side CSV export of the result table,
  including injection guarding, encoding, and truncation disclosure.

### Modified Capabilities

- `asistente-contrato-de-respuesta`: adds the feedback-token field the client
  needs to submit feedback for a turn; adds catalog-backed follow-up
  suggestions on a successfully answered turn (matched by answer category,
  filtered through the existing per-actor executability check, capped at 3,
  empty when none qualify); clarifies that `sugerencias` is no longer
  exclusive to a rejected turn.
- `asistente-superficie-frontend`: adds the thumbs control, the CSV export
  action on the result table, and rendering of follow-up suggestions after a
  successful answer.
- `asistente-accesibilidad`: adds keyboard operation, `aria-pressed` state,
  and live-region announcement requirements for the new thumbs control and
  for the export action's outcome.

## Impact

- **Backend** (`Modules.Asistente`): new `Api` endpoint and DTOs for
  feedback; new `Application`/`Infrastructure` pieces for feedback
  persistence and for the post-success suggestion path; a new versioned SQL
  migration in `database/asistente/` creating `asistente.registro_de_retroalimentacion`
  (or equivalent) with a foreign key to `asistente.registro_analitico(id)
ON DELETE CASCADE`; `ResultadoDelTurno` / `RespuestaDelAsistente` gain a
  feedback-token field.
- **Database**: new table in the already-denied `asistente` schema (no new
  `GRANT`, no manifest change expected — see design.md); retention follows the
  existing 90-day purge via the cascade to `registro_analitico`.
- **Frontend** (`frontend/src/features/asistente/`): new thumbs component,
  new export action wired to `TablaDeResultado.tsx`, extension of
  `Sugerencias.tsx` usage to the successful-answer path, extension of
  `portapapeles.ts` with a CSV formatter.
- **Docs**: `docs/architecture/api-contracts.md`, `docs/architecture/data-model.md`,
  `docs/architecture/domains/asistente.md`, and `backend/src/Modules.Asistente/README.md`
  need the new endpoint, table, and response field documented (AGENTS.md rule 6).
- **Tests**: `ManifiestoPrivilegiosTests` / `PrivilegiosLecturaTests` need a
  regression assertion that the new table stays unreadable by both
  `asistente_ro` roles; new integration tests for the feedback endpoint and
  the post-success suggestion path; new Vitest tests for the thumbs
  component, the CSV export utility, and the suggestions rendering.
