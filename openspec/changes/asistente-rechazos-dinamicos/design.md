## Context

See proposal.md for motivation. What the code does today, and what constrains the
approach:

- **One refusal branch reaches the fixed text.** `CarrilSql.ResolverAsync`
  (`CarrilSql.cs:195-206`) returns `NoContestable(generacion, aMostrar,
PoliticaDeAbstencion.TextoNoContestable, generacion.Categoria)` whenever
  `GeneracionDeSql.EsContestable` is false or `Sql` is null. That covers three origins:
  the model said `es_contestable: false`; the response could not be parsed
  (`RazonamientoIninteligible`); the generation was cut by the token ceiling
  (`CategoriaTruncada`). Every other refusal already has its own text:
  `TextoRechazadaPorValidador` (case 5), `TextoSinAccesoALosDatos` (engine 42501),
  `TextoErrorAlConsultar` (other engine rejections), `TextoDeResultadoVacio` (empty),
  `TextoReferenciaSinResolver` (applied later by `CierreDelTurno.TextoDelRechazo`), and
  the exhausted-clarification text in `CapaConversacional.ResolverAclaracion`.
- **No suggestions exist any more.** `asistente-rediseno-v3` D12 (ARS-149) deleted
  `ISugerenciasDeSeguimiento`, `ResultadoDelTurno.Sugerencias` and
  `RespuestaDelAsistente.Sugerencias`; `Mensaje.test.tsx` asserts no suggestion block in
  any state. This change must not reintroduce any of it.
- **The capability catalog is GRANT-derived.** `ICatalogoDeCapacidades.ObtenerAsync`
  returns `Cubre` (qualified table name, table comment, column count) from
  `has_column_privilege` under the actor's read role, cached per role, plus
  per-actor fields (scope, presentation, maintenance, quota). The meta-question and the
  «?» help popover are rendered from it. Table names are internal labels (RNF-18) and
  table comments are written for the model, not for people.
- **The conversation thread is server-side and actor-owned.** `HiloConversacional`
  lives in `AlmacenDeHilosEnMemoria`, keyed by id, checked against the session actor
  (`HiloAjeno`), expiring after `VigenciaDelHiloMinutos`. The SQL lane always calls
  `conversacion.Agregar(...)`, refusals included. `TurnoDelHilo` does not record the
  outcome today. `HistorialController.Reanudar` seeds a new thread from
  `turno_historico`, which **does** persist `estado` (never the answer text).
- **The generation prompt is the cassette seal.** `InstruccionesDeGeneracion.Instrucciones`
  is part of the stable prefix; `PrefijoDeLosCassettesTests` fails if any versioned
  cassette declares a prefix fingerprint that is no longer current, and
  `ClaveDeCassette` keys each cassette on prefix + message + effort + model. Changing a
  word of the instructions makes the generation half of the 109-cassette corpus
  irreproducible (it happened in `ce1ede8`; TD-021 needed a funded run to recover).
- **Two records that must not be joinable.** `registro_operativo` (actor, precise time)
  and `registro_analitico` (question text, day) share only `estado` and the day; the
  precedent for a new operational-only column is `intencion_sombra`, with a test that
  fails if the analytic table gains it (TD-012).
- **The evaluator calls `CarrilSql.ResponderAsync` directly** (`RunnerDeCapacidad`), with
  no conversation thread, and `capacidad.json` feeds a sealed per-item regression gate.
- **The fixture has a Python skill** (`GeneradorDeFixture` — «Python», 3 declarants), and
  `PoliticaDeAbstencion.AlcanzaTodo`'s own history records the bug where «¿qué docentes
  saben Python?» was answered «no encontré ningún registro». This drives D6.

## Goals / Non-Goals

**Goals:**

- The refusal body stays a code-owned string; the only user-visible text that can come
  through the model is a span the user typed, re-copied from the user's own message.
- Escalation state is server-owned and survives resuming a conversation.
- Shipping the deterministic part does not depend on a funded model run.

**Non-Goals:**

- No change to the response contract, the frontend, or the history view (a resumed
  no-contestable turn keeps showing «Esta pregunta no se pudo responder.»).
- No surfacing of refusal rates in the administration usage panel; the metric is a
  column plus a documented query.
- No change to the deterministic lane: it runs in shadow mode and produces no user-facing
  text. When ARS-46 connects it, any refusal it emits must go through the same templates.
- No new reasons beyond the four, no per-role or per-actor wording.

## Decisions

