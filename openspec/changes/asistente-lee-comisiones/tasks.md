## 0. Gates before writing code

- [x] 0.1 Turn boundaries decided by the team (morning before 12:00, afternoon 12:00 to 17:59, night from 18:00); the `dia` value for Saturday is read from the Guaraní client mapping in task 1.4; verify by writing the confirmed values in design.md D10 before tasks 1.4 and 5.2 start
- [x] 0.2 Sequencing resolved: `asistente-portal-por-ambito` is in `develop` and archived (2026-11-06); this change extends its per-domain rule (design D6)

## 1. The boundary in the engine

- [x] 1.1 Red first: a boundary test with a disposable `NOSUPERUSER NOBYPASSRLS` role, extending `RlsAlcanceTests`, that fails before the migration: an actor with `comisiones.ver` sees every comisión whatever their scope, an actor without it sees none, a connection with no actor sees none. Verify: `dotnet test backend/ArsDocendi.slnx --filter RlsAlcance` fails, then passes after 1.2
- [x] 1.2 `node scripts/migrations/scaffold.mjs Designaciones RlsAsistenteComisiones`: `ENABLE` (not `FORCE`) row level security and one `FOR SELECT` policy per exposed table, predicate `identity.asistente_tiene_permiso('comisiones.ver')`, header explaining D2 and the `ENABLE` choice. Verify: `node --test scripts/tests/migrations.test.mjs` and `node scripts/migrations/validate.mjs`
- [x] 1.3 A test that the owner connection still lists every comisión (the Comisiones screen and the importer are unaffected). Verify: the existing comisiones integration tests keep passing under `dotnet test backend/ArsDocendi.slnx`
- [ ] 1.4 `node scripts/migrations/scaffold.mjs Designaciones ComentariosAsistenteComisiones`: `COMMENT ON` for the four tables and every granted column, with the facts of the spec (several períodos, free-text `aula` and `responsabilidad`, `dia` as weekdays, the turn convention of 0.1, docente of a comisión versus designación, null link meaning "not matched"). Verify: migration checks as in 1.2 and the prefix test of 4.3
- [x] 1.5 `GRANT USAGE ON SCHEMA comisiones` and column-by-column `GRANT SELECT` on the four tables in `database/asistente/001_asistente_grants.sql`, with the D5 columns, explaining in a comment why this lives in `001` and why it runs after the RLS. Verify: `PrivilegiosLecturaTests` and `ManifiestoPrivilegiosTests` (all four directions)
- [x] 1.6 `manifiesto-privilegios.json`: `comisiones` schema becomes `expuesto`; the four tables `concedida` with granted and denied columns and the reason of each denied column; `sga_importaciones` as `denegada-explicita` with its reason. Verify: `Todo_schema_de_la_base_esta_clasificado_en_el_manifiesto` and the other three directions pass
- [x] 1.7 `manifiesto-sensibilidad.json`: classify every granted column `publica`, the docente `nombre` with the D1 reason. Verify: `ManifiestoSensibilidadTests`
- [x] 1.8 A privileges test that `sga_importaciones` fails for lack of privilege (not zero rows) and that neither role can mutate the exposed tables. Verify: `PrivilegiosLecturaTests`

## 2. Abstention by domain

