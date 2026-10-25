## 0. Prerequisites

- [ ] 0.1 Archive `asistente-feedback-export-seguimiento` (42/42 tasks done, still unarchived on this branch) so `asistente-retroalimentacion` is under `openspec/specs/` — BLOCKED 2026-10-25: `openspec archive` refuses it because its delta modifies `asistente-contrato-de-respuesta`, which only exists in other unarchived changes (`asistente-superficie-api` and its chain). Not needed by this change, which reads the feedback tables but modifies no feedback spec; archive the chain separately.
- [x] 0.2 Decide the minimum vote count below which the trend arrow is suppressed — decided 2026-10-24: 20 votes in either period, recorded as a named constant when the endpoint is written
- [x] 0.3 Re-derived 2026-10-25: the last `database/asistente` file is `009_asistente_modelo.sql`, so this change uses `010`; the last Identity id was `20261020000950` and the highest seeded UUID `b2000000-0000-4000-8000-000000000032`, so the new ones are `20261025020726_PermisoAsistenteRevisarCalidadIdentity` and `b2000000-0000-4000-8000-000000000033`. Original text: re-derive the next free `database/asistente` number (`asistente-modelo-en-caliente` uses `009`, this change uses `010`) and the next free Identity migration id and permission UUID against the target base immediately before writing them (today the last Identity id is `20261020000950` and the highest seeded UUID is `b2000000-0000-4000-8000-000000000032`)
- [x] 0.4 Human confirmation of the two privacy and scope questions in design D1 and D3 — confirmed 2026-10-24: policy denials stay out of the rates and are shown as a separate count; full `modelo` accepted under TD-012

## 1. Data additions

- [x] 1.1 New file `database/asistente/010_asistente_calidad.sql` (never edit a published file) — `ALTER TABLE asistente.registro_analitico ADD COLUMN IF NOT EXISTS` for `causa text NULL` with a `CHECK` over the 22 codes of design D2, `modelo text NULL`, and `reemplazado boolean NOT NULL DEFAULT false`, with `COMMENT ON COLUMN` that repeats the TD-012 warning against adding an actor or a precise time to the analytic table; plus `asistente.auditoria_revision_de_calidad(id, lector_id, ocurrido_en, filtros, filas)` with `IF NOT EXISTS`, comments and an `ocurrido_en` index, no foreign keys
- [x] 1.2 Static wrapper class (`RecursoSql`, `AplicarAsync`), embedded resource, and registration in `MigradorAsistente.Recursos` before `RecursoVersion`
- [x] 1.3 Add `causa` and `modelo` to `RegistroDelTurno.ColumnasQueEscribe["registro_analitico"]` so `MigradorAsistente.VerificarColumnasDelRegistroAsync` refuses to start without them; add an explicit check for `reemplazado` and the new audit table (written by a separate UPDATE and INSERT); update the migrator's error text, which still points at `002` for new columns
- [x] 1.4 Confirm the manifests need no change: the `asistente` schema is `denegado` wholesale in `manifiesto-privilegios.json` and neither manifest lists schema-internal columns; extend `PrivilegiosLecturaTests` with the new table
- [x] 1.5 Add `Permisos.AsistenteRevisarCalidad = "asistente.revisar_calidad"` and its entry in `Permisos.Todos` in `ArsDocendi.Shared/Auth/Permisos.cs`, with the XML doc stating it is seeded to no role
- [x] 1.6 Identity incremental (scaffolded like `20261020000900_PermisoAsistenteAdministrarIdentity`: `.sql` plus `.cs` and the model snapshot as the scaffold produces them) seeding `asistente.revisar_calidad` with `ON CONFLICT (code) DO NOTHING` and granting it to no role, not even `sys_admin`, with the guard that logs any non-empty grant
- [x] 1.7 Retention for the new audit table: a `RetencionDeAuditoriaDeCalidadDias` option (default 365) in `OpcionesAsistente`, an entry in `ValidadorDeOpcionesAsistente.EstrictamentePositivas`, and a DELETE in `PurgaDeRegistros` like the other audit tables
- [x] 1.8 Test: migration applies twice without error and passes the `ArquitecturaAsistenteTests` DDL rules (only `ADD COLUMN IF NOT EXISTS`, no `DROP` / `RENAME` / `ALTER COLUMN ... TYPE`)
- [x] 1.9 Test: the new permission exists and is assigned to no role after migration

