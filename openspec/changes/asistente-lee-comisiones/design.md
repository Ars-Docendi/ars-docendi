## Context

The assistant reads foreign schemas with two read roles and column-by-column `GRANT` (AGENTS.md rule 11). `comisiones` is a SIU-Guaraní snapshot created by PR #58: five tables, owned and migrated by `Modules.Designaciones` (SQL header says so; history lives in `designaciones.__EFMigrationsHistory`). Each import replaces only its own período, so several períodos accumulate.

Facts checked before writing this design:

1. **Real foreign keys.** `sga_comisiones.materia_id → identity.materias`, `sga_comision_carreras.carrera_id → identity.carreras`, `sga_comision_docentes.persona_id → identity.personas`, all nullable with `ON DELETE SET NULL`. The prompt's reference reader reads real FKs, so no logical references are needed (to be confirmed by a test, task 4.4).
2. **Only `designaciones` and `portal` have RLS today.** `identity` has none, so joining `identity.materias` or `identity.carreras` to comisiones does not truncate rows.
3. **`comisiones.ver` is held by all six assistant roles** (`jefe_catedra`, `coordinador_carrera`, `decanato`, `secretaria`, `administrativo`, `sys_admin`), seeded in `database/identity/20261107000000_PermisosComisionesIdentity.sql`. `docente` has no assistant access.
4. **`database/asistente/*.sql` is not an EF migration.** It has no history; `MigradorAsistente` re-applies it as a convergent whole and records a hash in `asistente.version_aplicada` (database/README.md, "Integración de incrementales del Asistente"). The portal grants were added inside `001_asistente_grants.sql`.
5. **The prompt prefix is built by a connection with no actor**, cached per role. `asistente_tiene_permiso` returns false without an actor, so any value list read from a row-protected table comes back empty.
6. **Whole assistant pipeline already abstains per turn** through `PoliticaDeAbstencion.AlcanzaTodo(perfil, tocaPortal)`: `AlcanzaDesignaciones && (!tocaPortal || VeTrayectoriaAjena)`. `asistente-portal-por-ambito` already announced that this must stop being a single boolean.

## Goals / Non-Goals

**Goals:** answer the comisiones questions of the proposal with the assistant's text-to-SQL lane; keep the boundary falsifiable in the engine; never present an empty result as a fact when the cause may be scope or a missing import; keep the prompt prefix inside the local 3070 profile.

**Non-Goals:** aulas as an entity; portal access for jefe de cátedra; links and cards to `/designaciones/comisiones`; the compiled plan; free-aula questions; changing the comisiones DDL; changing the Comisiones screen or its API.

## Decisions

### D1 — Grant the docente name, classified `publica` (team decision, recorded)

`sga_comision_docentes.nombre` is granted and classified `publica`, like `identity.personas.nombre`. **Why:** `persona_id` is nullable (a Guaraní docente not matched to a person). Without the name those docentes disappear from "who teaches comisión 02" and from counts. The Guaraní document is never stored (it is used only in memory to link), so no identifier travels with the name.

**Rejected:** grant only `persona_id` and join to `identity.personas`: loses every unmatched docente.

### D2 — RLS predicate is the permission only, no scope (team decision, recorded)

Every exposed table gets `USING (identity.asistente_tiene_permiso('comisiones.ver'))`. **Why:** the Comisiones screen shows every comisión to anyone with `comisiones.ver`; the assistant must not know more or less than the screen. All six assistant roles hold the permission.

**Consequence accepted:** a jefe de cátedra or coordinator sees comisiones outside their materia or carrera through the assistant, exactly as they do on the screen. This is a difference from `designaciones` (permission AND scope) and from the in-flight `asistente-portal-por-ambito`. It is written here so nobody "fixes" it by importing `asistente_alcanza_ambito`.

**Each policy names the actor itself**, even the child tables, for the same reason as portal D7: a policy that relies on another policy cannot be read alone. Unlike portal there is no outer-row reference, so the qualified-column trap does not arise.