- [x] 2.1 Red first: unit tests for the domain rule (comisiones-only needs only `comisiones.ver`; a global actor without it does not reach everything; comisiones plus designaciones needs both; identity-only keeps today's result). Verify: `dotnet test backend/ArsDocendi.slnx --filter PerfilDelActor` fails, then passes after 2.2 to 2.4
- [x] 2.2 `PerfilDelActor.VeComisiones` read live in `ConsultorDeAlcance`. Verify: a test that the flag follows the matrix without redeploy
- [x] 2.3 One detector that maps the tables of a generated query to domains (token-based, replacing the boolean `TablasQueToca(...).Count > 0` at the `CarrilSql` call sites and the `plan.TocaPortal` at `CarrilDelPlan`). Verify: detector tests including an alias and a comment that mention a domain name without touching it
- [x] 2.4 `PoliticaDeAbstencion.AlcanzaTodo` takes the domain set; empty-result text, retry decision and narration use it. Verify: the scenarios of `comisiones-visibilidad-asistente` as tests, including the scope notice on a non-empty cross-domain answer for a jefe de cátedra
- [x] 2.5 Extend the per-domain evaluation introduced by `asistente-portal-por-ambito` instead of adding a parallel one. Verify: the existing portal abstention tests and the new comisiones ones pass together

## 3. Imported períodos on an empty answer

- [x] 3.1 A reader of the imported períodos using the actor's connection, run only when the query touched comisiones and the result is empty; failure drops the sentence. Verify: tests for the three scenarios (names the períodos, no lookup outside comisiones, failing lookup still answers)

## 4. What the model and the user see

- [ ] 4.1 Add the four tables to `EtiquetasDeAreas.Mapa` under one label. Verify: the existing label tests plus an assertion that `comisiones` is in `SchemasHabilitados` and the "origen" text names it
- [ ] 4.2 Announce comisiones in `PresentacionPorRol` and in the catalog examples only with the live `comisiones.ver`, and never free aulas or the import log. Verify: `CapacidadesTests` for with and without the permission
- [ ] 4.3 Measure the prefix before and after (both roles) and check the spec facts: no `comisiones` column among the closed-value lists, the import log absent. Verify: the numbers are written in 6.1; `PrefijoDeEsquemaTests` assertions pass
- [ ] 4.4 A test that the prefix lists the real foreign keys of the comisiones tables (no logical references added). Verify: `PrefijoDeEsquemaTests`
- [ ] 4.5 Three or four examples in `ejemplos-sql.json` (docente's comisiones with days and aulas; docentes of a numbered comisión with responsabilidad; comisiones at night or on Saturday; a cross-check with designaciones), disjoint from the evaluator items. Verify: `SelectorDeEjemplosTests` validator test, the disjointness check, and an execution of each example against the fixture returning rows
- [ ] 4.6 Leave the glossary untouched. Verify: `CatalogoDeGlosarioTests` and `GlosarioInstitucionalTests` unchanged and green

## 5. Measurement

- [ ] 5.1 Seed comisiones in `GeneradorDeFixture` with declared cardinalities (two períodos of the same `anio_academico`, a docente without `persona_id`, a Saturday and a night schedule, free-text aula) and grant `comisiones.ver` to the evaluation actors. Verify: the fixture determinism test; an evaluation run where a comisiones item cannot pass without reading the snapshot
- [ ] 5.2 Add capacidad items (the questions in the proposal, with the turn values of 0.1) and robustez variants; add an item asking for "this período" whose reference answer is grouped by both períodos of the latest year (D10); add non-answerable items (free aulas). Verify: the dataset loads, every non-answerable item declares its refusal reasons, disjointness with the examples holds
- [ ] 5.3 Reclassify `cap-019` as answerable with its reference SQL and the reason written; check `cap-020`, `cap-039` and the dialogue items that say "dicta" against D8. Verify: the dataset diff shows each reclassification with its reason
- [ ] 5.4 Measure the longest request against the 3070 profile (one slot, 16,384): prefix plus request plus output ceiling fits. If not, compact comments, then examples; if the default 2 x 13,312 profile no longer fits, say so in `docs/architecture/modelo-local.md`. Verify: the measured numbers are in the doc
- [ ] 5.5 Funded run: re-record the baselines, archive the previous ones with the list of items that moved, freeze the new ones. Verify: the regression gate compares again and `lineas-de-base/README.md` records the diff

## 6. Documentation

- [ ] 6.1 `docs/architecture/domains/asistente.md`: add the comisiones question catalog (what can and cannot be asked, free aulas excluded), the prefix numbers of 4.3, and the abstention-by-domain rule. Verify: `pnpm format:check` and `node scripts/docs/check-links.mjs`
- [ ] 6.2 `docs/architecture/data-model.md` (comisiones section: now readable by the assistant under RLS) and `backend/src/Modules.Asistente/README.md`. Verify: same commands as 6.1
- [ ] 6.3 `docs/business-rules/asistente.md`: reword BR-asistente-001 and BR-asistente-003 (the example and the "scope and permission" statement now hold per domain, comisiones being permission only), and add BR-asistente-010 for D2 with "No es normativa" and its test mapping. Verify: the rule format check in `pnpm format:check`; the `_index.md` already lists the file
- [ ] 6.4 State in the PR that there is no assistant article in the Centro de ayuda (`frontend/src/features/ayuda/contenido/` has none), so rule 9 adds no article. Verify: `grep -ril asistente frontend/src/features/ayuda/contenido` shows only the dashboard article and the index

## 7. Verification

- [ ] 7.1 Backend: `dotnet test backend/ArsDocendi.slnx`
- [ ] 7.2 SQL and migrations: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, then after `dotnet build -c Release`, `dotnet run --project backend/src/ArsDocendi.Host --no-build -c Release -- --validar-recursos`
- [ ] 7.3 Docs and specs: `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`
- [ ] 7.4 Record any check the environment could not complete in the PR description
