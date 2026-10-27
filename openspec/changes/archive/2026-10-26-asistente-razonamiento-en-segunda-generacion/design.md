## Context

See `proposal.md` — Why. What shapes the approach:

- `GeneradorDeSql.GenerarAsync` builds the `SolicitudAlModelo` with `Esfuerzo = EsfuerzoConfigurado.Interpretar(EsfuerzoDeGeneracion, …)` and `MaximoDeTokens = MaximoDeTokensDeGeneracion`. It already exposes option-derived flags to the lane (`ReintentaConsultaVacia`, `ReintentaConContexto`, `ReparaConsultaFallida`) under the rule "the lane asks, it does not configure".
- The second generation of a turn has two paths in `CarrilSql`: `ReintentarAsync` (empty result; passes `intentoAnterior` only when `ReintentoConContexto` is on) and `RepararAsync` (engine rejection, only with `RepararConsultaFallida` and `ErrorDelMotorSaneado.EsReparable`). Both call `generador.GenerarAsync`.
- Both already fall back safely. If the second generation is not answerable, has no SQL or does not validate, the retry returns `(original, resultadoOriginal)` and the repair returns `null` so the original rejection is rethrown. A truncated generation is `GeneracionDeSql.Truncada` with `EsContestable = false`, so it cannot replace a good first result.
- `ProveedorLocal.Cuerpo` always sends `chat_template_kwargs.enable_thinking = Esfuerzo is Alto or Maximo`; `SeQuedoSinTokens` comes from `finish_reason == "length"`.
- Defaults: `EsfuerzoDeGeneracion = "medio"`, `MaximoDeTokensDeGeneracion = 4000`; the 3070 profile sets 600.
- The evaluator is deterministic in this setup: the local baseline (Qwen3-8B Q4_K_M, optimized profile, `eval-local/ARS-162/qwen3-8b-q4-B/`: 26/34 capacidad, 12/15 robustez, 10/11 diálogo, 18/20 social) was repeated three times with zero item changes. Any item that changes with the option on is an effect of the option.

Measured server behaviour (2026-10-03, llama-server build 11371, see proposal): with `--reasoning-budget 0` a per-request `enable_thinking: true` is a no-op; with `-1` it reasons, coexists with `json_schema`, and a request without kwargs reasons by default; a low `max_tokens` yields `length` with empty content.

## Goals / Non-Goals

**Goals**

- Spend reasoning only on turns that already failed once.
- One place that decides "this is a second generation", reusable by ARS-75.
- Zero change with the defaults, for every provider.
- A promotion decision made by the evaluator, not by the hypothesis.

**Non-Goals**

- The effort of the first generation or of the redaction.
- The repair of a validator rejection (ARS-75); this change only leaves the mechanism ready.
- Any prompt or prefix change.
- Verifying the option on vLLM.

## Decisions

### D1 — One boolean, `RazonamientoEnSegundaGeneracion`, default `false`

When on, every second generation of a turn — the empty-result retry (with or without context) and the engine-rejection repair — is requested with `EsfuerzoDelModelo.Alto`.

_Alternative considered:_ an effort-valued option (`EsfuerzoDeSegundaGeneracion`). Rejected: the ticket asks for an on/off switch and its acceptance test is phrased on "high effort"; a free effort value adds combinations nobody will measure.

### D2 — The generator owns the decision

`GenerarAsync` gains an explicit parameter saying "this is the second generation of the turn". It cannot be inferred from `intentoAnterior`, which is null in a retry without context. `CarrilSql` passes it in `ReintentarAsync` and `RepararAsync` and nothing else; the generator reads the option and picks effort and ceiling. This keeps "the lane asks, it does not configure" and gives ARS-75 one place to plug into: its repair passes the same parameter.

_Alternative considered:_ the lane building the effort itself. Rejected: it would spread option reads into the lane and duplicate the ceiling rule per path.

### D3 — A separate ceiling, `MaximoDeTokensDeSegundaGeneracion` (int, default `0`)

`0` means "use `MaximoDeTokensDeGeneracion`". The value is applied **only** when `RazonamientoEnSegundaGeneracion` is on, so a leftover value cannot change any request while the option is off. The options validator rejects negatives. The 3070 profile starts at 2000 and is tuned by the measurement (slot of 16,384 tokens, longest measured prompt about 9.8k).