## 2. Recording

- [x] 2.1 Add an internal `Causa` code type (the 22 codes of design D2, with a `Familia` of provider, query, signal or policy) and extend `TurnoParaRegistrar` / `RegistroDelTurno` to carry `Causa` and `Modelo` (`proveedor.Nombre`, always the resolved adapter); `reemplazado` is written by its own UPDATE
- [x] 2.2 Carry the cause out of every failure or empty path as data on `ResultadoDelTurno` (not by re-deriving it from the text): the `CarrilSql.ResponderAsync` catch chain (`TechoDeLlamadasSuperado`, `ProveedorNoDisponible`, `ProveedorSaturado`, `TimeoutDelProveedor`, `HttpRequestException` split by `StatusCode` 401/403, 404 and other, non-request `TaskCanceledException`, `ConsultaSinPrivilegio`, `ConsultaRechazadaPorElMotor` split by SQLSTATE `57014`), the validator rejection, the truncated and the unintelligible generation, `CarrilSql.Vacio` (`resultado_vacio`), `CarrilDelPlan.Abstencion` (`plan_invalido`), and in `CapaConversacional` the `MotivoSinModelo` paths (`cupo_agotado`, `tope_organizacional`, `mantenimiento`, `acceso_revocado`, `proveedor_no_disponible` from the availability pre-check), the refused turn lock (`turno_concurrente`), the expired turn budget (`presupuesto_del_turno`) and the unhandled exception (`excepcion`, state `Fallo`)
- [x] 2.3 A model's own declination keeps recording only `motivo_rechazo` on the operational record and no `causa`
- [x] 2.4 Record `modelo` on each analytic record, including turns that never called a model
- [x] 2.5 On a replacement that completes, `UPDATE registro_analitico SET reemplazado = true WHERE id = <old ClaveDeRetroalimentacion>` from the registration step in `CapaConversacional`; when `IdentidadDelUltimoRegistrado.ClaveDeRetroalimentacion` is null (any non-`Respondida` turn, or after a restart) set nothing and document it; a replacement that ends in `Fallo` sets nothing
- [x] 2.6 Test: each of the 22 codes is written by its own path (one test per code), including 429 and 503 from the shared service as `proveedor_saturado` with the breaker still closed
- [x] 2.7 Test: an empty result is recorded as `Respondida` with `causa = resultado_vacio`
- [x] 2.8 Test: replacing a `Respondida` turn sets `reemplazado` on that turn only, keeps its existing vote, and a replacement that fails sets nothing
- [x] 2.9 Test: `registro_analitico.modelo` equals the adapter name used by the turn, and `registro_operativo.proveedor` is unchanged
- [x] 2.10 Test: a cause always agrees with its state (design D2 table), checked over the recorded paths

## 3. Backend: aggregates and problem list

- [ ] 3.1 Aggregate reader `ConsultasDeCalidad` reading only `registro_analitico` and `retroalimentacion_turno`, never `actualizado_en` or `comentario` (negative rate, negatives by reason, failures by cause, problems once per turn, coverage, per-model table with turn counts, series by `dia`, previous-period trend omitted below 20 votes in either period); policy-family turns excluded from numerators and denominators and returned as a separate count by policy cause
- [ ] 3.2 `GET api/asistente/administracion/calidad?periodo=7|30|90&modelo=` (policy `asistente.administrar`); reject any other period; no route collides with `uso`, `presupuestos`, `servidor-local`, `tope-organizacional`, `mantenimiento`
- [ ] 3.3 `GET api/asistente/administracion/calidad/problemas?periodo&modelo&tipo=negativa|fallo|reemplazo&pagina` (permission `asistente.revisar_calidad`) returning anonymous rows: question text, reason or cause, type, model, day, state
- [ ] 3.4 Audit every successful read of the problem list into `asistente.auditoria_revision_de_calidad` (reader, moment, filters, row count), written before responding, append-only, following `auditoria_acceso_historial`
- [ ] 3.5 Extend `ArquitecturaAsistenteTests` (a new rule: no allowlist of `retroalimentacion_turno` readers exists today): only the quality readers may read `retroalimentacion_turno`; they reference neither `registro_operativo`, an actor column, `actualizado_en` nor `comentario`; update `PuedenUsarLaCadenaDelDueno` and `SuperficiePublicaDeApplication` (98 today) only by what the change adds; ratify nothing unless the `causa` CHECK is later replaced
- [ ] 3.6 Test: aggregate correctness against a seeded set (rates, reasons, causes, coverage, once-per-turn "with problems", policy turns excluded from the rates and counted separately, trend omitted below 20 votes)
- [ ] 3.7 Test: the trend compares against the previous period of equal length and is marked unavailable outside retention
- [ ] 3.8 Test: no endpoint returns `comentario` or `actualizado_en` (response-shape scan plus a seeded comment that must not appear)
- [ ] 3.9 Test: the problem list returns 403 without `asistente.revisar_calidad`, including for a holder of `asistente.administrar` alone
- [ ] 3.10 Test: a successful problem-list read writes exactly one audit row, and a refused read writes none
- [ ] 3.11 Test: aggregates return 403 without `asistente.administrar`