### D3 — `sga_importaciones` stays denied (team decision, recorded)

It is an operational import log that names the importing user (`usuario_id`) and answers no catalog question. It keeps its table entry as `denegada-explicita` with the reason rewritten. The `comisiones` schema entry changes from `denegado` to `expuesto`, and its reason text (which says no question needs it) is replaced.

### D4 — RLS and comments are Designaciones migrations; grants extend `001`

- **RLS and `COMMENT ON`** go in the Designaciones chain, scaffolded with `node scripts/migrations/scaffold.mjs Designaciones <Name>`. **Why:** that module owns the schema and its history; the precedent is placement by owning chain (`designaciones/20261103001100` and `…1200`, `portal/20261020001500` and `…1600`). They must sort after `20261107000100_ComisionesGuaraniDesignaciones`. Two files, RLS first, comments second.
- **`ENABLE`, not `FORCE`.** The owner is exempt, and the application connects as the owner: the Comisiones screen and the importer must keep seeing and writing every row. The policies are `FOR SELECT` and written for the assistant's roles only. Same reasoning as portal and designaciones.
- **Grants are added to `database/asistente/001_asistente_grants.sql`, not to a new numbered file.** This deviates from the preference "never edit a published migration", and the reason is evidence, not convenience. The preference protects EF migrations, whose published SQL is frozen by history. The asistente files have no history and are re-applied as a convergent whole under a hash; `001` is where the portal grants were added. A new `011` file would need new plumbing (a resource constant, an entry in the `Recursos` list, a call in `MigrarAsync`, a copy of the role-name prologue) for no extra protection, and the manifest tests compare the effective database against the manifest, not file against file. **Revisit if** the team prefers a new file: the change is mechanical.
- **Order.** Grants run last: the asistente module applies after Designaciones, so the RLS exists before the `GRANT`. Granting first would open the snapshot in the window between two merges, which is the reason portal documents the same order.

### D5 — Columns exposed and columns denied

| Table                   | Granted                                                                                                                                                 | Denied, with reason                                                                                                                                                                                                                                                      |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `sga_comisiones`        | `comision_guarani_id`, `anio_academico`, `periodo_lectivo_nombre`, `nombre`, `materia_codigo`, `materia_nombre`, `materia_id`, `modalidad`, `ubicacion` | `importacion_id` (FK into the denied import log; joining it fails), `periodo_lectivo_guarani_id` (opaque Guaraní key; the período is identified by name and year), `modalidad_codigo` (Guaraní code; `modalidad` is the readable value), `created_at` (import mechanics) |
| `sga_comision_carreras` | all four columns                                                                                                                                        | none                                                                                                                                                                                                                                                                     |
| `sga_comision_docentes` | all five columns                                                                                                                                        | none                                                                                                                                                                                                                                                                     |
| `sga_comision_horarios` | `comision_guarani_id`, `dia`, `desde`, `hasta`, `aula`                                                                                                  | `asignacion_guarani_id` (Guaraní key; no question needs it)                                                                                                                                                                                                              |

Four tables of five, derived from the questions, as in portal D4: a table nobody queries is prefix paid on every call. Every granted column is `publica` (D1 for the name); the protection is the grant and the RLS, not the mask.

### D6 — Abstention is evaluated by domain

Today `AlcanzaTodo` takes a boolean for portal. This change replaces it with the set of domains the query touched.