_Alternative considered:_ a fixed default such as 2000. Rejected: with Anthropic's default of 4000 it would lower the ceiling of the second generation.

### D4 — Provider-agnostic

Effort is a port concept, so with `anthropic` and the option on, the second generation goes out with high effort too. With the option off no request changes for any provider; the cassette key includes the effort, so a changed effort would invalidate cassettes. A test must pin that the request of a retry is unchanged with the option off.

### D5 — Server precondition and promotion

The option only has an effect if the model server lets a request turn reasoning on. For llama-server that means `--reasoning-budget` other than `0`. Reasoning stays off by default because the adapter always sends `enable_thinking` explicitly.

- The versioned compose files (`compose.llm-3070.yml`, and the llama-server alternative in `compose.llm.yml`) change **only if the evaluator gate passes**. For the measurement, a local untracked override (`eval-local/ARS-163/compose.razonamiento.yml`) sets the budget.
- On promotion, llama-server also gets a server-side default (`--chat-template-kwargs {"enable_thinking":false}`) **if** task verification shows that a per-request `true` overrides it, so a client that sends no kwargs does not reason. This was not tested; it is a verification task, not an assumption.
- vLLM (`compose.llm.yml`, started with `--default-chat-template-kwargs={"enable_thinking":false}`) was not tested. Whether reasoning coexists with structured outputs there (it may need a reasoning parser so the constraint starts after the reasoning) is an open question. The expected worst case is that the option has no effect rather than a failure, but that is an inference. The change does not claim it works.

_Alternative considered:_ changing the 3070 compose flag unconditionally. Rejected: it alters the server for every client before the evaluator says the option helps.

### D6 — Truncation and latency are contained, and measured

- Truncation: the existing fallbacks (see Context) keep the original result or rethrow the original rejection, so a second generation that reasons past its ceiling degrades to today's behaviour. The gate still counts `truncado_en_generacion` before and after; an increase blocks promotion.
- Latency: the cost lands only on turns that already failed once. Rough arithmetic: about 56 tok/s of output measured on the 3070, so 2000 tokens is about 36 s, inside `TimeoutDeLlamadaSegundos` (60) and the profile's turn budget of 120 s. This is arithmetic, to be measured in the gate.

## Risks / Trade-offs

- **No-op on servers with reasoning disabled.** Mitigation: the precondition is documented in the README, `.env.example` and `modelo-local.md`, and the gate runs with the budget changed.
- **Longer turns** on the failing path, up to roughly a minute. Mitigation: the ceiling is configurable and the gate records latency.
- **Reasoning tokens are counted as output** in the usage registry; the usage panel will show a higher output for these turns.
- **The reasoning text is discarded** by the adapter: it is neither persisted nor shown.
- **The gain may not exist.** A second generation that reasons may still not fix the query; the option then stays off and the result is documented.

## Open Questions

- Does reasoning coexist with structured outputs on vLLM? Out of scope; left recorded.

## Measured outcome (2026-10-03)

- **Server default (task 5.1).** llama-server build 11371 with `--reasoning-budget -1 --chat-template-kwargs '{"enable_thinking":false}'`: a request without kwargs does not reason (34 completion tokens), a request with `enable_thinking: true` does (254 tokens, reasoning in `reasoning_content`), a request with `false` does not. The server-side default works and a per-request `true` overrides it. The gate ran with this configuration.
- **Control run.** Same server flags, option off: zero item changes against the local baseline (26 · 12 · 10 · 18). The flag change is harmless on its own.
- **Option on, ceiling 2000.** Hits unchanged on the four axes. One item changed, `cap-008`: wrong translation to abstention on an answerable question. Capacidad normalized 55.9 % to 58.8 % at penalty 1.0 and 35.3 % to 41.2 % at 2.0. Truncations 0 before and after; no call reached the ceiling (largest output 1,231 tokens). Five second generations reasoned; four changed nothing.
- **Cost.** Run 316 s to 398 s (+26 %); slowest call 10.3 s to 23.3 s; turn p95 7.8 s to 9.6 s.
- **Decision.** The promotion criteria were met, but the effect is one item and not the intended one (reasoning did not fix a query; the repair stopped yielding a valid but wrong one). The product owner decided to leave the option off and documented. Nothing was promoted to `.env.example` or `infra/`.
