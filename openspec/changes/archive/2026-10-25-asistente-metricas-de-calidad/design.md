## Context

Votes live in `asistente.retroalimentacion_turno`, keyed by the analytic record id. The analytic record deliberately carries no actor and no precise time; the operational record carries latency, tokens and outcome but no question. The two are not joinable, on purpose (TD-012). Any quality metric has to be built inside that constraint.

Two facts about the current code shape the design:

- `RegistroDelTurno.ColumnasQueEscribe` lists, per table, the columns the two INSERTs write; `MigradorAsistente.VerificarColumnasDelRegistroAsync` derives its start-up check from that list, so a new recorded column is verified by being added there. A column written by a separate UPDATE (the replaced flag) is not in that list and needs an explicit check.
- `ServicioDeRetroalimentacion` and `RegistroDeRetroalimentacion` take no actor anywhere; the row has `actualizado_en`, a precise timestamp. No aggregate may read it.

Migration route (verified in `database/README.md` and `MigradorAsistente`): the `asistente` schema is not managed by EF history. `MigradorAsistente` applies the numbered `database/asistente/NNN_*.sql` scripts listed in `Recursos` through static wrapper classes, converging with `ADD COLUMN IF NOT EXISTS`, and fingerprints them in `asistente.version_aplicada` (`RecursoVersion`, `007`, stays last). Files `001`-`008` exist, so the new file is `010` (`asistente-modelo-en-caliente` takes `009`). The guard `El_DDL_del_asistente_no_borra_ni_reescribe_nada_de_lo_que_ya_existe` allows only `ALTER TABLE <own table> ADD COLUMN IF NOT EXISTS` and ratified CHECK replacements. New permissions are rows in the Identity chain (`database/identity/<id>_<Nombre>Identity.sql` plus a `.cs`, the pattern of `20261020000900_PermisoAsistenteAdministrarIdentity`; the current last id is `20261020000950`, and the permission UUID must be re-derived because two branches picking the same next-free value collided once), plus a constant and an entry in `Permisos.Todos` in `ArsDocendi.Shared/Auth/Permisos.cs`.

## Goals / Non-Goals

**Goals:** make negative ratings and detectable failures visible and comparable per model; keep TD-012 intact; make the list of problem answers reviewable only by an explicitly authorized, audited role.

**Non-Goals:** see "Out of Scope" in the proposal.

## Decisions

### D1 — Metric definitions

- **Negative rate** (headline) = negative votes / total votes.
- **Negatives by reason**: the four reasons plus "no reason given". A vote can carry several reasons, so the breakdown counts votes per reason and does not sum to the negative total.
- **Turns attended** = analytic rows of the period whose `causa` is not in the policy family (D2). Policy denials (quota, organization cap, maintenance, revoked access, concurrent turn) say nothing about a model and are excluded from every rate's denominator and numerator. They are reported instead as a separate **policy denials** count, broken down by policy cause, so an administrator still sees them without them distorting any model's rates (decided 2026-10-24).
- **Detectable failure rate by cause**: turns attended with a recorded `causa` in the provider or query family, divided by turns attended, grouped by cause. `resultado_vacio` is shown in the cause bars as a signal but is not counted as a failure, because an empty result is often the correct answer.
- **Answers with problems** = (negative OR replaced OR a provider/query-family cause) / turns attended, counted once per turn. All three signals live on `registro_analitico` or `retroalimentacion_turno`, so "once per turn" is one query without any cross-table row join.
- **Vote coverage** = votes / `Respondida` turns.
- All of the above are broken down by `empresa/modelo` and by day (by week when the period is 90 days) and compared with the previous period of equal length. The day of a vote is the turn's `dia`, never `retroalimentacion_turno.actualizado_en`.

**Alternative considered:** a single composite score. Rejected: it hides which component moved and cannot be explained to a reviewer.

### D2 — Record the failure cause explicitly, on the analytic record

`registro_analitico.causa` is a code from the set below, written on each failure or empty path; `estado` is unchanged. A `CHECK` limits the column to the set (a closed vocabulary cannot carry free text, as for `razones`); a test asserts every path records one, and adding a code later needs a ratified CHECK replacement in the architecture guard.

The set is derived from how failures are really translated today (`CarrilSql.ResponderAsync` catch chain, `CarrilDelPlan`, `CapaConversacional`, `FallasDelProveedor`, `ProveedorOllama.Rechazo`, `ProveedorConCompuerta`, `DisponibilidadDelModeloReal`):