### D1. Render in `CarrilSql`, on the model-declared refusal branch only

`CarrilSql.ResponderAsync` gains an optional `int rechazosPrevios = 0` (same pattern as
`consultasAnteriores`) and `ICatalogoDeCapacidades` as a dependency. On the
`!generacion.EsContestable || generacion.Sql is null` branch it renders
`PlantillasDeRechazo.Texto(motivo ?? NoCubierto, termino, areas, rechazosPrevios)`.
`CapaConversacional` passes `conversacion.RechazosPrevios()`; the evaluator passes
nothing (first variant). `CierreDelTurno.TextoDelRechazo` keeps its precedence: an
unresolved follow-up reference still replaces the text, because that diagnosis is
deterministic and more specific than the model's reason.

Alternatives: render in `CapaConversacional` after the lane returns — rejected, the
evaluator and `CarrilSqlTests` would never see the real text, and the term validation
needs the raw message the lane already has; keep a constant and decorate in the
controller — rejected, the edge is where D15 says texts must not be decided.

The areas are fetched only on this branch. `ObtenerAsync` also reads quota and
maintenance (three small queries); paying that only on refusals is acceptable and
avoids a second catalog entry point. A failure is caught (not
`OperationCanceledException`), logged, and the template renders without areas — same
rule as `CoberturaAsync`: context that improves the answer must not break it.

### D2. `MotivoDeRechazo` and tolerant parsing

New `internal enum MotivoDeRechazo { FueraDeTema, OtroSistema, MuyGeneral, NoCubierto }`
with a single wire map (`fuera_de_tema`…). `GeneracionDeSql` gains
`MotivoDeRechazo? Motivo` and `string? TerminoCandidato`. `RespuestaDeGeneracion` reads
`motivo` and `termino` as `JsonElement?` so a non-string value is ignored instead of
throwing `JsonException` (which would make the whole object unparseable and lose the
decision). Rules:

| Origin                                            | `Motivo`         | Text template          |
| ------------------------------------------------- | ---------------- | ---------------------- |
| `es_contestable: false`, `motivo` in the set      | that value       | that reason            |
| `es_contestable: false`, `motivo` missing/unknown | `NoCubierto`     | `no_cubierto`          |
| Answerable object                                 | `null` (ignored) | —                      |
| Contradiction (contestable, empty `sql`)          | `null`           | `no_cubierto`          |
| Unparseable / truncated by the token ceiling      | `null`           | `no_cubierto`, no term |

`null` means "the model did not decide": it keeps the operational metric (D7) clean of
format failures and truncations, which the analytic `categoria` already distinguishes.

### D3. The term slot: the model proposes a span, code re-copies it from the user

The model returns `termino`; `TerminoDelRechazo.Validar(termino, mensaje)` returns the
span **of `mensaje`** or `null`. Conditions, all required:

1. Matched against `mensaje` — what the user typed this turn — never against the
   rewritten question, which is itself model output (`ReescritorDePreguntas`). A term
   that exists only after rewriting (anaphora) is dropped.
2. Case- and accent-insensitive, using a per-character fold (lower-invariant + strip
   combining marks) so indices in the folded string map 1:1 to the original; the
   displayed text is `mensaje[inicio..fin]`, the user's own characters.
3. Starts and ends on word boundaries of `mensaje` (no «pyth» out of «python»).
4. 2–40 characters after trim, at most 4 words, no line break, no «, », `"` or control
   characters.
5. Not made only of demonstratives/stopwords (reuse `NormalizadorLexico` and the
   demonstratives list of `PoliticaDeAbstencion`), so «eso» never becomes «No puedo
   responder sobre «eso»».

Alternatives: deterministic extraction from the question (no model) — rejected, choosing
the salient phrase of a sentence is exactly what stopword heuristics get wrong, and a
wrong phrase is worse than none; trusting the model's string — rejected, it would put
model text in a refusal body, which the ticket forbids. The term is never logged and
never persisted (the rendered text is not persisted either: `turno_historico` stores no
answer text).

### D4. Areas: GRANT-derived coverage, human labels from a versioned map

`EtiquetasDeAreas` maps each qualified table to one label, in declared order; the
rendered list is the distinct labels of the actor's `Cubre`, in that order, at most
five, followed by «, entre otros datos» when more exist.