## 4. Frontend: "Calidad" tab

- [ ] 4.1 `frontend/src/features/sistema/hooks/useSeccionSistema.ts` and `pages/SistemaPage.tsx` — add `calidad` to `IdPestanaSistema`, `ORDEN` (after `modelo`, before `auditoria`; right after `asistente` until `asistente-modelo-en-caliente` lands), `PERMISO_POR_PESTANA` (`asistente.administrar`), `NOMBRES_PESTANAS`, the tab content, the "Actualizar" query-key prefix and the status-dot tone. `app/shell/nav.ts` needs no change
- [ ] 4.2 API hooks for both endpoints
- [ ] 4.3 Header: title "Calidad de las respuestas", model select, period 7/30/90
- [ ] 4.4 KPI row: "Respuestas negativas" first, wider, red-accented, with trend and an amber note when rising; then "Con problemas", "Fallos detectables", "Positivas", "Cobertura de voto" (reusing `KpisDeUso` styles where they fit)
- [ ] 4.5 Per-day negative-percentage bar chart: single series, average reference line, hover tooltip, accessible label and table alternative; weekly buckets at 90 days
- [ ] 4.6 "Por qué no sirvió" and "Fallos por causa" bars; `resultado_vacio` shown as a signal; saturation shown as "ocupado" and not as an outage
- [ ] 4.7 "Por modelo" table with turn counts
- [ ] 4.8 "Respuestas con problemas" table with filters Todas / Pulgar abajo / Fallos / Reemplazadas, pagination, and a privacy footnote; replaced by an information notice without `asistente.revisar_calidad` (and no problem-list request is made)
- [ ] 4.9 Tab status dot turns to warning when the negative rate rises
- [ ] 4.10 Optional, only if `asistente-modelo-en-caliente` is merged: show each model's 30-day negative rate in the Modelo tab dropdown
- [ ] 4.11 Vitest: "Respuestas negativas" is rendered first with the emphasis class and trend note
- [ ] 4.12 Vitest: filters change the requested `tipo` and the visible rows
- [ ] 4.13 Vitest (`SistemaPage.test.tsx`, `routes.test.tsx`): permission gating of the tab and of the problem table (notice shown instead)
- [ ] 4.14 Vitest: chart has an accessible label and table alternative
- [ ] 4.15 Update the design specs (`docs/product/designs/administracion-sistema-design-spec.md`, `docs/product/designs/asistente-conversacional-design-spec.md`) where they describe the Sistema tabs (AGENTS.md rule 9)

## 5. Documentation and verification

- [ ] 5.1 `docs/architecture/domains/asistente.md`, `docs/architecture/api-contracts.md`, `docs/architecture/data-model.md`
- [ ] 5.2 Permissions documentation, if any, for `asistente.revisar_calidad`
- [ ] 5.3 `docs/quality/tech-debt.md` — new entry for the in-memory voting token lost on restart and expiring after `VigenciaDeRetroalimentacionMinutos`; add a TD-012 note that the aggregates respect it and that `modelo` is an attribute shared with `registro_operativo.proveedor`
- [ ] 5.4 Run the proportional verification: `dotnet test backend/ArsDocendi.slnx`, `pnpm --filter frontend test:run`, `pnpm --filter frontend lint`, `pnpm --filter frontend build`, `pnpm format:check`, `pnpm exec openspec validate --all --strict`; record any check the environment could not complete
