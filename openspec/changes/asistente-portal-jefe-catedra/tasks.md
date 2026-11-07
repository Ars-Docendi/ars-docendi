## 0. Gates before writing code

- [x] 0.1 `asistente-lee-aulas` is merged or this change is rebased on it (both touch `database/asistente/*`, the fixture and the datasets). Verify: `git log` shows its commits in the branch
- [x] 0.2 Re-confirm that nothing outside the assistant consumes `portal.ver_trayectoria_ajena`. Verify: `grep -rIn ver_trayectoria_ajena backend/src frontend/src database scripts` lists only `ConsultorDeAlcance`, portal/designaciones RLS, the fixture and comments
- [ ] 0.3 NOT DONE, owner team / área legal (D5): verify the Ley 25.326 citations against the current text and consult the área legal on AAIP registration of the data bank. Does not block the code; keep the «pending verification» markers in the business rules until done

## 1. The permission

- [x] 1.1 Red first: change `PermisoYPersonaPortalTests.El_permiso_existe_y_no_lo_tiene_ningun_rol` to assert that exactly `jefe_catedra` holds it, and add a test that a `jefe_catedra` actor with a designación sees the profile of a docente designated in their materia and not of one designated elsewhere. Verify: `dotnet test backend/ArsDocendi.slnx --filter RlsPortalAsistente` fails, then passes after 1.2
- [x] 1.2 `node scripts/migrations/scaffold.mjs Identity PermisoPortalTrayectoriaAjenaJefeCatedra`: insert the `jefe_catedra` row in `identity.rol_permisos`, idempotent, header with D1 and the blast radius. Verify: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`
- [x] 1.3 Correct the stale «concedido A NADIE» claim: `001` is protected and cannot be edited, so the corrected explanation lives in the portal block of `database/asistente/011_asistente_grants_dominios.sql` in the same diff as 2.3. Verify: `ManifiestoPrivilegiosTests`

## 2. Projects

- [x] 2.1 Red first: boundary test with a disposable `NOSUPERUSER NOBYPASSRLS` role: a jefe sees projects of docentes in their materias and none of others; no actor sees none; the owner connection sees all; `descripcion` fails for lack of privilege (not zero rows). Verify: `dotnet test backend/ArsDocendi.slnx --filter RlsPortal` fails, then passes after 2.2 and 2.3
- [x] 2.2 `node scripts/migrations/scaffold.mjs Portal RlsAsistenteProyectos`: `ENABLE` (not `FORCE`) RLS and `asistente_ve_proyectos` with the `educaciones` pattern. Verify: migration checks as in 1.2
- [x] 2.3 `GRANT SELECT (id, perfil_id, nombre, rol, desde, hasta, doi, created_at) ON portal.proyectos` to both roles in `011_asistente_grants_dominios.sql` after the RLS. Verify: `PrivilegiosLecturaTests`
- [x] 2.4 `manifiesto-privilegios.json`: `proyectos` `concedida` with granted columns and denied `descripcion` with its reason; reword the `portal` schema `motivo`; `proyecto_documentos` unchanged. Verify: all four directions of `ManifiestoPrivilegiosTests`
- [x] 2.5 `manifiesto-sensibilidad.json`: classify the granted columns. Verify: `ManifiestoSensibilidadTests`
- [x] 2.6 `node scripts/migrations/scaffold.mjs Portal ComentariosAsistenteProyectos`: `COMMENT ON` for the table and granted columns with the facts of the spec. Verify: migration checks; prefix test of 3.2

## 3. What the model sees

- [x] 3.1 Add `portal.proyectos` to the area labels if `EtiquetasDeAreas` lists tables of the portal area. Verify: existing label tests plus an assertion that the origin text names projects
- [x] 3.2 Prefix assertions: `descripcion` absent, the real FK `proyectos.perfil_id` listed, no row values. Verify: `PrefijoDeEsquemaTests`
- [x] 3.3 Three or four examples in `ejemplos-sql.json` searching skills with several related terms via `termino_norm`, one joined to education and one to projects, disjoint from the eval items. Verify: `SelectorDeEjemplosTests`, the disjointness check, and each example executed against the fixture returning rows
- [x] 3.4 Red first: the answer to a profile search for a materia states that the portal records what docentes declared (not requirements) and states coverage. Verify: a narration/answer test fails, then passes; reuse `CoberturaDelPortal`, do not add a second coverage detection
- [x] 3.5 The jefe's catalog announces the docente search with the cátedra scope and never contact, CV content, project description or requirements matching. Verify: `CapacidadesTests` with and without the permission

## 4. Measurement

- [x] 4.1 `GeneradorDeFixture`: grant the permission to the jefe/materia eval actor and seed portal data (skills with related terms, a posgrado, projects with DOI) for some docentes inside their materias and one outside. Verify: fixture determinism test; an item cannot pass without reading
- [x] 4.2 Add about 5 capacidad items (D6) and 1 or 2 robustez variants; the outside-cátedra item excludes that docente. Verify: the dataset loads, disjointness with the examples holds
- [x] 4.3 ORCHESTRATOR: funded run, re-record baselines, archive the previous ones with the list of items that moved. Verify: regression gate compares again; `lineas-de-base/README.md` records the diff — done in dcec44d4 together with asistente-lee-aulas
- [x] 4.4 ORCHESTRATOR: measure the longest request against the 3070 profile (one slot, 16,384) after this change and `asistente-lee-aulas`; compare with 12,846 + 600. Verify: numbers written in `docs/architecture/modelo-local.md`; if over budget, compact comments, then examples — 15,577 of 16,384, fits; no compaction

## 5. Documentation

- [x] 5.1 `docs/business-rules/portal.md`: rewrite BR-portal-001 and BR-portal-002 as the team decision of 2026-11-07 (D4), update the Implementation and test mapping, keep legal citations marked pending (D5). Verify: `pnpm format:check`
- [x] 5.2 `docs/product/consulta-secretaria-portal-asistente.md`: rewrite «Ampliación del 2026-11-06» as a decision record (permission to the jefe, option A, Proyectos without descripción). Verify: `pnpm format:check`, `node scripts/docs/check-links.mjs`
- [x] 5.3 `docs/architecture/domains/asistente.md`, `docs/architecture/domains/portal.md`, `docs/architecture/data-model.md`, `backend/src/Modules.Asistente/README.md`, and `docs/business-rules/asistente.md` where its examples say the permission is unassigned. Verify: same commands as 5.2
- [ ] 5.4 State in the PR that there is no assistant article in the Centro de ayuda, so rule 9 adds none. Verify: `grep -ril asistente frontend/src/features/ayuda/contenido`

## 6. Verification

- [x] 6.1 Backend: `dotnet test backend/ArsDocendi.slnx`
- [x] 6.2 SQL and migrations: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, then after `dotnet build -c Release`, `dotnet run --project backend/src/ArsDocendi.Host --no-build -c Release -- --validar-recursos`
- [x] 6.3 Docs and specs: `pnpm format:check`, `node scripts/docs/check-links.mjs`, `pnpm exec openspec validate --all`
- [x] 6.4 Record any check the environment could not complete in the PR description
