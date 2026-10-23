## Why

Every question the SQL lane cannot answer gets the same fixed sentence,
`PoliticaDeAbstencion.TextoNoContestable` («No puedo responder eso con la información que
tengo disponible.»), whether the user asked about the weather, about grades that live in
Guaraní, typed a single vague word, or asked for data the schema does not cover. The text
does not say why, does not say what the assistant _can_ do, and repeats word for word when
the user keeps trying. ARS-139 (decided scope) replaces it with per-reason refusals built
from templates — never from model text — without adding a model call. AGENTS.md rule 5
requires this change before any code.

## What Changes

- **Bounded refusal reason, no extra call.** The generation JSON (`GeneradorDeSql`)
  gains `motivo`, from a closed set: `fuera_de_tema` · `otro_sistema` · `muy_general` ·
  `no_cubierto`, and an optional `termino` (the user's words naming what could not be
  answered). A missing or unknown `motivo` resolves to `no_cubierto`.
- **One template per reason, with slots.** The refusal body is rendered by a template in
  code. Slots: the user's term — shown only if it is a bounded, verbatim
  (case/accent-insensitive) span of what the user typed, and printed with the user's own
  characters, never the model's — and the areas the actor can actually consult, taken
  from the capability catalog (`CapacidadesDelActor.Cubre`, derived from GRANTs).
  `otro_sistema` names only the fixed list already in `LimitesDelAsistente` (Guaraní,
  planillas). No template asserts that something does not exist: no query ran.
- **Escalation.** Three variants per reason. The variant is chosen from how many refusals
  the same conversation already had, counted on the server-side thread (seeded from
  `turno_historico.estado` when a conversation is resumed), never from the client. Later
  variants add the consultable areas and a pointer to «¿qué podés hacer?». Two consecutive
  refusals never show the same text.
- **No suggestion chips.** Reaffirmed from `asistente-rediseno-v3` (ARS-149): a refusal is
  carried by its text alone; this change adds no field to the response.
- **Privacy cases stay generic.** Engine permission rejections, engine errors, empty
  results, validator rejections, unresolved follow-up references and exhausted
  clarifications keep their current texts (D15: do not reveal data existence).
- **Metric.** `asistente.registro_operativo` gains a nullable `motivo_rechazo` column
  (refusal rate by reason). `registro_analitico` does **not** get it (TD-012); a guard
  test fails if it ever does.
- **Evaluation.** `capacidad.json` gains items per reason (including «python») and the
  no-contestable items declare acceptable reasons; the capability report adds an
  informational reason-agreement section. The primary score and the regression gate do
  not change their rules.
- **Prompt and cassettes.** Asking the model for `motivo`/`termino` changes the fixed
  generation instructions, which moves the prefix fingerprint of every generation
  cassette. That part ships only together with a funded re-recording run and regenerated
  baselines (see design.md D9); everything else ships without touching the prompt.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `asistente-abstencion`: model-declared refusals are rendered from per-reason templates
  with validated slots and conversation-level escalation; generic privacy texts are kept;
  no suggestions.
- `asistente-generacion-sql`: the generation object carries a closed `motivo` and an
  optional `termino`; out-of-set values resolve to `no_cubierto`.
- `asistente-registros`: the operational record stores the refusal reason; the analytic
  record does not.
- `asistente-eje-capacidad`: items per reason and an informational reason-agreement
  report that does not alter scoring.

The four modified capabilities exist only in un-archived changes today
(`asistente-carril-sql`, `asistente-presupuesto-degradacion`, `asistente-rediseno-v3`,
`asistente-evaluacion`); this change archives after them.

## Impact

- **Backend (`Modules.Asistente` only).** `GeneradorDeSql` (parse `motivo`/`termino`),
  `InstruccionesDeGeneracion` (work unit B only), `CarrilSql` (template rendering on the
  model-declared refusal branch), new `PlantillasDeRechazo`, `TerminoDelRechazo`,
  `EtiquetasDeAreas`, `MotivoDeRechazo` under `Application/Abstencion/`,
  `HiloConversacional`/`TurnoDelHilo` (turn outcome), `CapaConversacional` (prior-refusal
  count, registro), `HistorialController.Reanudar` (seed outcome), `ResultadoDelTurno`,
  `TurnoParaRegistrar`, `RegistroDelTurno`. No `*.Contracts` change; dependency graph
  unchanged; no cross-module reference.
- **Database.** `database/asistente/002_asistente_registros.sql`: one
  `ADD COLUMN IF NOT EXISTS motivo_rechazo` with a named CHECK, nullable, no default. No
  GRANT change; privilege and sensitivity manifests unchanged.
- **API.** No contract change: `RespuestaDelAsistente` is mapped field by field and the
  reason is not exposed. `docs/architecture/api-contracts.md` is untouched.
- **Frontend.** No change: refusal text is rendered as plain text today, and the existing
  "no suggestions in any state" tests already cover item 4.
- **Evaluation.** `backend/eval/datasets/capacidad.json`, `DatasetDeCapacidad`,
  `RunnerDeCapacidad`, `Reporte`; the dataset hash changes, so baselines are regenerated
  in the funded run (never automatically).
- **Docs.** `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md`,
  `docs/business-rules/asistente.md` (new BR-`asistente`-009),
  `docs/quality/tech-debt.md` (TD-012 note), `backend/eval/README.md`,
  `backend/src/Modules.Asistente/README.md`.
- **Rollback.** The column is additive and nullable; rolling back code leaves a
  compatible schema. Work unit B is reverted as one commit (prompt + cassettes +
  baselines) so the corpus never disagrees with the prompt.
