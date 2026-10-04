## Context

See `proposal.md` — Why. What the code looks like today, verified:

- **The entity index is not scoped and does not cover what the ticket needs.** `IndiceDeEntidades` reads `identity.materias` (with its carrera as discriminator) and `identity.personas` (indexed by **surname only**) through `AperturaDeLectura.AbrirAsync` with **no actor preamble**, caches the result for the process, and `DetectorDeAmbiguedad` / `ResolutorDeIntenciones` match it by whole-term containment in the normalized question (`" termino "` inside `" pregunta "`). Carreras are not indexed, an entity name longer than the span the user typed («Análisis Matemático» vs the term `analisis matematico i`) never matches, and there is no tolerance for typos. `identity.materias`, `identity.carreras` and `identity.personas` have no RLS of their own (`database/asistente/001_asistente_grants.sql` grants columns; `BuscadorDeMenciones` says so for materias). **The ticket's phrase "use the entity index that already exists" cannot be taken literally**: that index would put out-of-scope entities in the prompt.
- **The mentions search is the scoped precedent.** `BuscadorDeMenciones` opens with `AperturaDeLectura`, begins a transaction, calls `PreambuloDelActor.AplicarAsync` (the only place the actor setting is written; `ArquitecturaAsistenteTests` enforces that every read transaction goes through it), and scopes materias with `identity.asistente_materias_visibles()` and docentes by joining `designaciones.designaciones`, whose RLS conjoins `designaciones.ver` with the actor's scope (`009_designaciones_rls_asistente.sql`). All columns it touches are `publica` in `manifiesto-sensibilidad.json` (`materias.name`, `carreras.name`, `personas.nombre/apellido`) and already granted.
- **The «Menciones» block is the message precedent.** `GeneradorDeSql.ArmarMensaje` appends it after `Pregunta del usuario:` and only when non-empty, so a turn without mentions is byte-identical to the pre-feature message.
- **What the cassette key hashes.** `ClaveDeCassette.Calcular` hashes four things with SHA-256: the `system` text (the prefix), the concatenation of the text of **all `messages`** (the user message), the effort and the model. So the message is covered: with the option off the message must be byte-identical, and the retrieval must never touch the prefix (which `PrefijoDeLosCassettesTests` pins).
- **Where the evaluator enters.** `RunnerDeCapacidad.EvaluarAsync` calls `carrilPorItem().ResponderAsync(actor, item.Pregunta, null, ct, mencionesNuevas: menciones)` directly on `CarrilSql`. `RunnerDeRobustez` reuses that same method (`capacidad.EvaluarAsync`). Only `RunnerDeDialogo` goes through `CapaConversacional`, which itself ends in `carril.ResponderAsync` (line ~712 of `CapaConversacional.cs`). The evaluator builds the lane with `PorTurno<CarrilSql>` in `backend/eval/ArsDocendi.Evaluacion/Program.cs`. So **`CarrilSql.ResolverAsync` is the only point all three axes and production share.** The detector runs earlier, in `CapaConversacional`, so the evaluator's capacidad/robustez runs never see `necesita_aclaracion`.
- **Fuzzy matching in the database.** Only `unaccent` exists (`CREATE EXTENSION IF NOT EXISTS unaccent` in `database/asistente/001_asistente_grants.sql`, also in `database/audit/002_audit_busqueda.sql`). A search for `pg_trgm`, `similarity`, `fuzzystrmatch` and `levenshtein` in `database/`, `infra/`, `backend/src` finds nothing. The image is `postgres:18-alpine`; `pg_trgm` is a contrib module that is trusted since PostgreSQL 13, so the owner could install it, but that is a schema change with its own cost (see D3).
- **Fixture facts that bound what the evaluator can show** (`backend/eval/datasets`). Of 34 capacidad items about seven name an entity (`cap-004`, `-015`, `-019`, `-020`, `-021`, `-028`, plus the two mention items `-033` and `-034` that already bind by marker); of 15 robustez items, `rob-003` (carrera without accents), `rob-005` («Bases de Dattos», the only entity typo) and `rob-011` name one. «Análisis Matemático» exists in three carreras and «Gómez» three times, so `cap-020` and `cap-021` must produce no line. The two other `tipeo` items misspell schema words («carogs», «cilcos»), not entities, so **the retrieval cannot move them**.