- **Detection** generalizes `CoberturaDelPortal.TablasQueToca` into one detector that maps the tables of a generated query to domains: `comisiones.*`, `portal.*`, `designaciones.*`. The token-based detection of `OrigenesDeLosDatos` is preferred over the substring match used for portal, because an alias or a comment can fool a substring.
- **Rule.** The query reaches everything iff every touched domain is reached: designaciones needs `AlcanzaDesignaciones` (global scope and `designaciones.ver`); portal keeps its rule; comisiones needs only `comisiones.ver`, a new `PerfilDelActor.VeComisiones` read live like `VeTrayectoriaAjena`. A query that touches no domain (identity catalogs only) keeps today's behavior, so no existing answer changes.
- **A comisiones-only query does not require designaciones scope.** A jefe de cátedra asking about comisiones of any materia must not be told "outside your scope" about data they can see.
- **A cross-check does keep the designaciones limit, and carries it even when rows come back.** "Teaches a comisión without a designación" is an anti-join. For a non-global actor the designaciones side is truncated by scope, so the join returns false positives with no empty result to warn anyone. The existing flag already reaches the narration of non-empty answers; the scenario in the spec makes it a requirement and a test.
- **The compiled plan** (`CarrilDelPlan`) only needs its call site adapted: it touches designaciones and, for one measure, portal.
- **Builds on `asistente-portal-por-ambito`.** That change is already in `develop` (its code merged with #40) and was archived on 2026-11-06, together with `asistente-fundaciones` and `asistente-lee-portal`. Its requirement «El alcance se evalúa por dominio consultado» is in `openspec/specs/asistente-alcance-por-actor/`, and this change modifies it to add the comisiones domain; the code extends the per-domain evaluation that portal already introduced.

### D7 — No closed-value list for comisiones

`LectorDeValoresDeCatalogo.CatalogosCerrados` reads values with the prompt connection, which has no actor (fact 5), so lists for `responsabilidad`, `modalidad` or `dia` would be empty. `responsabilidad` is also heterogeneous (the fixture shows "Titular" and "Ingenieria", the second not being a responsabilidad), so the cap of 40 and its stability are not guaranteed. **Choice:** the facts go in the `COMMENT ON` text: `dia` 1 to 7 as weekdays, `responsabilidad` as free text to match with `ILIKE`, `aula` as free text. **Rejected:** a bypass so the prefix reader sees rows (it would put row values of a protected table in a prompt shared by every actor).

### D8 — "Who teaches X" keeps defaulting to designaciones

Existing items (`cap-020` "¿Quién dicta Análisis Matemático?", `dialogo` items) are answered through `designaciones`. The comments distinguish a docente of a comisión (Guaraní, per período) from a designación (administrative appointment), and the few-shot examples use comisiones only when the question names comisión, horario, aula, día, turno, cuatrimestre or período. **Why:** it preserves the frozen baselines and the meaning of the existing answers. **Risk:** the model may pick comisiones for "quién dicta"; the measurement (task 7.5) shows exactly which items moved.

### D9 — The empty comisiones result declares which períodos are imported

A docente with no comisiones in a período that was never imported gives the same zero rows as a docente with none. As portal D6 does for self-declared data, a small reader returns the imported períodos and the text states them next to the scope notice. It uses the actor's connection, so it is subject to the same RLS; it runs only when comisiones was touched and the result is empty, and a failure drops the sentence, not the turn. Confirmed by the team on 2026-11-06.

### D10 — Turn boundaries and "this período"

"Night", "Saturday" and "this período" are not in the schema. `dia` and `desde` answer Saturday and night, but the boundaries of morning, afternoon and night are an institutional convention, and there is no flag for the current período (several are accumulated). **Choice:** the comments state the convention once, and for "this período" the model filters to the most recent `anio_academico`; when that year holds more than one período, the answer is grouped by `periodo_lectivo_nombre` and names each one instead of picking or mixing them (decided by the team on 2026-11-06). **Turn boundaries, decided by the team on 2026-11-06:** morning before 12:00, afternoon 12:00 to 17:59, night from 18:00, all by `desde`. Which `dia` value is Saturday is a data fact, not a decision: it is read from the Guaraní client mapping before the comment is written (task 1.4).

### D11 — Catalog, labels and examples

- `EtiquetasDeAreas.Mapa` gets the four tables under one label ("comisiones y horarios"), which also enables the `comisiones` schema in `SchemasHabilitados` and in the "origen" texts.
- `PresentacionPorRol` and the catalog examples announce comisiones by the live `comisiones.ver`, not by the grant: the catalog derives from effective grants, which both roles hold, and the `EXPLAIN` check of examples does not read rows. Without this gate an actor without the permission would be offered a question that returns zero rows.
- Three or four few-shot examples in `ejemplos-sql.json`, disjoint from the evaluator items, each executing and returning rows against the fixture. The glossary is not touched: it is off by default, and its spec forbids vocabulary of modules the assistant cannot read, not of comisiones; the turn terms would need values, which the schema does not have (D10).

### D12 — Measurement

The fixture seeds comisiones with declared cardinalities: two períodos, a docente without `persona_id`, a Saturday and a night schedule, an aula in free text, and `comisiones.ver` for the evaluation actors (without it the items pass without reading the snapshot, the first defect of the portal measurement). New capacidad and robustez items include non-answerable ones: free aulas, and anything that needs an aula catalog. `cap-019` ("¿En qué aula se dicta Sistemas Operativos?") becomes answerable and is reclassified in the same change; `cap-039` ("¿Cuántas clases dio cada docente el cuatrimestre pasado?") stays non-answerable (a comisión is not a class given) and is checked, not assumed. Baselines are regenerated explicitly because the prefix changes; the gate refuses to compare otherwise.

**Prefix budget.** The stable prefix is about 12k tokens; the local single-slot profile has 16,384 of context and was observed at about 10k cached tokens; the compose default is 2 x 13,312. Four tables with comments and examples are estimated at +1.5k to +2.5k tokens **[estimate]**. Acceptance: the longest evaluator request plus the prefix plus the output ceiling fits the 16,384 profile. **If it does not fit:** first compact the comments (drop synonyms and repeated caveats), then reduce the examples; if the default 2 x 13,312 profile still does not fit, document in `docs/architecture/modelo-local.md` that the default profile no longer fits, instead of silently truncating.

## Risks / Trade-offs

- **Scope widening for jefe de cátedra and coordinators** (D2) → accepted as parity with the screen; recorded in `BR-asistente-010` and in the policy comment.
- **Anti-join false positives for non-global actors** (D6) → the scope notice is required on non-empty cross-domain answers and has its own test.
- **`responsabilidad` is dirty** (values such as "Ingenieria" in the fixture) → comment says free text; no closed list; answers by "titular" use `ILIKE`. Cleaning belongs to the importer, not here.
- **Prompt growth** (D12) → measured before promotion, with a written fallback.
- **Multiple períodos make "this período" ambiguous** (D10) → the latest year is answered grouped by período, so it never picks the wrong cuatrimestre; the cost is a longer answer when a year has two. A current-período flag in the snapshot would remove it and is outside this change.
- **Baseline churn** → expected and explicit; the diff lists which items moved, as the portal change did.
- **The abstention code already has a portal rule** (D6) → this change extends it; the existing portal abstention tests must keep passing.

## Migration Plan

1. RLS migration in the Designaciones chain (after `20261107000100`), then the comments migration.
2. `001_asistente_grants.sql`, `manifiesto-privilegios.json` and `manifiesto-sensibilidad.json` in the same merge, grants last.
3. Application code, examples, fixture, items.

Rollback: revert the grants and manifests first (the assistant cannot read the schema afterwards), then the two migrations. The migrations are additive (`ENABLE ROW LEVEL SECURITY`, policies, comments); dropping the policies and disabling RLS restores the previous state without touching data. Verification commands are in tasks.md and follow the AGENTS.md table.

## Open Questions

- Does the Departamento want `periodo_lectivo_guarani_id` exposed later for ordering periods? Not needed for the questions listed; deferring does not change the specs.
- Should `ubicacion` (sede) be answerable by name? It is granted; examples for it are optional.
