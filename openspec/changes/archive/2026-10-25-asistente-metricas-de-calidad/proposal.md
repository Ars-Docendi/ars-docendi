## Why

Users can already rate an answer (thumbs up, or "No sirvió" with one or more reasons), but nobody can see the ratings, and failures collapse into coarse outcome states. Administrators need a quality metric that puts positively and negatively rated answers side by side, with emphasis on the negative ones and on answers with detectable failures, and that can be compared per model (which ties into `asistente-modelo-en-caliente`).

Verified current state (this branch, head of PR #40):

- Voting UI: `frontend/src/features/asistente/components/VotoDeRetroalimentacion.tsx`, posting to `POST /api/asistente/retroalimentacion` (`AsistenteController.Retroalimentacion`, policy `asistente.consultar`), handled by `ServicioDeRetroalimentacion`.
- Storage: `asistente.retroalimentacion_turno(analitico_id PK FK registro_analitico ON DELETE CASCADE, voto boolean, razones text[] CHECK in {datos_incorrectos, no_entendio_la_pregunta, faltan_datos, otro}, comentario text <= 500, actualizado_en)` (`database/asistente/003_asistente_retroalimentacion.sql`): one row per turn, upserted by `RegistroDeRetroalimentacion`, no history. It is linked only to `registro_analitico(id, pregunta, categoria, estado, dia)`, never to the actor nor to `registro_operativo` (TD-012, deliberate non-joinability). `actualizado_en` is a precise timestamp and is **not** to be read by any aggregate: the day of a vote is the turn's `dia`. Retention is 90 days (`RetencionDeRegistrosDias`).
- The voting token lives in memory (`ValidezDeRetroalimentacionEnMemoria`), is valid `VigenciaDeRetroalimentacionMinutos` (default 120) after the turn, is purged on registration of a new token (commit `a1c485d8`), is revoked when the turn is replaced, and is lost on restart.
- No read surface exists for votes or comments; the spec of `asistente-rediseno-v3` forbids a read surface for the comment.
- `registro_operativo.estado` is one of Respondida, NoContestable, NecesitaAclaracion, ServicioDegradado, Fallo, with `carril`, latency, tokens, `proveedor`, `hubo_reintento`, `truncado` and `motivo_rechazo` (model declines only). Causes collapse: breaker, timeout, saturation, HTTP error, call ceiling and the turn budget all become ServicioDegradado; validator rejection, engine error, statement timeout, no privilege, a truncated or unintelligible generation and an invalid plan become NoContestable; anything unhandled is Fallo (see `CarrilSql.ResponderAsync` and `CapaConversacional`). An empty result is `Respondida`. A replacement (edit and resend, `HiloConversacional.QuitarUltimoParaReemplazo`) is not recorded.
- The usage panel (`GET api/asistente/administracion/uso`, `ConsultasDeUso`, `KpisDeUso.tsx`, `PanelDeUso.tsx`, embedded in the Sistema "Asistente" tab) reads only `registro_operativo`.
- The Sistema screen has three tabs today (`estado`, `asistente`, `auditoria`) in `useSeccionSistema.ts` / `SistemaPage.tsx`.

## What Changes

- **Quality metrics endpoint** `GET api/asistente/administracion/calidad` (permission `asistente.administrar`): negative rate (headline), negatives by reason, detectable failures by cause, "answers with problems", vote coverage, per-model table, series (one point per day, per week for 90 days, covering the whole period with gaps for days without votes), `modelosDisponibles` (every model seen in the period, ignoring the model filter, for the select) and trend against the previous period of equal length. Failures by cause include `resultado_vacio` as a signal (`familia: senal`) and a `sin_clasificar` bucket for a failing turn that recorded no cause; policy denials come back as a separate count by cause.
- **A problem list endpoint** `GET api/asistente/administracion/calidad/problemas` behind a **new permission `asistente.revisar_calidad`**, seeded to no role. Holding `asistente.revisar_calidad` alone opens this list (the API does not require `asistente.administrar`) but not the aggregates; the Sistema tab itself is gated by `asistente.administrar`. It returns anonymous rows (question text, reason or cause, type, model, day, state) with no actor and no free-text comment, and every read is audited.
- **Three data additions on `registro_analitico`** (new numbered SQL): `causa` (a closed code set recorded on each failure or empty path), `modelo` (`empresa/modelo`, low cardinality) and `reemplazado boolean` (set when the turn is replaced). `causa` lives on the **analytic** record, not the operational one, so the quality reader can stay confined to analytic plus feedback; it is not shared with `registro_operativo`, so it adds no join selector (the attribute that is shared, `modelo`, is discussed in design D3).
- **A "Calidad" tab** in the Sistema screen, between "Modelo" and "Auditoría" (tab order Estado, Asistente, Modelo, Calidad, Auditoría): header with title "Calidad de las respuestas", model select and period 7/30/90; a KPI row in which "Respuestas negativas" comes first, is wider and red-accented, with a trend note; "Con problemas", "Fallos detectables", "Positivas" and "Cobertura de voto"; a per-day bar chart of negative percentage; "Por qué no sirvió" and "Fallos por causa" bars; a "Por modelo" table; and a filterable "Respuestas con problemas" table (Todas, Pulgar abajo, Fallos, Reemplazadas) with a privacy footnote, replaced by an information notice when the viewer lacks the permission. The tab status dot turns to warning when the 30-day negative rate rises against the previous 30 days (independent of the period and model selected inside the tab). Showing each model's 30-day negative rate in the Modelo tab dropdown was optional and is **not built** (tasks 4.10).
- **Prerequisite housekeeping**: archive `asistente-feedback-export-seguimiento` (42 of 42 tasks done, still unarchived on this branch) so its `asistente-retroalimentacion` capability exists under `openspec/specs/` before this change builds on it.
- **Documentation in the same diff**: `docs/architecture/domains/asistente.md`, `api-contracts.md`, `data-model.md`, the permissions documentation if any, and `docs/quality/tech-debt.md` (the in-memory voting token, and a TD-012 note that aggregates respect it).

## Capabilities

### New Capabilities

- `asistente-metricas-de-calidad`: aggregate quality metrics, the failure-cause and replacement signals they need, the audited anonymous problem list, and the Calidad tab.

### Modified Capabilities

<!-- None at the requirement level. The feedback and usage capabilities keep their contracts; this change adds recorded fields and a new read surface under a new capability. `asistente-retroalimentacion` is not yet under openspec/specs/ until the prerequisite archive runs. -->

## Impact

- **Affected module**: `Modules.Asistente` (the failure paths in `CarrilSql`, `CarrilDelPlan` and `CapaConversacional`, `TurnoParaRegistrar` / `IRegistroDelTurno` / `RegistroDelTurno`, a new aggregate reader, a new controller section), `database/asistente/`, `ArsDocendi.Shared/Auth/Permisos.cs`, `database/identity/` (permission incremental), `frontend/src/features/sistema/`, and `ArquitecturaAsistenteTests`.
- **Dependency graph**: no new edges. The new permission is seeded by an Identity incremental migration (`database/identity/<next id after 20261020000950>_PermisoAsistenteRevisarCalidadIdentity.sql` plus its `.cs`, scaffolded like the existing ones); the Asistente only checks it.
- **Database**: new numbered `database/asistente/010_asistente_calidad.sql` (008 is taken by `008_asistente_presupuesto_usuario_vigente_unico.sql`; `asistente-modelo-en-caliente` takes 009; renumber both together if either lands out of order) with a static wrapper class registered in `MigradorAsistente.Recursos` before `RecursoVersion` (asistente SQL has no EF history), and no manifest entries (the whole `asistente` schema is `denegado` to both read-only roles in `manifiesto-privilegios.json`, and the manifests list only granted columns). The guard allows `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`; a later change to the `causa` code set needs a ratified CHECK replacement. No published SQL file is edited.
- **Privacy**: aggregates join `registro_analitico` with `retroalimentacion_turno` only, which are already linked by id. Nothing joins either to the actor or to `registro_operativo` per row. TD-012 is preserved.
- **Public API**: two new administration routes (existing ones: `uso`, `presupuestos`, `servidor-local`, `tope-organizacional`, `mantenimiento`); no change to `Modules.Asistente.Contracts`.
- **Institutional business rules**: not applicable; no `BR-*` is introduced.
- **Rollback**: columns are additive and nullable or defaulted; removing the routes and the tab leaves recording harmless.
- **Artifact language**: English, per the Language Domain Contract (the project's local preferences override `openspec/config.yaml` for newly generated artifacts); existing Spanish code and product documents stay as they are.

## Out of Scope

- Showing or storing the free-text comment anywhere readable.
- Linking votes to users or to operational rows.
- Persisting the voting token across restarts (recorded as tech debt).
- Windows longer than 90 days (bounded by retention).
- Alerting or notifications on metric changes beyond the tab status dot.
- Automatic quality-based model switching.
