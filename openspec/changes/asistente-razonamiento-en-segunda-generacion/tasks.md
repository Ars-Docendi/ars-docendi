## 1. Options

- [x] 1.1 Add `RazonamientoEnSegundaGeneracion` (bool, default `false`) and `MaximoDeTokensDeSegundaGeneracion` (int, default `0`) to `OpcionesAsistente` with XML docs; verify `dotnet build backend/ArsDocendi.slnx` passes.
- [x] 1.2 Write the failing test first in `ValidacionDeOpcionesTests` (negative ceiling rejected, zero and positive accepted), then reject negatives in `ValidadorDeOpcionesAsistente`; verify the test passes.
- [x] 1.3 Add both options to the module README options table with their defaults; verify `OpcionesDocumentadasTests` passes.

## 2. Generator (D2, D3)

- [x] 2.1 Write the failing tests first for `GeneradorDeSql`: with the option on, a second generation requests `EsfuerzoDelModelo.Alto` and the first one does not; with the option off, neither does.
- [x] 2.2 Write the failing ceiling tests: option on with 2000 → second generation 2000 and first `MaximoDeTokensDeGeneracion`; option on with 0 → both `MaximoDeTokensDeGeneracion`; option off with a leftover value → both `MaximoDeTokensDeGeneracion`.
- [x] 2.3 Add the "second generation" parameter to `GenerarAsync` and select effort and ceiling there; verify the tests of 2.1 and 2.2 pass and the existing generator tests pass unchanged.

## 3. Lane (D2)

- [x] 3.1 Write the failing lane tests first: the empty-result retry with context, the retry without context and the engine-rejection repair each ask for `Alto` with the option on; the first generation never does.
- [x] 3.2 Pass the parameter from `ReintentarAsync` and `RepararAsync`; verify the tests of 3.1 pass.
- [x] 3.3 Verify the fallbacks: a truncated retry keeps the original result and a truncated repair rethrows the original rejection, with the option on.

## 4. Nothing changes by default (D4)

- [x] 4.1 Add a test that, with the option off and an `anthropic` provider, the request of a retry is byte-for-byte the same (same effort, same ceiling); verify it fails if the option's wiring leaks into the default path.
- [x] 4.2 Run `PrefijoDeLosCassettesTests` and the cassette-based tests and confirm they are green without re-recording anything.

## 5. Server verification

- [x] 5.1 Against llama-server build 11371 with `--reasoning-budget -1`, check whether `--chat-template-kwargs '{"enable_thinking":false}'` acts as a default that a per-request `enable_thinking: true` overrides, and that a request with no kwargs does not reason; record the result in `design.md` Open Questions.
- [x] 5.2 Confirm with `--reasoning-budget 0` that the option is a no-op (33 tokens, empty `reasoning_content`); this is the precondition documented in 7.1.

## 6. Evaluator gate

- [x] 6.1 Run the evaluator with `eval-local/ARS-163/compose.razonamiento.yml` (budget `-1`) and the option **off**, same model and profile as `eval-local/ARS-162/qwen3-8b-q4-B/`; compare item by item with `eval-local/comparar.py`; verify there are zero item changes (proves the server flag is harmless). Never use `--congelar`; reports go to `eval-local/ARS-163/<config>/`.
- [x] 6.2 Run it again with the option **on** (start `MaximoDeTokensDeSegundaGeneracion` at 2000); compare item by item against the local baseline; record capacidad, robustez, diálogo and social, the count of `truncado_en_generacion`, and the latency of turns with a second generation.
- [x] 6.3 If truncations or timeouts appear, tune the ceiling and repeat 6.2; verify the final numbers are the ones recorded.

## 7. Promotion (conditional) and documentation

- [x] 7.1 Document the option in the "Optimizaciones para un modelo propio" section of the module README and in `docs/architecture/modelo-local.md`, with the server precondition, the measurement and the result of the gate, in both outcomes; verify `OpcionesDocumentadasTests` and `pnpm format:check` pass.
- [x] 7.2 Not promoted. The gate was met (capacidad up at every penalty, no axis worse, no truncations), but the gain is a single item moving from a false answer to an abstention, so the product owner decided on 2026-10-03 to leave the option off. `.env.example` and `infra/` are untouched; verified with `git diff --stat`.
- [x] 7.3 The option stays off and the measurement, the server precondition and the decision are recorded in `docs/architecture/modelo-local.md` §6 and in the module README.

## 8. Verification

- [x] 8.1 Run `dotnet test backend/ArsDocendi.slnx` and confirm it is green, including `ArquitecturaAsistenteTests`.
- [x] 8.2 Run `pnpm format:check` and `pnpm exec openspec validate --all --strict` and confirm both pass.