## Goals / Non-Goals

**Goals**

- A literal copied exactly: the model gets the stored spelling of every entity the question names unambiguously and visibly.
- Zero change with the option off; zero entity outside the actor's scope in the prompt.
- Zero model calls; one bounded database read per turn.
- A matcher that is pure and deterministic, so the evaluator stays deterministic.

**Non-Goals**

- Embeddings and semantic search (ticket).
- Resolving ambiguity: that stays with `DetectorDeAmbiguedad` and `necesita_aclaracion`.
- Matching free text, codes (`materias.code`), cargos or closed-catalog values (the prompt already carries them).
- Changing the HTTP API, the prefix, the schema or any GRANT.
- Pruning or reusing the process-wide `IndiceDeEntidades` for this purpose.

## Decisions

### D1 — One boolean, `RecuperacionDeValores`, default `false`

It sits with its siblings in `OpcionesAsistente` (documented in the README table; `OpcionesDocumentadasTests` forces it). Every optimization of this family is an opt-in so that Claude's prompt and the 109 cassettes stay untouched and comparing is toggling one variable.

_Alternatives:_ a numeric knob for the cap or the similarity threshold. Rejected: two more values to tune without evidence; they are constants with a named reason, and become options only if the evaluator says they must.

### D2 — It lives in the SQL lane (`CarrilSql.ResolverAsync`), not in the conversational layer

The lane gets a new optional collaborator, a port `IRecuperadorDeValores` (`ListarAsync(actor, pregunta, ct)`), called once per turn after the inherited mentions are filtered and before the cache lookup. The result is threaded to `GenerarAsync` and to the empty-result retry and the repair, exactly like `menciones`.

_Alternatives:_

- `CapaConversacional`, next to the detector: rejected. The capacidad and robustez runners never enter it, so the two axes the ticket expects to move would never exercise the option, and the gate would pass vacuously.
- `GeneradorDeSql.GenerarAsync`: rejected. It has no actor, and a retry would repeat the read.
- Do it in the controller like mentions: rejected. Mentions are the user's explicit choice, revalidated there; this is inference about the question and belongs next to generation.

The port is public because `CarrilSql` is public and takes it as a constructor parameter (the compiler forces it, same rule as `CacheDeConsultasGeneradas`); the matched values travel as a value tuple so nothing else becomes public. `ArquitecturaAsistenteTests.SuperficiePublicaDeApplication` goes from 94 to 95, with a remark naming this change. The implementation is `internal` in `Infrastructure`.

### D3 — Matching: scoped read, then in-process matching

**Recommended:** per turn, one scoped read of the entities the actor can see, then a pure function matches the question against them.

- Materias: `identity.materias` joined to carreras, `is_active`, `id IN (SELECT identity.asistente_materias_visibles())`.
- Carreras: those with at least one visible materia. There is no scope function for carreras; deriving it from visible materias is the conservative reading (an actor who sees no materia of a carrera does not get it named).
- Docentes: `identity.personas` joined to the most recent row of `designaciones.designaciones` (RLS decides, as in `SqlDocentes`), prefiltered in SQL to people whose folded surname shares a word with the folded question, so the read is bounded by the question and not by the padron.

The same helpers as the mentions search are used: `AperturaDeLectura.AbrirAsync`, a read-only transaction, `PreambuloDelActor.AplicarAsync`, `public.unaccent` qualified. The query text is a second constant next to the mentions ones (same predicates, no free-text term), so the architecture guards for connection opening and preamble apply unchanged. **No new GRANT**: every column read (`materias.id/name/carrera_id/is_active`, `carreras.id/name/is_active`, `personas.id/nombre/apellido`, the designation columns of `SqlDocentes`) is already in `manifiesto-privilegios.json` for both roles and already read by `BuscadorDeMenciones`. `database/asistente/manifiesto-privilegios.json` and its tests stay untouched. If implementation finds it needs one more column, that is a stop-and-ask, not a quiet GRANT.

