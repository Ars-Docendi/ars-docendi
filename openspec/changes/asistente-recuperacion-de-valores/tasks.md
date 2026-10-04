## 1. Option and documentation row

- [ ] 1.1 Write the failing test first in `ValidacionDeOpcionesTests` and `OpcionesDocumentadasTests`: `RecuperacionDeValores` exists, defaults to `false`, and the README table must list it; verify the tests fail.
- [ ] 1.2 Add `RecuperacionDeValores` (bool, default `false`) to `OpcionesAsistente` with its XML doc (D1) and the row to the options table of `backend/src/Modules.Asistente/README.md`; verify the tests of 1.1 pass.

## 2. The matcher (pure, deterministic)

- [ ] 2.1 Write the failing unit tests first for `ReconocedorDeValores`: a unique materia written without accent or numeral yields a line with the span as typed; one-edit typo in a materia («Bases de Dattos»); a carrera without accents; exact full name of a docente in both orders; verify they fail.
- [ ] 2.2 Add the negative tests: ambiguous span (same kind, materia vs carrera, homonymous docentes) yields no line and blocks nested spans; surname alone yields no line; no fuzzy for docentes; common-word spans, spans starting or ending with a stop word, single words under 4 letters, single fuzzy word under 6 letters, first-letter mismatch; entity values with a quote or control character skipped.
- [ ] 2.3 Add the structural tests: overlapping spans (longest first, then leftmost), one entity reached twice gives one line, cap of five lines in reading order, same input gives the same output across repeated runs; verify all fail.
- [ ] 2.4 Implement `ReconocedorDeValores` (design D3) reusing `NormalizadorLexico`; verify 2.1 to 2.3 pass without a database.
- [ ] 2.5 Add fixture-collision tests with the evaluator's shapes: «Análisis Matemático» in three carreras and «Gómez» three times produce no line.

## 3. The scoped lookup

- [ ] 3.1 Write the failing integration tests first against the migrated database (style of `MencionesTests` and `RlsAlcanceTests`): a materia outside `asistente_materias_visibles()` is absent for a materia-scoped actor; a docente whose designations the actor cannot see is absent; a carrera with no visible materia is absent; an out-of-scope homonym neither appears nor suppresses the line of the visible one; a global actor sees all.
- [ ] 3.2 Add the port `IRecuperadorDeValores` (public, value-tuple result) and its `internal` implementation in `Infrastructure`, using `AperturaDeLectura.AbrirAsync`, a read-only transaction and `PreambuloDelActor.AplicarAsync` (D3); verify 3.1 passes and `ArquitecturaAsistenteTests` connection-opening and preamble guards stay green.
- [ ] 3.3 Prove no GRANT is needed: verify `ManifiestoPrivilegiosTests`, `PrivilegiosLecturaTests` and `RlsAlcanceTests` pass unchanged and that `database/asistente/manifiesto-privilegios.json` has no diff; if a column is missing, stop and raise it before touching a grant.
- [ ] 3.4 Test the failure path: a database error and a timeout in the lookup yield no lines and a warning log with no entity value, and the turn answers normally.
- [ ] 3.5 Register the port in `ModuleExtensions`; raise `SuperficiePublicaDeApplication` from 94 to 95 with a remark naming this change; verify `ArquitecturaAsistenteTests` passes.

## 4. Message rendering

- [ ] 4.1 Write the failing tests first in `GeneracionDeSqlTests`: with no lines (or the option off) `ArmarMensaje` returns exactly the bytes of today; with lines the block follows the question and the «Menciones» block and precedes «Intento anterior», one line per match in the ticket's form.
- [ ] 4.2 Add the optional parameter and the block to `GeneradorDeSql.ArmarMensaje` and `GenerarAsync` (D4); verify 4.1 passes.
- [ ] 4.3 Verify the default-off guarantees: `PrefijoDeLosCassettesTests` and every cassette-based test (`ParseoDesdeCassettesTests`, `CassettesEnDiscoTests`) pass with no re-recording.

