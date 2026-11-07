## 1. The boundary in the engine

- [x] 1.1 Boundary tests with a disposable non-superuser role in `RlsAlcanceTests`: approver sees all, requester sees only own (strict subset), no permission and no actor see none, requester without persona sees none, owner still sees all, `ENABLE` without `FORCE` and one SELECT policy. Verify: `dotnet test --project backend/tests/ArsDocendi.IntegrationTests/ArsDocendi.IntegrationTests.csproj --filter "FullyQualifiedName~RlsAlcance"`
- [x] 1.2 `node scripts/migrations/scaffold.mjs Aulas RlsAsistenteSolicitudes`: `ENABLE` row level security and the D1 policy, header explaining D1 and the `ENABLE` choice. Verify: `node --test scripts/tests/migrations.test.mjs` and `node scripts/migrations/validate.mjs`
- [x] 1.3 `node scripts/migrations/scaffold.mjs Aulas ComentariosAsistenteSolicitudes`: `COMMENT ON` for the table and every column, with the D4 disambiguation and the nullability facts. Verify: migration checks as in 1.2 and the prefix test of 3.3
- [x] 1.4 `GRANT USAGE ON SCHEMA aulas` and a column-by-column `GRANT SELECT` in `database/asistente/001_asistente_grants.sql`, explaining why it runs after the RLS. Verify: `PrivilegiosLecturaTests` and `ManifiestoPrivilegiosTests`
- [x] 1.5 `manifiesto-privilegios.json`: `aulas` schema becomes `expuesto`, the table `concedida`, `aulas.__EFMigrationsHistory` `denegada-explicita`. Verify: the four directions of `ManifiestoPrivilegiosTests`
- [x] 1.6 `manifiesto-sensibilidad.json`: classify every column (D2). Verify: `ManifiestoSensibilidadTests`
- [x] 1.7 A privileges test that neither role can mutate the table. Verify: `PrivilegiosLecturaTests`

## 2. Abstention by domain

- [x] 2.1 `DominioConsultado.Aulas` in `DetectorDeDominios`; tests that an alias or a comment naming the table does not touch it. Verify: `AbstencionYRedaccionTests`
- [x] 2.2 `PerfilDelActor.VeSolicitudesDeAula` and `VeSolicitudesPropias` read live in `ConsultorDeAlcance`. Verify: `PerfilDelActorTests.Los_permisos_de_aulas_siguen_la_matriz_sin_redeploy`
- [x] 2.3 `PoliticaDeAbstencion.AlcanzaTodo` handles `Aulas` (D3), including the cross-domain cases. Verify: the aulas theory in `AbstencionYRedaccionTests`

## 3. What the model and the user see

- [x] 3.1 `aulas.solicitudes_reserva` in `EtiquetasDeAreas`; `PresentacionPorRol` and `CatalogoDeCapacidades` announce by the live permissions (approvers: all; requesters: own; others: nothing), never free aulas. Verify: `CapacidadesTests.Las_solicitudes_de_aula_se_anuncian_segun_el_permiso_vivo_y_dicen_de_quien_son`
- [x] 3.2 Three examples in `ejemplos-sql.json`, disjoint from the evaluator items. Verify: `SelectorDeEjemplosTests`, `DatasetDeCapacidadTests` disjointness, and `Cada_ejemplo_de_aulas_ejecuta_y_devuelve_filas_contra_el_fixture`
- [x] 3.3 Measure the prefix before and after (both roles) and check the spec facts: no `aulas` closed-value list, real foreign keys. Verify: numbers written in 5.1; `PrefijoDeEsquemaTests`
- [x] 3.4 Leave the glossary untouched. Verify: `CatalogoDeGlosarioTests` and `GlosarioInstitucionalTests` green

## 4. Measurement

- [x] 4.1 Seed requests in `GeneradorDeFixture` (D6) and grant the two permissions to the evaluation roles. Verify: the fixture determinism test and the executable-references test
- [x] 4.2 Add `cap-051` to `cap-056` and `rob-019`, `rob-020`; leave `cap-048`/`cap-049` unchanged (D5). Verify: `DatasetDeCapacidadTests`, `ReferenciasEjecutablesTests`
- [ ] 4.3 Measure the longest request against the 3070 profile (one slot, 16,384): prefix plus request plus output ceiling fits (orchestrator, with the local server)
- [ ] 4.4 Funded run: re-record the baselines, archive the previous ones, freeze the new ones (orchestrator)

## 5. Documentation

- [x] 5.1 `docs/architecture/domains/asistente.md`: the aulas question catalog (what can and cannot be asked), the prefix numbers of 3.3, and the abstention rule. Verify: `pnpm format:check` and `node scripts/docs/check-links.mjs`
- [x] 5.2 `docs/architecture/data-model.md` (aulas section: readable by the assistant under RLS) and `backend/src/Modules.Asistente/README.md`. Verify: same commands as 5.1
- [x] 5.3 `docs/business-rules/asistente.md`: BR-asistente-003 clause and `BR-asistente-011` (D7). Verify: `pnpm format:check`
- [x] 5.4 There is no assistant article in the Centro de ayuda, so rule 9 adds none; state it in the PR. Verify: `grep -ril asistente frontend/src/features/ayuda/contenido`

## 6. Verification

- [x] 6.1 Backend: `dotnet test --project backend/tests/ArsDocendi.IntegrationTests/ArsDocendi.IntegrationTests.csproj --filter "FullyQualifiedName~Asistente"`; `Sin_proveedor_local_la_telemetria_dice_que_no_esta_configurado` fails in a checkout with a local `.env`, not because of this change
- [x] 6.2 SQL and migrations: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, then after `dotnet build -c Release`, `dotnet run --project backend/src/ArsDocendi.Host --no-build -c Release -- --validar-recursos`
- [x] 6.3 Docs and specs: `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`