The matcher (`ReconocedorDeValores`, pure, `internal`):

1. Tokenize the question into words with their offsets (letters and digits, like `NormalizadorLexico`'s tokenizer), fold each word with `NormalizadorLexico.SinAcentos` and lower case. Offsets are kept so the line can quote the span **as typed** (the ticket's form has accents); the normalizer alone loses positions.
2. Candidate spans are runs of 1..8 consecutive words that do not start or end with a word of `NormalizadorLexico.PalabrasVacias` (inner ones are allowed: «bases de datos»). A single word needs at least 4 letters and must not be a domain generic word the normalizer already treats as a synonym key («materia», «carrera», «docente»…).
3. A span matches a **materia or carrera** if, word by word, it equals the entity name folded (exact), or equals the name minus trailing numerals (arabic or roman: «Análisis Matemático» ↔ «Análisis Matemático I»), or is within the tolerance below.
4. Tolerance: Damerau–Levenshtein per word, 0 edits under 5 letters, 1 edit for 5 to 10, 2 for longer, the first letter must agree, and at most 2 edits per span. A single-word approximate match needs at least 6 letters.
5. A span matches a **docente** only exactly, and only as the whole given name and whole surname adjacent, in either order (D5).
6. Per span, collect the distinct entity ids that match. More than one (same kind or different kinds) means **ambiguous**: no line, and the span still consumes its words so nested shorter spans cannot emit. Spans are processed longest first, then leftmost; a span overlapping a consumed word is dropped.
7. One entity reached twice yields one line. At most 5 lines, in order of position.

_Alternatives considered:_

- **A. Normalized exact/containment only** (what the detector does). Deterministic and trivial, catches «Análisis Matematico», does not catch «Bases de Dattos» (`rob-005`, the one entity typo in the fixture). Kept as the first two rules of the recommended matcher, so it is the fallback if D3-fuzzy turns out harmful in the gate.
- **B. `pg_trgm` similarity in PostgreSQL.** Pushes ranking into the engine and scales to a large catalog, but: it needs `CREATE EXTENSION pg_trgm` in an assistant migration (a schema change that `ArquitecturaAsistenteTests` treats with suspicion), `EXECUTE` on the extension's functions for the two read roles under an empty `search_path` (a manifest-level decision per AGENTS.md rule 11), and a similarity threshold that is hard to reason about per word. The catalog is a few hundred names, so the engine's advantage buys nothing. Rejected for now; documented as the path if the catalog grows.
- **C. In-process edit distance over a scoped list** (recommended). No schema change, no GRANT, deterministic, unit-testable without a database, and scope comes from the same predicates as mentions.
- **D. Reuse `IndiceDeEntidades`.** Rejected: unscoped, surname-only for people, no carreras, and a process-wide cache that would outlive an actor's revoked scope (the SQL says a revocation must stop adding scope on the next query).

### D4 — The block in the user message, after the question

`ArmarMensaje` gets one more optional parameter and appends, after the «Menciones» block and before «Intento anterior», only when there is at least one line:

```
Valores reconocidos en la pregunta. Cada línea dice a qué valor EXACTO de la base corresponde lo que el usuario escribió; usá ese valor tal cual en el filtro, en lugar del texto de la pregunta:

- En la pregunta, "análisis matemático" corresponde a la materia "Análisis Matemático I".
- En la pregunta, "ingenieria industrial" corresponde a la carrera "Ingeniería Industrial".
- En la pregunta, "marta suarez" corresponde al docente "Marta Suárez".
```

Reasons:

