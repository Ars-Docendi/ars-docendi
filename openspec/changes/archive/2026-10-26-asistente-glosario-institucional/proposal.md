## Why

The SQL-generation prompt carries the schema and, for a handful of closed catalogs, the values that exist. It does not carry the Department's vocabulary. A small local model does not know that "en Decanato" is a request state, that "aprobado" is stored as `en_lote` (no `aprobado` state exists), that "adjunto" is a teaching rank and not a request attachment, or that "carga horaria" is not a column. It guesses, and a guessed literal returns zero rows with valid SQL, which looks like "there is no data".

Linear ARS-164 asks for a versioned file of institutional terms, rendered in the fixed (cacheable) part of the prompt behind an option that is off by default, like the other local-model optimizations (`asistente-optimizaciones-modelo-local`).

## What Changes

**One new option, off by default.** With the default, the prompt is byte-for-byte today's for every provider, the recorded cassettes stay valid, and `PrefijoDeLosCassettesTests` stays green.

- **Glossary option** (`GlosarioEnElPrefijo`, default `false`): when on, the fixed part of the generation prompt gains a deterministic block «GLOSARIO INSTITUCIONAL», after the schema and the closed-catalog values and before the examples block (when `EjemplosEnElPrefijo` is also on). The block is identical for every turn and for both read roles.
- **Glossary file** (`Recursos/glosario.json`, embedded like `ejemplos-sql.json`): 24 terms. Each has the term, optional synonyms, a one-line explanation in Spanish, and a machine-checkable list of references (`schema.table.column`, optionally the exact stored values). A term that is not a plain conjunction of equalities, the sum of the three hour columns, carries a short SQL hint that the tests also check.
- **Dedications as a closed catalog, under the same option.** `designaciones.dedicaciones.nombre` and `.codigo` join the columns whose values the prompt reads live from the database. This changes the prefix, so it is gated by `GlosarioEnElPrefijo` and not added to the always-on list. The guard test that lists which tables may be enumerated is extended deliberately.
- **A test the ticket demands.** Against the migrated test database: every column the file names exists, is granted to the basic read role and is not `sensible-valor`/`sensible-texto`; every literal it names exists, in the catalog table, in the column's `CHECK`, or (for `propietario_actual`, which has no `CHECK`) in `identity.roles.code`. A value that cannot be verified mechanically does not go in the file.
- **Measured, then promoted or not.** The local evaluator compares item by item against the Qwen3-8B baseline. Only if capacidad and robustez improve and diálogo and social do not worsen, the option is added to the RTX 3070 profile; otherwise it stays off and the result is documented.
- **Known schema inconsistency, recorded and not fixed:** the legacy `CHECK` and two column comments say "Categoría 0 a 6", the catalog has 1 to 6.

**What does not enter the glossary** (decided by the product owner on 2026-10-03):

- terms with no column or value yet: «interino», «suplente», «carácter», «exclusiva / semiexclusiva / simple», «concurso», «licencia», and the rest of the "no column" list, not even as "this does not exist, abstain" entries;
- Tareas and Aulas vocabulary, until those modules are merged into `develop` and granted in the privilege manifest;
- the dedication values as glossary terms (they are the closed catalog above);
- named metrics and views (ARS-169).

No **BREAKING** changes.

## Capabilities

### New Capabilities

- `asistente-glosario-institucional`: when and how the Department's vocabulary enters the fixed part of the generation prompt, what the glossary may and may not reference, and how that is verified against the database.

### Modified Capabilities

None. The related capabilities (`asistente-optimizaciones-modelo-local`, `asistente-carril-sql`) have no main spec under `openspec/specs/` yet, so this change defines its own.

## Impact

- **Module**: `Modules.Asistente` only. No `Contracts` change, no new project reference, the dependency graph is untouched.
- **Code**: `OpcionesAsistente`; a loader and a renderer for the glossary; `RenderizadorDeEsquema` and `ProveedorDeEsquema` (append the block, read the dedication values); `LectorDeValoresDeCatalogo` (a second declared list, gated); `Modules.Asistente.csproj` (embedded resource).
- **Schema / API / frontend**: none. No migration, no endpoint, no UI.
- **Tests**: new `GlosarioInstitucionalTests` (against the database) and pure tests for loader and renderer; changes to `PrefijoDeEsquemaPuroTests` (admitted tables) and `OpcionesDocumentadasTests` (boolean option rows). `PrefijoDeLosCassettesTests` and `PrefijoDeEsquemaTests` must pass unchanged.
- **Docs**: module `README.md` (options table, «Optimizaciones para un modelo propio», a «Cómo se agrega un término al glosario» note), `docs/architecture/modelo-local.md`, `docs/architecture/domains/asistente.md` where it describes the fixed part of the prompt, `docs/quality/tech-debt.md` for the "Categoría 0" inconsistency.
- **Profiles**: `.env.example` and `infra/compose/compose.asistente-local.yml`, only if the evaluator gate passes.
- **Business rules**: none. The glossary maps vocabulary onto existing columns; it adds no institutional rule.
- **Rollback**: set `Asistente__GlosarioEnElPrefijo=false` (or remove it) and restart. Nothing is persisted.

## Deferred (not part of this change)

The paid re-recording of the corpus with Claude, and therefore the Claude-side evaluation of the ticket's acceptance criteria, is deferred ("más adelante") and stays coupled to ARS-161: both change the fixed part of the prompt, so there is **one** paid re-recording for both. This change neither prepares nor runs it. Because the option is off by default, the default prefix does not change and the cassettes stay valid, so nothing forces the re-recording now.
