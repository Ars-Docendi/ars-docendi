## 1. Option

- [x] 1.1 Write the failing tests first: `OpcionesDocumentadasTests` gains a check that every boolean row of the README options table matches the code default, and a case that requires `GlosarioEnElPrefijo` to be documented with `false`; verify they fail before the option exists
- [x] 1.2 Add `GlosarioEnElPrefijo` (bool, default `false`) to `OpcionesAsistente` with its XML doc, next to `EjemplosEnElPrefijo` (design D1), and its row in the README options table; verify `dotnet build backend/ArsDocendi.slnx` and the tests of 1.1 pass (if an existing boolean row drifts, fix the row and note it in the diff)

## 2. Glossary file and loader

- [x] 2.1 Write the failing pure tests first for the loader: required fields, at least one reference, unique terms ignoring case and accents, `columna` shape, `verificaContra` only on a column without `CHECK`, hint closure (literals among the term's values, identifiers among its columns or the SQL allow-list), and a malformed file failing with a message naming the term; verify they fail
- [x] 2.2 Add `Recursos/glosario.json` with the 24 decided terms (design D9; do not add, drop or reword terms), the `<EmbeddedResource>` line in `Modules.Asistente.csproj`, and `CatalogoDeGlosario` (lazy, parsed into immutable records, validated at load) (design D3); verify the tests of 2.1 pass and that the resource is found by name in a test
- [x] 2.3 Review each entry against the comments of its columns and against prompt rule 8 (design D7, D8): codes use `=`/`IN`, polysemous words say their sense; verify by reading the migration comments (`010_…comentarios_asistente.sql`, `012_…comentarios_dedicaciones.sql`) and recording any tension found in the PR description

## 3. Existence, readability and sensitivity tests

- [x] 3.1 Write `GlosarioInstitucionalTests` (migrated database) with the column checks, failing first with deliberately broken entries held in the test: a nonexistent column, a schema absent from the privilege manifest (`tareas`), a column granted only to the personal-data role, a `sensible-texto` column; verify each broken entry makes its rule fail with the term and the column named, and that the real file passes none of them as a failure (design D4 rules 2 to 4)
- [x] 3.2 Add the value checks, failing first with broken entries: `aprobado` on `pedidos.estado` (not in the `CHECK`), a value on a column with no `CHECK`, no catalog and no `verificaContra`, a `verificaContra` that is not a catalog column; verify each fails by name, and that `propietario_actual` values are found in `identity.roles` and the cargo values in `designaciones.cargos` (design D4 rule 5)
- [x] 3.3 Add the explanation checks: a backticked identifier that does not exist fails, a qualified `schema.table.column` outside the references fails; verify with a broken entry for each

## 4. Dedications as a closed catalog

- [x] 4.1 Write the failing tests first: with the option off the declared list equals today's four columns; with it on the prefix lists «Categoría 1» to «Categoría 6» and the codes; `Ninguna_columna_declarada_pasa_el_tope_de_valores` covers the flagged list; verify they fail
- [x] 4.2 Add the second declared list (`designaciones.dedicaciones.nombre` and `.codigo`) to `LectorDeValoresDeCatalogo`, with the flag on `LeerAsync` and `ProveedorDeEsquema` passing the option (design D5); verify the tests of 4.1 pass and the existing four values keep their positions
- [x] 4.3 Extend the guard `Solo_se_enumeran_tablas_de_catalogo_sin_datos_personales` deliberately with `designaciones.dedicaciones` and a comment stating why it qualifies (six rows fixed by regulation, no personal data, every granted column `publica`); verify it still fails for an undeclared table by temporarily adding one
- [x] 4.4 Verify against the real database that a query using the rendered code form (`codigo = '3'`) executes, so the prefix does not teach a failing filter

## 5. Rendering

- [x] 5.1 Write the failing tests first: the block is byte-for-byte equal across two calls and two loads; the order is instructions, values, schema, glossary, examples (with `EjemplosEnElPrefijo` on); the examples block is the same string with the glossary on and off; the block is the same in the basic and the personal-data prefix; one line per term in file order; verify they fail
- [x] 5.2 Add the renderer of the block (from the parsed fields, not the file bytes) and the optional `bloqueDeGlosario` parameter of `RenderizadorDeEsquema.Renderizar`, appended at the end of both the compact and the non-compact branches; `ProveedorDeEsquema` passes it only with the option on (design D6); verify the tests of 5.1 pass
- [x] 5.3 Verify the default is untouched: with the option off `Renderizar` output is byte-identical to today's, and `PrefijoDeLosCassettesTests` and `PrefijoDeEsquemaTests` pass without any edit; verify the evaluator seal (`EsquemaParaPrompt.Huella`) differs with the option on and off

## 6. Prompt size

- [x] 6.1 Measure the block's characters and the full prompt of the longest request with the option on (profile: `EsquemaCompacto` and `EjemplosEnElPrefijo` on), from the llama-server log (`n_tokens`) or the operational log; verify it fits the 13,312-token slot and record the numbers in `docs/architecture/modelo-local.md`. If it does not fit, stop and report to the product owner; do not trim terms

## 7. Evaluation with the local model

- [x] 7.1 Run `eval-local/ARS-164/correr.sh` for the control (option off; the script reproduces the baseline profile of `qwen3-8b-q4-B`) into `eval-local/ARS-164/control/`, then `eval-local/comparar.py ARS-162/qwen3-8b-q4-B ARS-164/control`; verify zero items changed (26/34, 12/15, 10/11, 18/20). If any changed, the environment is not the baseline: fix that before going on
- [x] 7.2 Run `eval-local/ARS-164/correr.sh` with `Asistente__GlosarioEnElPrefijo=true` into `eval-local/ARS-164/glosario/`, then `comparar.py ARS-164/control ARS-164/glosario`; verify the reports are in `eval-local/ARS-164/glosario/` and list every changed item by axis with its direction. Never pass `--congelar`, and do not touch `backend/eval/lineas-de-base/`
- [x] 7.3 Apply the promotion rule (design D10) and record the decision with the item-level evidence in `docs/architecture/modelo-local.md` §8; verify that the numbers in the doc match the reports

## 8. Promotion (only if 7.3 passes; skipped, the gate failed)

- [x] 8.1 Not promoted, so this group was skipped by design: the promotion gate of 7.3 failed (capacidad and social worsened), `.env.example` and `infra/compose/compose.asistente-local.yml` are untouched and the option stays off everywhere

## 9. Documentation (in both outcomes)

- [x] 9.1 Update the module `README.md`: the option row, the «Optimizaciones para un modelo propio» section, and a short «Cómo se agrega un término al glosario» note (the verifications that run on their own, a value must be verifiable, the glossary and the datasets stay disjoint); verify `OpcionesDocumentadasTests` passes
- [x] 9.2 Update `docs/architecture/modelo-local.md` §6 (the technique, the option, the cassette consequence) and `docs/architecture/domains/asistente.md` where it describes the fixed part of the prompt; verify the order of the fixed part is described as in design D6
- [x] 9.3 Record the "Categoría 0" inconsistency (legacy `CHECK` and two column comments say 0 to 6, the catalog has 1 to 6) in `docs/quality/tech-debt.md` as a known schema inconsistency, not fixed here; verify the entry names the three places
- [x] 9.4 Record in the README note and in the PR description that the vocabulary was mined from the repository, PRs #27/#32/#38 and `feature/reserva-aulas`, was not validated with the Department (ARS-65) and was deliberately not derived from the evaluation datasets; verify the text is present

## 10. Verification

- [x] 10.1 Run `dotnet test --solution backend/ArsDocendi.slnx` and confirm it is green, including `ArquitecturaAsistenteTests`, `PrefijoDeLosCassettesTests`, `PrefijoDeEsquemaTests`, `EjemplosEjecutablesTests` and `OpcionesDocumentadasTests`
- [x] 10.2 Run `pnpm exec prettier --check` on the touched files and `pnpm exec openspec validate --all --strict`; verify both pass

<!-- Deferred, not a task of this change: the paid re-recording of the corpus with Claude and the Claude-side evaluation stay coupled to ARS-161 (one single re-recording for both). See design.md, "Deferred". -->