| Family   | `causa`                   | Where it is produced                                                                                                                                         | `estado`          |
| -------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------- |
| Provider | `proveedor_no_disponible` | `ProveedorNoDisponible` (breaker open) or the `ProveedorCaido` availability pre-check                                                                        | ServicioDegradado |
| Provider | `proveedor_saturado`      | `ProveedorSaturado`: concurrency gate full, or 429/503 from the shared inference service. Does **not** open the breaker, so it must not be read as an outage | ServicioDegradado |
| Provider | `timeout_proveedor`       | `TimeoutDelProveedor`, or a `TaskCanceledException` not from the request token                                                                               | ServicioDegradado |
| Provider | `credencial_rechazada`    | `HttpRequestException` with status 401/403 (wrong cloud key, shared-service BasicAuth or Access rejection)                                                   | ServicioDegradado |
| Provider | `modelo_no_encontrado`    | `HttpRequestException` with status 404 (model not pulled or retired)                                                                                         | ServicioDegradado |
| Provider | `error_del_proveedor`     | any other `HttpRequestException` (transport, 5xx, 400/422)                                                                                                   | ServicioDegradado |
| Provider | `techo_de_llamadas`       | `TechoDeLlamadasSuperado`                                                                                                                                    | ServicioDegradado |
| Provider | `presupuesto_del_turno`   | the per-turn budget expired (`PresupuestoDelTurno.Vencio` in `CapaConversacional`), distinct from a per-call timeout                                         | ServicioDegradado |
| Provider | `generacion_cortada`      | the generation hit its token ceiling (`SeQuedoSinTokens`)                                                                                                    | NoContestable     |
| Provider | `respuesta_ininteligible` | the generation could not be interpreted (`RazonamientoIninteligible`)                                                                                        | NoContestable     |
| Query    | `validador_rechazo`       | `ValidadorDeSql` rejected the generated SQL                                                                                                                  | NoContestable     |
| Query    | `plan_invalido`           | the compiled plan lane abstained: invalid first sample, compiled SQL rejected, or numerator above denominator (`CarrilDelPlan.Abstencion`)                   | NoContestable     |
| Query    | `sin_privilegio_de_datos` | `ConsultaSinPrivilegio` (engine refused for lack of privilege)                                                                                               | NoContestable     |
| Query    | `error_del_motor`         | `ConsultaRechazadaPorElMotor` other than a statement timeout                                                                                                 | NoContestable     |
| Query    | `timeout_de_sentencia`    | `ConsultaRechazadaPorElMotor` with SQLSTATE `57014`                                                                                                          | NoContestable     |
| Query    | `excepcion`               | an unhandled exception, recorded as `Fallo` by `CapaConversacional`                                                                                          | Fallo             |
| Signal   | `resultado_vacio`         | the query ran and returned no rows (`CarrilSql.Vacio`)                                                                                                       | Respondida        |
| Policy   | `cupo_agotado`            | `MotivoSinModelo.CuotaAgotada`                                                                                                                               | ServicioDegradado |
| Policy   | `tope_organizacional`     | `MotivoSinModelo.TopeOrganizacionalAgotado`                                                                                                                  | ServicioDegradado |
| Policy   | `mantenimiento`           | `MotivoSinModelo.Mantenimiento`                                                                                                                              | ServicioDegradado |
| Policy   | `acceso_revocado`         | `MotivoSinModelo.SinAcceso`                                                                                                                                  | ServicioDegradado |
| Policy   | `turno_concurrente`       | the turn lock was refused (`candadoDelTurno`)                                                                                                                | ServicioDegradado |

A model's own declination (`motivo_rechazo`: out of topic, other system, too general, not covered) is **not** a cause: it is a legitimate abstention and stays only in `registro_operativo.motivo_rechazo`.

Provider-family causes that are attributable to configuration rather than to the model (`credencial_rechazada`, `modelo_no_encontrado`) are exactly the ones an administrator can fix after a switch in `asistente-modelo-en-caliente`, which is why they are separate codes. They depend on `ProveedorChat`, `ProveedorOllama` and `ProveedorLocal` preserving `HttpRequestException.StatusCode`, which `ProveedorOllama.Rechazo` already does.

**Why on the analytic record.** The first version of this design put `causa` on `registro_operativo` while confining the quality reader to the analytic and feedback tables, which cannot compute "failures by cause" or "answers with problems counted once per turn". The analytic table already holds `estado` and `pregunta`; adding `causa` there keeps every quality query inside the two tables that are already linked by id, and does not add a selector shared with the operational table (the operational table does not carry `causa`). The cost is that the operational record is not enriched; operations keep using `estado`, `carril` and `motivo_rechazo`.