- It is a fact about **this** question, so it goes in the user message, never in the cached prefix (same reasoning as «Menciones» and the verified-examples decision).
- It follows the mentions block: mentions are the user's explicit choice and stay the last certain thing; these are inferences.
- The instruction does not say "compare with `=`": the prompt's own rule 8 already says to compare names with `public.unaccent` on both sides, and a hint that contradicts it would make the model pick between two rules. "Exact value" fixes the spelling; how it is compared stays rule 8's business.
- The span is quoted from the question; the canonical value comes from the database. A canonical value that contains a double quote or a control character is not rendered (the entity is skipped and counted in the log), so catalog data cannot reshape the prompt.
- Retries and repairs call the same `ArmarMensaje` with the same lines, so the second attempt sees what the first saw.

With the option off, or with no lines, the method returns the same bytes as today (pinned by a test that builds the message with and without the parameter).

_Alternatives:_ a line per entity with the id as a marker (rejected: ids never go to the model, D11 of `asistente-rediseno-v3`); sending the whole catalog of names instead of matches (rejected: it is what the ticket says is left out on purpose, ~hundreds of names every turn); putting the hints in the prefix (rejected: invalidates prefix cache and cassettes).

### D5 — People: conservative rule

A docente line requires the whole given name **and** whole surname, adjacent, in either order, exact after folding. Consequences:

- The canonical text equals what the user typed up to case and accents, so the line never discloses a name the user did not already send in the question (a stored second given name that the user omitted prevents the match instead of being revealed).
- A surname alone («¿Qué nombramiento tiene Gómez?») gives no line. The model already has the surname and rule 8 handles accents; the hint would add a canonical string and a risk of steering to the wrong homonym, for nothing.
- Never fuzzy for people: «Pérez»/«Perez»/«Peres» can be three people.

_Sensitivity statement._ `personas.nombre/apellido`, `materias.name`, `carreras.name` are `publica`, which means "travels to the model as is". The output-boundary masker covers the **redaction** call, not the generation call, and the manifest says so itself (`asimetria`: the question travels). So a person's name already travels when the user types it; with the full-name rule the line adds only its canonical spelling. Materia and carrera names are public catalog data the actor can see.

_Alternatives recorded:_ (a) surname-only with exactly one visible docente: more recall, more risk and nothing the model lacks; (b) restricting people matching to actors with global scope: not needed, because the designations RLS already bounds what a scoped actor can match, and it would make behavior depend on role in a way the evaluator's actors would then have to cover; (c) no people at all: the ticket lists personas, and the full-name rule is safe, so it stays.

### D6 — Scope: the database decides

