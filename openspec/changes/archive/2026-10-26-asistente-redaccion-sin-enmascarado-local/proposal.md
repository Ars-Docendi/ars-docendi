## Why

The output boundary masks eight sensitive columns before the redaction call so their values never reach a third-party model provider (`asistente-enmascaramiento`). With the provider set to `local` the model runs on the Department's own machine and there is no third party, yet the narrated sentence still reads "documento 1, documento 2 y documento 3" for an actor who is already allowed to read those documents and sees them in the result table.

Measured on 2026-10-03 against Qwen3-8B (llama-server, RTX 3070) with the synthetic fixture, 3 questions × 2 variants × 3 repetitions (`eval-local/enmascarado/`):

- Masked narration carries no usable value; unmasked narration states the values, with 0 invented values in 9 calls.
- Unmasking is **not** a performance gain: redaction takes 0.45 → 0.57 s on one row and 0.42 → 0.70 s on three rows, and the prompt grows 28 % on 24 rows.
- No evaluator score can move: SQL generation never sees rows, and no dataset item touches a sensitive column.

So this is a redaction-quality option for on-premises deployments, not an optimization.

## What Changes

- Add an opt-in setting, `Asistente__RedaccionSinEnmascarar` (default `false`), that lets the real values of `sensible-valor` columns reach the redaction prompt.
- The setting takes effect **only** when the provider is `local` **and** no cassette directory is configured. In any other configuration masking stays on and startup logs a warning; the process still starts, so rolling the provider back to `anthropic` never depends on remembering to clear this setting.
- `sensible-texto` columns stay suppressed in every configuration: they are free text written by other users, and letting them into the prompt adds a prompt-injection path and unbounded tokens.
- Access control does not change. Who can read personal data is still decided by the PostgreSQL role (permission **and** global scope); this setting only decides what the redaction model sees for an actor who already received the real rows.
- Document the exposure the operator accepts when turning it on: the values enter the model server's KV cache and its request path.

No **BREAKING** changes: with the default, the redaction prompt is byte-for-byte the current one.

## Capabilities

### New Capabilities

- `asistente-redaccion-sin-enmascarado-local`: when the redaction prompt may carry real `sensible-valor` values, under which provider and recording configuration, and what stays masked regardless.

### Modified Capabilities

None under `openspec/specs/`. The masking contract lives in the still-unarchived change `asistente-enmascaramiento` (`asistente-enmascarador`), whose requirement "Ninguna columna `sensible-valor` llega al modelo con su valor real" is stated without exceptions. This change narrows it to "unless this option is effective"; the wording of that delta is updated in the same diff (see tasks) so the two do not contradict each other at archive time.

## Impact

- **Module**: `Modules.Asistente` only. No `Contracts` change, no new project reference; the dependency graph is untouched.
- **Code**: the options class and its validator, the masking function, and the single call site that feeds the redactor (`CarrilSql`, redaction step).
- **API / schema**: none. The turn response already returns the real rows and the per-column sensitivity.
- **Tests**: new cases next to `EnmascaradorTests`, `EnmascaramientoDelTurnoTests` and `ValidacionDeOpcionesTests`; `OpcionesDocumentadasTests` forces the README table to list the new option.
- **Docs**: `backend/src/Modules.Asistente/README.md` (output boundary and options table), `docs/architecture/modelo-local.md` §6, `docs/architecture/domains/asistente.md`, `.env.example`.
- **Business rules**: none. No institutional regulation is added or changed.
- **Rollback**: set `Asistente__RedaccionSinEnmascarar=false` (or remove it) and restart. No data migration, nothing persisted.