**Alternative considered:** `causa` on the operational record and a second reader. Rejected: failures could then be grouped by cause but not joined with votes or replacements per turn, and "once per turn" would be an approximation across two tables.

**Alternative considered:** infer the cause from existing columns. Rejected: the information is not stored, so inference would guess.

### D3 — Model on the analytic record, not a join

The model is recorded in `registro_analitico.modelo` (`empresa/modelo`, the adapter `Nombre`, for example `ollama/qwen3.5:9b` or `anthropic/claude-sonnet-5`). It is always the adapter resolved for the turn, including turns that never called a model, so that a provider outage is attributed to the model that was down. Aggregates then need only `registro_analitico` joined with `retroalimentacion_turno`, which are already linked by id.

**Privacy note, to be confirmed by a human.** `registro_operativo.proveedor` carries the same string, so `modelo` is an attribute shared by both tables, alongside `estado` and `categoria`, and a rarely used model on a given `dia` narrows the set of operational rows that could correspond. With `asistente-modelo-en-caliente` the number of models in use stays small and one model carries most traffic, but a model used for a day of testing is a rare value, which is the tail risk that `002_asistente_registros.sql` records for `intencion_sombra`. The accepted mitigations are that the catalog is closed and small, that the per-model table shows turn counts, and that TD-012 gets a note naming `modelo` as a shared attribute. If the human prefers zero shared attributes, the alternative is to record `modelo` only in the analytic table under a coarser value (company without model) at the cost of comparing models within a company.

**Alternative considered:** join to `registro_operativo.proveedor`. Rejected: that join is exactly the per-row link TD-012 forbids.

### D4 — Replacement is implicit dissatisfaction

`registro_analitico.reemplazado boolean` (default false) is set when `HiloConversacional.QuitarUltimoParaReemplazo` replaces a turn and the replacement completes (`CapaConversacional` revokes the old feedback token and calls `ReemplazarUltimoTurnoAsync`; a replacement that ends in `Fallo` restores the old turn and sets nothing). The flag needs the replaced turn's analytic id. The thread keeps it only as `IdentidadDelUltimoRegistrado.ClaveDeRetroalimentacion`, which is the analytic id **only for `Respondida` turns**; every other state generates an id nobody keeps. So the flag can be set for replaced `Respondida` turns, which are the only turns a vote could attach to anyway, and is not set when the id is unavailable (a restart empties the in-memory thread). The limitation is documented, not guessed. The write is an UPDATE by id performed from the replacement path, in the existing registration step.

An existing vote on a replaced turn is kept (the flag does not delete feedback); the old token is already revoked, so no new vote can arrive.

### D5 — Privacy of the problem list (confirmed by the user)

The list shows the anonymous question text, reason or cause, type, model, day and state, and shows neither the actor nor the free-text comment. It requires a new permission `asistente.revisar_calidad`, seeded to no role by default (not even `sys_admin`, like `asistente.ver_consulta` and `asistente.leer_historial_ajeno`), and every read of the list writes an audit row, following the pattern of `asistente.auditoria_acceso_historial` (written before responding, append-only, own retention). Aggregate KPIs require only `asistente.administrar`. The comment is never returned by any endpoint, which preserves the `asistente-rediseno-v3` prohibition.

The audit needs its own table because `auditoria_acceso_historial` records a reader and a subject. The new table `asistente.auditoria_revision_de_calidad(lector_id, ocurrido_en, filtros, filas)` is created in `010`, denied to both read-only roles like every table in the schema, and purged by `PurgaDeRegistros` with the same independent retention option as the other audit tables.

**Alternatives considered:** show the list under `asistente.administrar` (rejected: question text is more sensitive than counters); include the comment (rejected: re-identification channel noted in TD-012); reuse `auditoria_administracion` (rejected: it is for changes, and a read-heavy list would bury them).

### D6 — Window and retention

The maximum period is 90 days, matching `RetencionDeRegistrosDias`. Requests beyond that are rejected. The trend compares against the preceding period of equal length and is reported as unavailable if that period is not fully inside retention.

### D7 — Known limitation: the voting token is in memory and short-lived

`ValidezDeRetroalimentacionEnMemoria` loses pending tokens on restart, and tokens expire `VigenciaDeRetroalimentacionMinutos` (default 120) after the turn, so a user who answers and returns later, or who answered before a restart, cannot vote. This lowers vote coverage and is why coverage is shown next to the negative rate. It is recorded as tech debt rather than fixed here.