All reads run as the actor (D3). **A test proves an out-of-scope entity never reaches the prompt**, for a materia outside `asistente_materias_visibles()` and for a docente outside the designations RLS, against the migrated database (same fixture style as `MencionesTests` and `RlsAlcanceTests`). Uniqueness is computed **over the visible set only**: an out-of-scope homonym neither appears nor suppresses a line, so the presence or absence of a line carries no information about entities the actor cannot see. (Cost, accepted: an actor who sees one «Análisis Matemático» gets a line even though another exists elsewhere. The model's own filter on `materias.name` behaves as it does today without the option.)

The ambiguity detector, which works on the unscoped index in `CapaConversacional`, is not touched and keeps its own behavior.

### D7 — Interactions

- **Mentions.** The lane already holds the ids bound by new and inherited mentions (`todasLasReferencias`). Lines whose entity id is in that set are dropped. Matching by id, not by label, because inherited references carry no label.
- **Generated-query cache** (`VigenciaDeCacheDeConsultasMinutos`). The key is role variant, day and question; it does not include the actor. A generation made with a line embeds a literal derived from one actor's scope, and a cache hit would serve it to an actor who could not see that entity, and the SQL can surface in the interpretation or the history. Decision: a turn with lines is **not** `sinContexto`, so it neither reads nor writes the cache. A turn with no lines behaves exactly as today. The loss is that a no-line entry may serve an actor who would have had a line, which is today's behavior, never a scope leak. Tested both ways.
- **`ReescrituraEnLaGeneracion` and follow-ups.** The match runs on the string the model will see as «Pregunta del usuario»: the rewritten question when the separate rewriter ran, the raw message otherwise. Entities carried from earlier turns travel inside the earlier queries, not through this retrieval.
- **Detector and clarification.** In production the detector runs first; a clarified question carries «Me refiero a …», which now has a full name or a carrera that this retrieval can match uniquely. In the evaluator there is no detector, and the fixture collisions resolve to no line by D3 rule 6.
- **Retries and repair.** Same lines, same message builder.

### D8 — Cost and failure

Zero model calls, so the per-turn call ceiling (4) is unchanged. One database round trip per turn with the option on, over a catalog of a few hundred rows plus the prefiltered docentes; the budget is **p95 under 30 ms**, measured in the gate run, and the read has its own 1 s timeout (the command timeout already bounds it). Any failure other than the caller's cancellation (database error, timeout) is swallowed: log a warning with no entity value, no lines, the turn goes on as if the option were off. The retrieval is an aid; it never turns a working turn into an error.

_Alternative:_ cache the scoped lists per actor. Rejected: revocations must take effect on the next query, and the saved millisecond is not worth a staleness bug.

### D9 — Evaluation gate and promotion

Run `backend/eval` against the **local** baseline (Qwen3-8B Q4_K_M, optimized profile: 26/34 capacidad, 12/15 robustez, 10/11 diálogo, 18/20 social; deterministic, three identical runs changed zero items), same model and profile, **only this option toggled**, never `--congelar`:

1. Control run with the option off: must equal the baseline item by item. If not, stop: something else moved.
2. Run with the option on. Compare **item by item**, not by total. Reports go to `eval-local/ARS-165/<config>/` (untracked).
3. **Promote** (`.env.example` RTX 3070 block, `compose.asistente-local.yml`) only if no axis worsens in any item. If any item worsens, the option stays documented, off, and the report says which item and why.

Expectation, stated so the result is read honestly: at most `cap-004/015/019/028` and `rob-003/005/011` can change. The capacidad and robustez gain the ticket wants may therefore be zero or one item on this fixture, and a gate that shows "no regression, no change" is not evidence of value. That is an open question for the product owner (see Open Questions). The glossary change (ARS-164) is being measured on another branch against the same baseline; this change does not include it and is not compared against it.

## Risks / Trade-offs

- **A wrong hint is worse than no hint.** A unique but wrong fuzzy match steers the model to the wrong entity with authority. Mitigations: unique-or-nothing, conservative thresholds (first letter, short words exact, one edit), exact-only for people, and the gate compared item by item. If the gate shows a wrong steer, fuzzy is switched off (D3 alternative A) before the option is promoted.
- **Names of people in the prompt.** Bounded by D5 (full name only, exact, visible, adds only canonical spelling) and by the manifest's own `asimetria`.
- **Out-of-scope leakage through uniqueness.** Avoided by computing uniqueness over the visible set only (D6). Residual: none beyond today's detector, which is global.
- **Evaluator collisions** («Análisis Matemático» ×3, «Gómez» ×3) must resolve to no line; they are fixture-pinned tests.
- **Stale or cached lines.** Turns with lines bypass the cache (D7).
- **Latency.** One read per turn; measured in the gate (D8).
- **A catalog value that attacks the prompt.** Quote and control characters skipped (D4).
- **The database in the middle of a failure.** Degrades to no lines (D8).

## Migration Plan

1. Ship with the default `false`: no behavior change in any environment (`PrefijoDeLosCassettesTests` and the cassette-based tests green without re-recording).
2. Run the gate (D9). Promote only on a clean comparison.
3. To enable: set `Asistente__RecuperacionDeValores=true`, restart.
4. Rollback: set it to `false`, restart. Nothing is persisted, no schema or grant changed.

## Open Questions

- **For the product owner.** The fixture has about seven items where this can act and two of them are ambiguous by construction, so the gate may show no movement. Is "no regression, option documented and off" an acceptable end state, or should the dataset grow (which invalidates the frozen baselines and is a separate change)?
- **For the product owner.** Surname-only docente hints are excluded (D5). Is the added recall for `¿Qué nombramiento tiene Suárez?` worth the risk if the evaluator later shows it helps?