| Label                       | Tables                                                                                                    |
| --------------------------- | --------------------------------------------------------------------------------------------------------- |
| designaciones               | `designaciones.designaciones`                                                                             |
| pedidos de designación      | `designaciones.pedidos`, `designaciones.pedido_historial`, `designaciones.pedido_adjuntos`                |
| materias                    | `identity.materias`                                                                                       |
| carreras                    | `identity.carreras`                                                                                       |
| cargos                      | `designaciones.cargos`, `designaciones.dedicaciones`                                                      |
| períodos                    | `designaciones.periodos`                                                                                  |
| docentes                    | `identity.personas`                                                                                       |
| perfiles del portal docente | `portal.perfiles`, `educaciones`, `certificaciones`, `experiencias`, `habilidades`, `docente_habilidades` |
| usuarios con sus roles      | `identity.users`, `roles`, `user_roles`, `permisos`, `rol_permisos`                                       |

A test reads `database/asistente/manifiesto-privilegios.json` and fails if any
`concedida` table has no label, so a new GRANT forces a wording decision instead of
silently disappearing from refusals. A table present in `Cubre` but unknown to the map
(defensive) is skipped, never printed by name.

Naming consultable areas does not widen disclosure (D15): it is the same information the
meta-question and the «?» popover already give that actor, derived from the same
privileges, and it says nothing about what exists outside them.

Alternatives: a label in each table `COMMENT` — rejected, those comments belong to other
modules' migrations and are part of the generation prefix; the first sentence of the
comment — rejected, written for the model and too long; grouping by schema — rejected,
it prints `identity`, an internal label.

### D5. Escalation from the server-side thread

`TurnoDelHilo` gains `EstadoDelTurno? Estado = null`; `HiloConversacional.Agregar`
takes it; `RechazosPrevios()` counts turns with `Estado == NoContestable` across the
**whole** conversation (not the segment: a pivot does not reset frustration).
`HistorialController.Reanudar` seeds `Estado: t.Estado` from `turno_historico`. The
variant is `k == 0 ? 1 : 2 + (k - 1) % 2` (1, 2, 3, 2, 3…). Adjacent refused turns always
differ because the earlier one is in the thread (`k` grows by exactly one) and the
formula never repeats on consecutive `k`; different reasons never collide because every
fixed part is distinct (pinned by a test over all 4 × 3 × {with, without term} ×
{with, without areas} renderings).

Why not the client: the request carries only the thread id; any count or history in the
body would be forgeable and would let a client pin the first variant. Why not query
`turno_historico` on every turn: the live thread already holds the state, and the write to
history is fire-and-forget, so a read-after-write could miss the previous turn.

Known, accepted edges: a replaced last question is removed from the thread before
counting (D9 of `asistente-rediseno-v3`), so its replacement reuses its variant — it
replaces that text on screen, it does not follow it. An expired in-memory thread comes
back empty and restarts at variant 1. A resumed thread contains every persisted turn,
while a live thread only records SQL-lane turns, so an exhausted clarification counts
after resuming and not before; it never produces two identical adjacent texts.

### D6. Wording never asserts absence (deviation from the ticket's example)

The ticket's example «No encontré nada sobre «python» en los datos del sistema…» is
reworded to «No puedo responder sobre «python» con los datos que consulto: trabajo con
…». No query ran, and in this fixture — as in production — three teachers declared
Python as a skill: «no encontré nada» would be exactly the false negative
`PoliticaDeAbstencion.AlcanzaTodo` documents. A test scans every rendered variant for
«no hay», «no existe», «no encontré», «ningún», «nadie». The same test set extends
`Ningun_texto_de_abstencion_habla_de_esquema_ni_de_sql` to the templates.

### D7. Metric column in `registro_operativo` only

`002_asistente_registros.sql` gains, in both places the file's rule requires:

```sql
ALTER TABLE asistente.registro_operativo
    ADD COLUMN IF NOT EXISTS motivo_rechazo text
    CONSTRAINT registro_operativo_motivo_rechazo_valido
    CHECK (motivo_rechazo IN ('fuera_de_tema', 'otro_sistema', 'muy_general', 'no_cubierto'));
```

plus the column in the `CREATE TABLE`, a `COMMENT ON COLUMN`, `ColumnasQueEscribe`,
`TurnoParaRegistrar.MotivoDeRechazo` (from `ResultadoDelTurno.MotivoDeRechazo`), and the
startup column check in `MigradorAsistente` (which reads `ColumnasQueEscribe`). Nullable
without default: null is the normal value for every turn that is not a model-declared
refusal (same reasoning as `intencion_sombra`). The architecture guard already allows one
`ADD COLUMN IF NOT EXISTS` per statement on an own table; a named inline CHECK is part of
that statement, not a separate `ADD CONSTRAINT`.