## 5. Wiring in the SQL lane

- [ ] 5.1 Write the failing tests first in `CarrilSqlTests`: with the option on, a direct `CarrilSql.ResponderAsync` call (the evaluator's entry point) sends the line in the generation request; with it off, no read happens and the request is unchanged; an entity already bound by a new or inherited mention gets no line.
- [ ] 5.2 Call the port in `CarrilSql.ResolverAsync` after the inherited mentions are filtered, drop lines for entities in the bound set (D7), pass the lines to `GenerarAsync`, `ReintentarAsync` and `RepararAsync`; verify 5.1 passes and that a retry and a repair carry the same lines.
- [ ] 5.3 Make a turn with lines not `sinContexto` (D7); write and pass the cache tests: a turn with lines neither reads nor writes the cache; a turn without lines caches as today; two actors with different scope never share a line-built generation.
- [ ] 5.4 Pin the evaluator coverage: a test resolves `CarrilSql` from the module's service provider the way `backend/eval/ArsDocendi.Evaluacion/Program.cs` does (`PorTurno<CarrilSql>`) with the option on and asserts the recuperador is wired; a second test asserts `RunnerDeCapacidad` and `RunnerDeRobustez` reach the lane (robustez through `capacidad.EvaluarAsync`) and not the conversational layer, so a future refactor that moves the call cannot silently disable the measurement.
- [ ] 5.5 Test the conversational path: a clarified question («Me refiero a …») and a follow-up with `ReescrituraEnLaGeneracion` match on the question of this turn (`CapaConversacionalTests`).

## 6. Evaluation gate

- [ ] 6.1 Control run with the option off, same model and profile as the local baseline (Qwen3-8B Q4_K_M, optimized profile), reports in `eval-local/ARS-165/control/`, never `--congelar`; verify it equals the baseline item by item (26/34 capacidad, 12/15 robustez, 10/11 diálogo, 18/20 social). If it differs, stop and find what else moved.
- [ ] 6.2 Run with `Asistente__RecuperacionDeValores=true`, reports in `eval-local/ARS-165/recuperacion/`; compare item by item, list every item that changed (expected candidates: `cap-004`, `-015`, `-019`, `-028`, `rob-003`, `-005`, `-011`) and confirm `cap-020` and `cap-021` produce no line; record the lookup latency (budget p95 under 30 ms).
- [ ] 6.3 Decide promotion (design D9): if no axis worsens in any item, promote; otherwise leave the option off and record which item worsened and the matching rule that steered it. If a wrong steer comes from approximate matching, retry once with exact-only matching before concluding.

## 7. Documentation (both outcomes)

- [ ] 7.1 Document the option in the README «Las menciones» neighbourhood and the options table, including the scope rule, the unique-or-nothing rule, the full-name rule for docentes and the cache bypass; add the section to `docs/architecture/modelo-local.md` §6 with the gate result; update `docs/architecture/domains/asistente.md` if it describes the generation prompt. Verify `OpcionesDocumentadasTests` and `pnpm exec prettier --check` on the touched files pass.
- [ ] 7.2 Only if promoted: add the option to the RTX 3070 block of `.env.example` and to `infra/compose/compose.asistente-local.yml`; verify `pnpm format:check` passes. If not promoted, state in the docs that it is off and why.
- [ ] 7.3 Confirm `docs/architecture/api-contracts.md` needs no change: no endpoint, request or response shape differs.

## 8. Final verification

- [ ] 8.1 Run `dotnet test --solution backend/ArsDocendi.slnx` and confirm it is green, including `ArquitecturaAsistenteTests`, `PrefijoDeLosCassettesTests` and the manifest tests.
- [ ] 8.2 Run `pnpm format:check` and `pnpm exec openspec validate --all --strict`; note any check the environment could not complete.