### D8 — Aggregation reader and guards

A new reader (`ConsultasDeCalidad`) queries `retroalimentacion_turno` and `registro_analitico` only, and selects neither `actualizado_en` nor `comentario`. `ArquitecturaAsistenteTests` is extended: the allowlist for readers of `retroalimentacion_turno` includes this file and nothing else, and a rule asserts the file does not reference `registro_operativo`, any actor column, `actualizado_en` or `comentario`. The problem-list reader may read `pregunta` from `registro_analitico` and is a second file under the same rules.

### D9 — Frontend emphasis and Sistema integration

"Respuestas negativas" is the first KPI, wider and red-accented, with the trend against the previous period (an amber note when rising). The per-day chart is a single series with an average reference line (computed only over the days with votes), an accessible label and a table alternative; days without votes are gaps (no bar, null rate, "sin valoraciones" in the tooltip, the table and the accessible summary), never 0%. The tab status dot (`useCalidadEnAlza`) turns to warning when the **30-day** negative rate is above the previous 30 days', regardless of the period and model chosen inside the tab; it shares the panel's default query key. Without `asistente.revisar_calidad`, the problem table is replaced by an information notice. The tab sits after "Modelo" in `ORDEN` (`estado`, `asistente`, `modelo`, `calidad`, `auditoria`); `asistente-modelo-en-caliente` is on this branch and archived, so "Modelo" exists. Showing each model's 30-day negative rate in the Modelo dropdown was optional and is not built (task 4.10). Gating reuses `asistente.administrar`, so `app/shell/nav.ts` needs no change. The page's "Actualizar" button invalidates the tab's own query-key prefix, as it does for the other tabs.

## Risks / Trade-offs

- **Low vote volume** makes rates noisy. Mitigation: show absolute counts alongside rates and suppress trend arrows below a minimum number of votes (configurable constant).
- **Question text in the list** can contain personal data typed by users. Mitigation: the permission, audit, no actor, and the existing retention window.
- **Cause recording touches several failure paths**; a missed path yields a null `causa`. Mitigation: a test per path and an "unclassified" bucket so gaps are visible.
- **Per-model comparison is only fair at similar volume**; the table shows turn counts.
- **Replacement coverage is partial** (D4): only `Respondida` turns with a live thread are flagged.

## Migration Plan

1. Prerequisite: archive `asistente-feedback-export-seguimiento`.
2. Ship the columns, the audit table and the new permission seed (inert until written).
3. Ship recording of `causa`, `modelo` and `reemplazado`.
4. Ship the endpoints, then the tab.
5. Rollback: stop reading; the columns can stay.

## Resolved Questions (2026-10-24)

- **Trend arrow threshold:** the arrow is suppressed when either period has fewer than 20 votes (named constant).
- **`modelo` under TD-012:** accepted as the full `empresa/modelo` string shared with `registro_operativo.proveedor`. Comparing models is a core goal; the attribute has low cardinality and carries no actor or precise time. The acceptance is recorded in TD-012.
- **Policy denials:** excluded from every rate and shown as a separate count by policy cause (D1).

### D10 — Response contract as built

- **Series.** The backend returns one point per day of the period (per week, keyed by the Monday of the week, when the period is 90 days), oldest first. A point without turns has zero counts and a null `tasaNegativa`. Filling it in the backend keeps the contract self-describing and stops a chart from stretching a single day over the whole width. A weekly first point is the Monday of the week of `desde`.
- **Rates are null, not zero,** whenever their denominator is zero; shares inside a breakdown (for example the reasons) are computed by the client over the listed total and show a dash when that total is zero.
- **`modelosDisponibles`** lists every model seen in the period and ignores the `modelo` filter, so the select stays whole while one model is selected.
- **`familia`** on a cause is `proveedor`, `consulta`, `senal` or `politica`; it is null for reasons and for `sin_clasificar`. `resultado_vacio` carries `senal`: shown, not counted as a failure.
- **`sin_clasificar`** collects failing turns (state `ServicioDegradado` or `Fallo`) that recorded no cause, so a path that forgot to record one is visible. A turn with an unrecorded cause and any other state is not a failure.
- **Permissions.** `asistente.revisar_calidad` alone opens the problem list and not the aggregates; `asistente.administrar` alone opens the aggregates and not the list.
- **Purge.** `PurgaDeRegistros` deletes the quality audit table by `RetencionDeAuditoriaDeCalidadDias` and includes its count in the total and in its log line.