TD-012 analysis. The analytic table does not get the column (guard test, like
`intencion_sombra`), so no new shared column exists. The residual is inferential: someone
with database access could read an analytic question, guess its reason and match it to
same-day operational rows, refining the existing `estado` bucket by at most four. That
person already has `turno_historico`, which attributes the question text to its actor
directly, so the column reveals nothing new to them. Rate by reason is computable from the
operational table alone:
`SELECT motivo_rechazo, count(*) FROM asistente.registro_operativo WHERE estado = 'no_contestable' GROUP BY 1`.

### D8. No response-contract or frontend change

`RespuestaDelAsistente.De` maps fields explicitly; the reason is not added. No consumer
needs it (no icons, no chips — invariant #7), and exposing it would be a contract to
maintain for nothing. The debug metrics line (`MetricasDto`) is unchanged.

### D9. Two work units, because the prompt is the cassette seal

- **Unit A — deterministic, no prompt change.** Enum, tolerant parsing, templates, term
  validation, labels, thread outcome and escalation, `Reanudar` seeding, column and
  migration, docs and BR, tests. Old cassettes lack `motivo`, so replay yields
  `no_cubierto` without term: the parse tests stay green and the prefix guard is
  untouched. Until unit B lands every model refusal records `no_cubierto`; the docs say
  the per-reason metric is meaningful from unit B's deploy date.
- **Unit B — prompt + corpus, one commit.** `InstruccionesDeGeneracion` describes
  `motivo` and `termino`; the generation corpus is re-recorded with
  `Asistente__RegrabarCassettes` in a funded run; the new eval items (D10) are added in
  the same run, so the dataset hash and baselines are regenerated once. The prompt text
  goes in the stable prefix, not the per-turn message: the key covers prefix and message,
  so the message would invalidate every key just the same while paying uncached tokens
  on every turn.

Unit B is sequenced after `asistente-proveedor-local` settles its cassette changes
(`ClaveDeCassette`, `CassettesConProveedorLocalTests`) so the corpus is re-recorded once.

### D10. Evaluation: acceptable reasons, reported apart

`ItemDeCapacidad` gains `IReadOnlyList<string>? MotivosAceptables` (`motivos_aceptables`
in JSON), validated at load (non-empty, in the set, only on `no_contestable`). Existing
items: cap-017 sueldo → `otro_sistema`, `no_cubierto`; cap-018 calificaciones →
`otro_sistema`; cap-019 aula → `no_cubierto`; cap-030/031/032 → `no_cubierto`. New
items: «python» (`muy_general`), a clearly off-topic question (`fuera_de_tema`), a
Guaraní enrolment question (`otro_sistema`), a contentless request (`muy_general`), and
an in-domain question on data the schema lacks (`no_cubierto`); each checked against the
catalog-disjointness tests. `ResultadoDelTurno` gains `MotivoDeRechazo?` so
`RunnerDeCapacidad` can read it; `Reporte` adds «Motivo del rechazo (informativo)». The
outcome enum, the three penalties and `GateDeRegresion` are untouched.

### D11. Initial template copy

`{t}` is the validated term; `{areas}` the D4 list; `AYUDA` = «Si querés ver todo lo que
puedo consultar, preguntame «¿qué podés hacer?».»; `L` = the second line of
`LimitesDelAsistente` rewritten in the first person («Solo leo este sistema: no Guaraní,
ni planillas, ni otras fuentes.»; the help list says «Solo lee…», which would mix voices
inside a refusal). Brackets mark the with-term form.

| Reason          | Variant 1                                                                                                                       | Variant 2                                                                                                                        | Variant 3                                                  |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| `fuera_de_tema` | No puedo responder [sobre «{t}»\|eso] con los datos que consulto: trabajo con {areas}.                                          | [«{t}»\|Esa pregunta] también queda fuera de lo que consulto. Solo trabajo con {areas}; si es sobre eso, reformulala y la busco. | Sigo sin poder ayudarte con eso[ («{t}»)]. AYUDA           |
| `otro_sistema`  | [«{t}»\|Eso] parece estar en otro sistema. L                                                                                    | Eso[ («{t}»)] también parece venir de otro sistema. L Lo que sí consulto: {areas}.                                               | Esa información no la leo desde acá. AYUDA                 |
| `muy_general`   | La pregunta[ sobre «{t}»] es muy amplia para una consulta. Contame qué dato puntual buscás y de qué carrera, materia o período. | Necesito una pregunta más concreta[ sobre «{t}»]. Puedo consultar {areas}: decime qué dato de eso te sirve.                      | Todavía me falta precisión para armar la consulta. AYUDA   |
| `no_cubierto`   | No puedo responder [sobre «{t}»\|eso] con la información que tengo disponible.                                                  | Tampoco puedo responder eso[ sobre «{t}»] con lo que consulto. Lo que sí tengo: {areas}.                                         | Eso[ («{t}»)] sigue fuera de lo que puedo consultar. AYUDA |

Without areas (catalog failure), the clause carrying `{areas}` is replaced by `AYUDA`.
`no_cubierto` variant 1 without term is `PoliticaDeAbstencion.TextoNoContestable`
verbatim, so existing tests and the evaluator keep their expectation. **This copy is
confirmed by the PO (2026-09-26) and is the exact, final wording** — `PlantillasDeRechazoTests`
pins it literally (one assertion renders all twelve reason × variant cells with a term
and with areas and compares them against this table verbatim) and additionally pins the
properties this table implies (distinctness per reason × variant for a fixed input, term
echo exactly in the bracketed cells and nowhere else, no absence claim, no schema words,
help pointer routed to the meta lane) — it does not pin a different sentence than the one
above.

### D12. Files

`Application/Abstencion/MotivoDeRechazo.cs`, `PlantillasDeRechazo.cs`,
`TerminoDelRechazo.cs`, `EtiquetasDeAreas.cs`, each well under the ~300-line cap
(`PoliticaDeAbstencion.cs` is already 417 lines and does not grow). Logging: one
`LogInformation` with the reason on a model-declared refusal; never the term, never the
question.

## Risks / Trade-offs

- [Unit B has no funded run available (TD-008)] → Unit A ships alone; every refusal is
  `no_cubierto` with escalation and areas, which already fixes "same text forever". The
  prompt change never lands without its corpus.
- [The model picks a poor reason] → Wording is written so every reason is still honest
  when wrong (no absence claims, no capability claims); the eval section measures
  agreement; the score cannot be distorted by it.
- [The model returns a term the user did not type] → Dropped by D3; the no-term variant
  renders. Worst case is a less specific text, never model text.
- [A new GRANT adds a table without a label] → The manifest-coverage test fails in CI.
- [Catalog read adds latency to refusals] → Cached per role; the per-actor part is three
  small queries on a turn that skipped execution and redaction entirely.
- [Long area lists] → Capped at five labels plus «entre otros datos».

## Migration Plan

1. Unit A: code, migration (additive, idempotent), docs, BR, tests. Deploy order does not
   matter: the column is nullable and the startup check requires it only once the code
   that writes it is deployed with its migration (same module, same release).
2. Unit B: in the funded run, set `Asistente__DirectorioDeCassettes` and
   `Asistente__RegrabarCassettes`, commit prompt + cassettes + dataset + regenerated
   baselines together; `PrefijoDeLosCassettesTests` and `HigieneDeCassettesTests` green.
3. Rollback: revert unit B as one commit (prompt and corpus stay consistent); reverting
   unit A leaves a nullable column nobody writes.

## Pending user confirmation

These change externally observable behavior or acceptance, and the ticket does not settle
them; the design above takes the stated default until answered:

1. **Funded re-recording for unit B.** Is a real-provider run (key and budget, TD-008)
   available for this change? Without it only unit A can merge.
   **Answered 2026-09-26**: not for now — implement unit A only; unit B waits for an
   explicit go-ahead on the paid run.
2. **«python» expectation.** Default: `muy_general` («La pregunta sobre «python» es muy amplia para una
   consulta…»), because the fixture and production have Python as a declared skill, so a
   bare term is ambiguous rather than off-topic. Alternative: accept an answer listing
   the teachers who declared it, which would move the item out of `no_contestable`.
   **Confirmed 2026-09-26**: `muy_general`, the default.
3. ~~**Wording deviation from the ticket example** (D6) and the initial copy (D11): PO
   review of the Spanish sentences.~~ **Confirmed 2026-09-26** by the user, with one
   fix: `muy_general` variant 1 says «La pregunta[ sobre «{t}»] es muy amplia…» so the
   adjective agrees with «pregunta» and never with the user's term.

## Open Questions

- Whether the administration usage panel should later show refusal rate by reason; the
  column supports it without schema change.
