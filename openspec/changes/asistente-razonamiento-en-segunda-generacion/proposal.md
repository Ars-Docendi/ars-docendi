## Why

With the local model, reasoning (Qwen3's `enable_thinking`) is off in every call: on a shared GPU it multiplies the output 5-20x. But the questions that end in a second generation of the turn — the empty-result retry or the repair of a query PostgreSQL rejected — are precisely the hard ones, where thinking helps. Paying the latency only on the path that already failed is the cheapest place to buy accuracy (Linear ARS-163).

Measured on 2026-10-03 against the real server (llama-server build 11371, Qwen3-8B Q4_K_M, RTX 3070) — this changes the scope:

- With the flag the 3070 profile uses today (`--reasoning-budget 0`), a request with `enable_thinking: true` does **not** reason: 33 completion tokens, empty `reasoning_content`, same answer. Under the current server flags the option would be a **no-op**.
- With `--reasoning-budget -1`, `enable_thinking: false` per request gives no reasoning (34 tokens) and `enable_thinking: true` reasons (254 tokens, the JSON alone in `message.content`). It works together with `response_format: json_schema`. A request with no `chat_template_kwargs` reasons by default.
- With a low `max_tokens` the reasoning eats the budget: `finish_reason: length` and **empty** content, which the generator maps to `truncado_en_generacion`.

So the option needs a server precondition and a higher token ceiling for that call.

## What Changes

- **`RazonamientoEnSegundaGeneracion`** (bool, default `false`): when on, every second generation of a turn — the empty-result retry (with or without context) and the engine-rejection repair — is requested with effort `Alto`. In `ProveedorLocal` that turns `enable_thinking` on. The first generation, the rewrite and the redaction are untouched.
- **`MaximoDeTokensDeSegundaGeneracion`** (int, default `0` = use `MaximoDeTokensDeGeneracion`): the ceiling of that call, applied only when the option is on. The validator rejects negatives.
- `GeneradorDeSql.GenerarAsync` gains an explicit "this is the second generation of the turn" parameter; `CarrilSql` passes it in `ReintentarAsync` and `RepararAsync`. ARS-75 (repair of a validator rejection) can reuse it.
- **Server precondition**: the option only has an effect if the model server lets a request turn reasoning on. For llama-server that means `--reasoning-budget` other than `0`. The versioned compose files change **only if the evaluator gate passes**.
- **Acceptance gate**: the evaluator, run with the option off and on (same model and profile), item by item against the local baseline. If it worsens an axis, the option stays off, is not promoted to `.env.example` nor to `infra/compose/compose.asistente-local.yml`, and the result is documented.

No **BREAKING** changes: with the defaults, Claude's prompt and request do not change by one byte and the cassettes remain valid.

## Capabilities

### New Capabilities

- `asistente-razonamiento-en-segunda-generacion`: when the second generation of a turn is requested with reasoning effort, with which token ceiling, and what stays untouched.

### Modified Capabilities

None. `asistente-optimizaciones-modelo-local` is still an active change, not yet a main spec, so there is nothing to modify; the sibling changes of this family also define their own capability.

## Impact

- `backend/src/Modules.Asistente/`: `Configuracion/OpcionesAsistente.cs`, `Configuracion/ValidadorDeOpcionesAsistente.cs`, `Application/CarrilSql/GeneradorDeSql.cs`, `Application/CarrilSql/CarrilSql.cs`.
- Tests: generator, lane, options validation, `OpcionesDocumentadasTests`, `PrefijoDeLosCassettesTests`.
- Docs (Spanish): `backend/src/Modules.Asistente/README.md`, `.env.example`, `docs/architecture/modelo-local.md`.
- Conditional on the gate: `infra/compose/compose.llm-3070.yml`, `infra/compose/compose.asistente-local.yml`.
