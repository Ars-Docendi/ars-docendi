## Why

The hardest wrong answer to detect is a valid query with a miscopied literal. The model writes `WHERE m.name = 'Análisis Matematico'` (or «Ing. Informática») from the user's question, PostgreSQL returns zero rows, and zero rows from valid SQL is indistinguishable from "there is none". The prompt already carries the values of the closed catalogs; `identity.materias`, `identity.carreras` and people's names are left out on purpose because they are not closed catalogs (ARS-165).

This is a quality option for any provider, but it matters most for a small local model (Qwen3-8B), which copies literals less carefully than Claude.

## What Changes

**One new option, `RecuperacionDeValores`, off by default.** With the default, the prompt Claude receives does not change by one byte, the user message is byte-identical, and the recorded cassettes stay valid (the cassette key hashes the prefix, the concatenated message contents, the effort and the model).

- Before generating, look in the question for spans that name a real **materia**, **carrera** or **docente**, and put one line per match in the **user message**, after the question: `En la pregunta, "análisis matemático" corresponde a la materia "Análisis Matemático I".` The model is told to use that exact value in the filter.
- **Unique or nothing.** A span that matches more than one entity (two materias, a materia and a carrera, two homonymous docentes) produces no line. The retrieval never chooses; the existing ambiguity detector and `necesita_aclaracion` path are untouched.
- **Scoped as the actor.** The lookup runs with the read role, the actor preamble and the same predicates as the mentions search (`identity.asistente_materias_visibles()`, RLS of `designaciones.designaciones`). Nothing the actor cannot see reaches the prompt. No new GRANT.
- **Deterministic and local.** Matching is accent- and case-insensitive containment plus a conservative edit-distance for materia and carrera names, computed in process over the actor's visible entities. No model call, no embeddings, no PostgreSQL extension.
- **Lives in the SQL lane**, not in the conversational layer, so production turns, the dialogue axis **and** the evaluator's capacidad and robustez runs (which call the lane directly) all go through it.
- **Interactions.** Explicit mentions win (an entity already bound to a `$refN` gets no line). A turn with lines does not read or write the generated-query cache. Repair and empty-result retries keep the same lines.
- **Evaluation gate.** The option is measured against the local baseline item by item, with only this option toggled, and is promoted to `.env.example` and the local compose profile only if no axis worsens.

## Capabilities

### New Capabilities

- `asistente-recuperacion-de-valores`: which real entity names found in a question reach the generation prompt, under which scope and ambiguity rules, and what stays out.

### Modified Capabilities

None. With the default the behavior of every existing capability is unchanged.

## Impact

- `backend/src/Modules.Asistente/`: options and validator, a pure matcher, a scoped reader (new port, `internal` implementation), the SQL lane (call site, cache condition, threading to retries), `GeneradorDeSql.ArmarMensaje` (new block), DI registration.
- **Tests**: new pure and integration tests; `OpcionesDocumentadasTests` forces the README row; `ArquitecturaAsistenteTests` public-surface count goes from 94 to 95 (the port is a constructor parameter of the public `CarrilSql`).
- **Docs**: `backend/src/Modules.Asistente/README.md` (options table, «Las menciones» neighbour section), `docs/architecture/modelo-local.md`, `.env.example` (RTX 3070 block, only if promoted), `infra/compose/compose.asistente-local.yml` (only if promoted). `docs/architecture/api-contracts.md` does not change: no HTTP surface changes.
- **Database / manifests**: no migration, no GRANT, `database/asistente/manifiesto-privilegios.json` untouched. Every column read is already granted and already used by the mentions search.
- **Rollback**: set `Asistente__RecuperacionDeValores=false` and restart. Nothing is persisted.
