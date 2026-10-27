## Context

See `proposal.md` for the motivation. What shapes the approach, read from the code:

- **How the fixed part is built.** `ProveedorDeEsquema.ConstruirAsync` reads columns, references and the closed-catalog values, and calls `RenderizadorDeEsquema.Renderizar(columnas, referencias, vocabularios, compacto)`. The output is, in order: `InstruccionesDeGeneracion.Instrucciones`, the «VALORES POSIBLES» block (the closed-catalog values), the schema header, the tables (or the compact form). It is cached per role by `ValorPerezosoPorRol` and hashed into `EsquemaParaPrompt.Huella`.
- **Where the examples enter.** `GeneradorDeSql.GenerarAsync` appends `BloqueDeEjemplos()` to `prefijo.Prefijo` when `EjemplosEnElPrefijo` is on. The examples are therefore **outside** `Huella`, but inside `SolicitudAlModelo.PrefijoEstable`, which is what `ClaveDeCassette` hashes for the cassette seal.
- **Two different "prefix hashes".** The evaluator's seal (`Program.cs`, `new(esquema.Huella, huellaDelDataset, fixture.Huella())`) uses `EsquemaParaPrompt.Huella`. The cassette key uses the hash of `PrefijoEstable`. `PrefijoDeLosCassettesTests` checks that every committed cassette declares one of five current hashes (two schema prefixes, the redactor and rewriter instructions, the preflight prefix).
- **Closed catalogs.** `LectorDeValoresDeCatalogo.CatalogosCerrados` is a declared list of four columns (`identity.carreras.code/name`, `designaciones.cargos.codigo/nombre`). `PrefijoDeEsquemaPuroTests.Solo_se_enumeran_tablas_de_catalogo_sin_datos_personales` is the guard: it admits only `identity.carreras` and `designaciones.cargos`, written by hand and not derived from the list, because the vocabulary travels whole to the model provider. `PrefijoDeEsquemaTests.Ninguna_columna_declarada_pasa_el_tope_de_valores` asserts that every declared column yields a vocabulary, and `EjemplosEjecutablesTests` reuses the list to check example literals.
- **Embedded catalogs.** `Recursos/ejemplos-sql.json` is an embedded resource (`Modules.Asistente.Recursos.ejemplos-sql.json`) read by `SelectorDeEjemplos` with `GetManifestResourceStream`, with `version` and `descripcion` keys.
- **What the basic role can read.** `database/asistente/manifiesto-privilegios.json` lists, per table and per role (`asistente_ro`, `asistente_ro_pii`), the granted columns. `designaciones.dedicaciones` is granted whole to both; `identity.roles` and `identity.user_roles` expose `code` and `deleted_at`; `portal.*` is partly granted; `tareas` and `aulas` are not in the manifest at all. `manifiesto-sensibilidad.json` classifies every granted column: in `designaciones` the only non-public ones are `pedidos.justificacion`, `pedidos.tipo_baja_detalle` and `pedido_historial.comentario` (all `sensible-texto`).
- **Where the stored literals live.** `pedidos.estado`, `pedidos.novedad`, `pedido_historial.accion` have a `CHECK` (`003_designaciones_pedidos.sql`, `005_designaciones_pedido_historial.sql`). `pedidos.propietario_actual` has none: its values are `identity.roles.code` (`RolesCircuito` in the code). `cargos.codigo` and `dedicaciones.codigo/nombre` are catalog rows seeded by migration.
- **The 3070 budget.** The llama-server slot is 13,312 tokens (16,384 in the one-slot fallback) and the longest measured request is about 9.8k with `EsquemaCompacto` and `EjemplosEnElPrefijo` on (`docs/architecture/modelo-local.md` §8).
- **Evaluation.** The local baseline is Qwen3-8B Q4_K_M with the optimized profile (`eval-local/ARS-162/qwen3-8b-q4-B/`): 26/34 capacidad, 12/15 robustez, 10/11 diálogo, 18/20 social. The run is deterministic: three identical runs changed zero items. `eval-local/correr.sh <config> [Opcion=valor…]` writes the reports to `eval-local/<config>/` and `eval-local/comparar.py <A> <B>` compares two runs item by item.

## Goals / Non-Goals

**Goals:**

- Give the model the Department's vocabulary for the data it can already read, with a file whose every column and value is checked against the database.
- Zero change with the defaults: the prompt, the cassettes and the evaluator baselines stay as they are.
- A result the product owner can decide on: a controlled, item-by-item comparison against the local baseline.

**Non-Goals:**

- Named metrics and views (ARS-169).
- Terms with no column or value yet, and Tareas and Aulas vocabulary, until the data exists and is granted.
- Validating the vocabulary with the Department (ARS-65 is open) and deriving it from the evaluation datasets. The first is a risk below; the second is deliberate, see D9.
- Fixing the "Categoría 0" inconsistency in the schema.
- **The paid re-recording of the corpus with Claude, and the Claude-side evaluation.** Deferred, coupled to ARS-161 (see "Deferred" below).
- Making the glossary per-question (selecting terms by similarity). It is one fixed block so the prefix cache keeps working; selecting would reproduce the problem measured with the examples (the selector moves what the model sees, `modelo-local.md` §8).

## Decisions

### D1 — One option, `GlosarioEnElPrefijo`, default `false`, naming parallel to `EjemplosEnElPrefijo`

A boolean in `OpcionesAsistente`, documented in the README options table. With the default, `RenderizadorDeEsquema` receives no block and `LectorDeValoresDeCatalogo` reads the same four columns as today, so the prefix is byte-for-byte today's and `PrefijoDeLosCassettesTests` and `PrefijoDeEsquemaTests` stay green without being touched.

It is not coupled to `Proveedor`: the glossary is as valid for Claude as for a local model, and it is the cassette directory (not the option) that makes a prefix change visible for Claude — a call without a matching cassette already fails loudly instead of reaching the network.

_Alternative considered:_ enabling it whenever the provider is `local`. Rejected for the reason `asistente-redaccion-sin-enmascarado-local` D1 gives: it would silently change the prompt of every existing local deployment, and each optimization is a hypothesis the evaluator has to confirm.

### D2 — One option for the glossary and for the dedication catalog

Reading `designaciones.dedicaciones.nombre/codigo` live adds a «VALORES POSIBLES» entry to the prefix of both roles. That changes the prefix for every provider, which would invalidate the recorded cassettes. So the addition is **also** off by default, behind the same option.

_Alternative considered:_ a second independent option for the dedication catalog (for example `DedicacionesEnElPrefijo`). Rejected. The two are one hypothesis — "give the model the Department's vocabulary" — and the term «dedicación» of the glossary points at the catalog's values, so each is half-useful without the other. Measured together, there is one run to read and one decision to take. If the gate result is ambiguous, a follow-up can split them; the code is shaped so that splitting is a second boolean and a second branch in one method (D5), not a redesign.

### D3 — The file: `Recursos/glosario.json`, embedded next to the examples

Same mechanism as the examples catalog: `<EmbeddedResource Include="Recursos\glosario.json" />`, read with `GetManifestResourceStream`, resource name `Modules.Asistente.Recursos.glosario.json`, `version` and `descripcion` at the top. A new internal class (`CatalogoDeGlosario`) parses it with `System.Text.Json` into immutable records and validates the structure at load (D4). It is read lazily and only when the option is on, so a malformed file cannot take down a default deployment.

Schema:

```json
{
  "version": 1,
  "descripcion": "…",
  "terminos": [
    {
      "termino": "en Decanato",
      "sinonimos": [],
      "explicacion": "Pedido en la etapa de revisión del decanato.",
      "referencias": [
        {
          "columna": "designaciones.pedidos.estado",
          "valores": ["en_revision_decanato"]
        }
      ]
    },
    {
      "termino": "en Cátedra",
      "sinonimos": ["volvió a la cátedra"],
      "explicacion": "Pedido devuelto que tiene el jefe de cátedra; no incluye borradores.",
      "referencias": [
        { "columna": "designaciones.pedidos.estado", "valores": ["devuelto"] },
        {
          "columna": "designaciones.pedidos.propietario_actual",
          "valores": ["jefe_catedra"],
          "verificaContra": "identity.roles.code"
        }
      ]
    },
    {
      "termino": "carga horaria total",
      "sinonimos": ["total de horas"],
      "explicacion": "Suma de las horas de dictado, de investigación y externas de la designación.",
      "referencias": [
        { "columna": "designaciones.designaciones.horas" },
        { "columna": "designaciones.designaciones.horas_investigacion" },
        { "columna": "designaciones.designaciones.horas_externas" }
      ],
      "sql": "COALESCE(horas,0) + COALESCE(horas_investigacion,0) + COALESCE(horas_externas,0)"
    }
  ]
}
```

Field rules:

- `termino` (required, unique ignoring case and accents), `sinonimos` (optional), `explicacion` (required, one line, Spanish).
- `referencias` (required, at least one). Each has `columna` (`schema.table.column`, required), `valores` (optional, the exact stored values; several values mean "any of"), and `verificaContra` (optional, `schema.table.column` of a catalog column that is the source of truth for the values; only allowed when the column has no `CHECK` and is not itself a catalog).
- `sql` (optional hint, short).

**How composite and derived terms are represented.** The references of a term are always a **conjunction**: "en Cátedra" is `estado = 'devuelto'` AND `propietario_actual = 'jefe_catedra'`; "finalizado" is one reference with three values (an `IN` list). The renderer derives the `col = 'v'` or `col IN (…)` text from the references, so a stored literal exists in exactly one place and cannot diverge from what the test verifies. Only what is not a conjunction of equalities and lists — the sum of three hour columns — needs the `sql` hint. The hint is checked by closure (D4): every quoted literal must be among the term's values and every identifier that is not an SQL word from a short allow-list (`COALESCE`, `NULL`, `IN`, `AND`, `OR`, `NOT`, `IS`) must be the column name of one of the term's references. So a hint cannot smuggle in a column or a literal that the tests did not see.

_Alternatives considered:_ (a) free-form text with columns and values written inside it, validated by regex. Rejected: a regex on prose either misses a typo or rejects good prose, and the rule "everything the glossary names is verified" would rest on the regex. (b) A mini query language (`estado = 'x' AND …`) parsed by the loader. Rejected: it is a parser to maintain for 24 entries; the conjunction rule plus one verified hint covers every term in scope. (c) Per-entry `SELECT` snippets executed in the test. Rejected: it verifies that the SQL runs, not that every name in it is the one the entry claims, and it invites copying the examples catalog's role.

**Prose is checked too.** `explicacion` is plain Spanish. Any identifier written between backticks in it (for example `dedicacion`, the legacy text column that the term «dedicación» tells the model not to use) must exist as a column or table readable by the basic role. A qualified `schema.table.column` outside `referencias` is rejected, so everything a term says about the data is in the references.

### D4 — The tests the ticket demands, against the migrated database

A new `GlosarioInstitucionalTests` (a `ClasePostgresAislada`, like `EjemplosEjecutablesTests` and `PrefijoDeEsquemaTests`, on the migrated test database; no seed needed since the checked values are migration-seeded or constraint text). It checks, for every entry of the real file:

1. **Structure** (a pure test, no database): required fields, at least one reference, unique terms, `columna` shape, `verificaContra` only where allowed, hint closure.
2. **Column exists.** Every referenced column, and every backticked identifier of an explanation, exists in `information_schema.columns` (or, for a table name, `information_schema.tables`).
3. **Column is readable by the basic role.** The column is in `columnas_concedidas.asistente_ro` of `manifiesto-privilegios.json`. A schema or table absent from the manifest (`tareas`, `aulas`) fails here.
4. **Column is not sensitive.** The column is `publica` in `manifiesto-sensibilidad.json`. A `sensible-valor` or `sensible-texto` reference fails.
5. **Value exists**, by the first rule that applies to the column:
   - the column has a `CHECK` constraint with string literals (`pg_constraint`, `contype = 'c'`, `conkey` containing the column): the value must be among the literals of that definition. This covers `pedidos.estado`, `pedidos.novedad`, `pedido_historial.accion`;
   - the reference declares `verificaContra`: the value must be a row of that column (`identity.roles.code` for `propietario_actual`, which has no `CHECK`);
   - the table is one of the declared catalog tables in the test (`designaciones.cargos`, `designaciones.dedicaciones`, `identity.roles`): `SELECT EXISTS (… WHERE col::text = @v)`;
   - none applies: **the test fails**. A value that cannot be verified mechanically must not be in the file.
6. **Both roles see the same block** and it is deterministic (D6).

Each rule is written failing first against a deliberately broken entry (a nonexistent column, an ungranted schema, a `sensible-texto` column, the value `aprobado` on `estado`, a hint with a foreign column), so that a verifier that silently stops verifying is caught. The same pairing as `PrefijoDeLosCassettesTests.El_guard_reconoce_una_huella_ajena`.

The check by `CHECK` text deserves a note: PostgreSQL renders `IN (…)` as `ARRAY['a'::text, …]`, so the test extracts quoted literals and unescapes `''`; it does not try to evaluate the constraint.

### D5 — Dedications as a declared closed catalog under the option

`LectorDeValoresDeCatalogo` keeps `CatalogosCerrados` exactly as it is (the four columns), and gains a second declared list for this change, `CatalogosDelGlosario`, with `designaciones.dedicaciones.nombre` and `.codigo`. `LeerAsync` takes a flag and reads `CatalogosCerrados` or `CatalogosCerrados` plus the second list, in that order, so the existing four values keep their positions in the block.

- `PrefijoDeEsquemaPuroTests.Solo_se_enumeran_tablas_de_catalogo_sin_datos_personales` is extended **deliberately**: `designaciones.dedicaciones` is added to the hand-written admitted list, with a comment stating why it qualifies — six rows fixed by regulation (`CHECK (codigo BETWEEN 1 AND 6)` in migration 009), no personal data, every granted column `publica`. The test keeps iterating both lists, and the admitted list is still written by hand and not derived, which is the whole point of the guard: the vocabulary travels whole to the model provider, so the list is declared, never detected.
- A new pure test pins that, with the option off, the declared list equals today's four columns, so the default cannot drift by editing the second list.
- `Ninguna_columna_declarada_pasa_el_tope_de_valores` is extended to the flagged list.
- `codigo` is `SMALLINT`; the reader casts to text, so the block lists `'1'` to `'6'`. PostgreSQL resolves `codigo = '3'` for an untyped literal. A test builds the prefix from the real database and runs a query that uses that literal form, so a model copying it is not taught a failing filter.

_Alternative considered:_ appending the dedication catalog to `CatalogosCerrados` with a gate at read time. Rejected: the "default list equals today's" invariant is then a runtime filter instead of a declaration visible in the diff.

_Alternative considered:_ also carrying the dedication values as the glossary row 12 («categoría N»). Rejected by the product owner: the values are kept by the catalog and stay correct if it changes.

### D6 — Rendering: where the block goes, why that order, and why it is stable

Order of the fixed part with both options on:

1. instructions · 2. «VALORES POSIBLES» (the four columns, then the dedications) · 3. the schema (compact or not) · 4. «GLOSARIO INSTITUCIONAL» · 5. examples.

Why:

- The prefix cache keys on the leading bytes. Everything that is the same across every option combination goes first (instructions, values, schema); what is optional goes after it, so a deployment that turns an option on invalidates the cache from the point of the change and not from the start.
- The glossary talks about columns and values the model has just read in the schema and the values block, so it comes after them.
- The examples stay last, as `EjemplosEnElPrefijo` has them today. Turning the glossary on inserts text before them but does not reorder or alter them: with the glossary off the examples block is the same string it always was.

The block is appended by `RenderizadorDeEsquema.Renderizar` through an optional parameter (`bloqueDeGlosario`, null by default) at the end of both branches (compact and not), not by `GeneradorDeSql`. This is deliberate and is the one place the stated order could have gone wrong: if the glossary were appended in `GeneradorDeSql` next to the examples, it would be outside `EsquemaParaPrompt.Huella`, so the evaluator seal would not change when the option does, and the evaluator's regression gate could compare a glossary run against a no-glossary baseline as if they were the same prompt. Built into the schema prefix, it is inside the hash. (The examples are outside the hash today, which is why `PrefijoEstable` is what the cassette key hashes. This change does not alter that.)

Format — compact, one line per term, from the parsed fields and not from the file bytes (so line endings or JSON formatting do not move the prefix):

```
GLOSARIO INSTITUCIONAL

Vocabulario del Departamento y el valor exacto que le corresponde en el esquema. Cuando la pregunta use uno de estos términos, filtrá con la columna y el valor indicados; no uses las palabras de la pregunta como literal.

- «en Decanato»: pedido en la etapa de revisión del decanato → designaciones.pedidos.estado = 'en_revision_decanato'
- «finalizado» (también: «cerrado», «terminado»): pedido que ya no sigue en el circuito → designaciones.pedidos.estado IN ('en_lote', 'rechazado', 'cancelado')
- «carga horaria total» (también: «total de horas»): suma de las horas de la designación → designaciones.designaciones.horas, designaciones.designaciones.horas_investigacion, designaciones.designaciones.horas_externas; SQL: COALESCE(horas,0) + …
```

The Spanish wording of the explanations above is illustrative; the file's explanations are written from the "Corresponde a" column of the reviewed draft.

The renderer sorts nothing: terms, synonyms, references and values go in the file's order, which the file's author controls. The block never reads the database or the clock.

**Same block for both roles.** The block depends only on the file, so it is the same string in the basic and the personal-data prefix. What would break that is a reference to a column only the personal-data role reads; D4 rule 3 forbids it (basic-role grants only) and rule 4 forbids sensitive columns. Both are test-enforced, and a third test compares the two blocks.

**Size.** Roughly 24 lines of ~150 characters, on the order of 1k tokens, plus about 60 for the dedication values. Against the 9.8k longest measured request that leaves the 13,312 slot with room, but it is a prediction: a task measures the real prompt size with the option on. If the measured request does not fit the slot, the answer is a decision of the product owner, not a silent trim of terms.

### D7 — The glossary and prompt rule 8

Prompt rule 8 says to compare free text with `public.unaccent(col) ILIKE public.unaccent('%…%')`, and some column comments prescribe exact comparisons. The glossary values are the opposite case: stored codes and the exact catalog strings, for which equality is right (`estado = 'en_lote'`, `novedad = 'Cambio de cargo o dedicación'`). The block's introduction says "valor exacto" and "no uses las palabras de la pregunta como literal", consistent with the closed-catalog block. A glossary line must never prescribe `ILIKE` for a coded column, and must say which sense of a polysemous word it means (D8).

### D8 — Polysemous words

«estado» (pedido, tarea, proyecto, solicitud de aula), «tipo», «adjunto» (cargo / archivo de un pedido), «cátedra» (materia / rol / área) collide. Each entry that touches one of them says which sense it means in its explanation («adjunto» says it is the rank `Profesor Adjunto`, not a file of a request; «en Cátedra» says it is a state of a request, not the subject), and the terms are named so that the bare ambiguous word is not itself a term. No entry is created for the bare word «estado» or «tipo» on its own except «tipo de pedido», which names the sense.

### D9 — Content: 24 terms, decided, mined from the repo and not from the datasets

The content is the product owner's, decided on 2026-10-03: the terms of section 1 of the reviewed draft, minus row 12 (dedication values, D5), plus rows 24 and 25. This change does not add, drop or reword terms. They were mined from the repository, the open PRs #27/#32/#38 and `feature/reserva-aulas`, and **deliberately not derived from the evaluation datasets**: a glossary written after reading the items the model fails on would tune the prompt to the test, and the measurement would stop meaning anything. It was **not validated with the Department** (ARS-65 is open); see Risks.

| Group                | Terms (references)                                                                                                                                                                                                                                                                                                                                                           |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Request circuit (10) | en Coordinación, en Secretaría, en Decanato (`pedidos.estado` = `en_revision_*`); aprobado (`en_lote`); finalizado (`en_lote`, `rechazado`, `cancelado`); en Cátedra (`devuelto` and `propietario_actual` = `jefe_catedra`); tipo de pedido, cambio (`pedidos.novedad`); quién lo aprobó (`pedido_historial.accion` = `aceptar`); en qué bandeja está (`propietario_actual`) |
| Dedication (1)       | dedicación (`dedicaciones.id` via `designaciones.dedicacion_id` / `pedidos.dedicacion_solicitada_id`; never the text column)                                                                                                                                                                                                                                                 |
| Roles, not ranks (5) | jefe de cátedra, coordinador, secretaría, decanato, administrativo (`identity.roles.code`; `user_roles.role_id` and `deleted_at` for "current")                                                                                                                                                                                                                              |
| Rank nicknames (6)   | titular, asociado, adjunto, JTP, ayudante de primera, ayudante de segunda (`designaciones.cargos.codigo`)                                                                                                                                                                                                                                                                    |
| Hours (2)            | carga horaria (`designaciones.horas`); carga horaria total (sum of three columns, with a hint)                                                                                                                                                                                                                                                                               |

**«En Cátedra»** means requests in state `devuelto` whose `propietario_actual` is `jefe_catedra`; drafts are excluded. It follows the interface of PR #32. That PR is not merged: the glossary uses its labels («en Coordinación», «en Secretaría», «en Decanato», «aprobado», «finalizado», «en Cátedra») as the term and the stored literals as what is verified, so a future rename of the labels costs a synonym and not a failing test.

### D10 — The evaluation seal and the comparison

With the option on the schema prefix changes, so the evaluator's seal changes and its own regression gate refuses to compare against the frozen baselines (it demands regeneration). That is correct and not a problem to work around: the comparison for this change is **item by item against the local baseline**, with `eval-local/comparar.py`. Two runs, both on Qwen3-8B Q4_K_M with the optimized profile and `EsquemaCompacto` + `EjemplosEnElPrefijo` on:

1. `eval-local/ARS-164/control` with the option off. It must equal the baseline `eval-local/ARS-162/qwen3-8b-q4-B/` with zero changed items; if it does not, the environment moved and the comparison is not valid.
2. `eval-local/ARS-164/glosario` with `Asistente__GlosarioEnElPrefijo=true`.

The runs are deterministic, so one run per configuration is enough: a changed item is attributable to the option. Never `--congelar`: freezing would turn a research run into a new frozen baseline.

**Promotion rule** (the product owner's): only if capacidad **and** robustez improve and diálogo and social do not worsen, the option goes into the RTX 3070 block of `.env.example` and into `infra/compose/compose.asistente-local.yml`. Otherwise it stays off everywhere and the result is documented. "Improve" and "not worsen" are read on the aggregate hits per axis and then item by item: a swap that nets to zero in diálogo or social (one item up, one down) is reported with the item names and judged by the product owner, not hidden by the sum. An ambiguous result leaves the option off; a follow-up can split the glossary from the dedication catalog (D2).

### D11 — Documentation, in both outcomes

The option is documented whether or not it is promoted: the README options table (a boolean row with its default; `OpcionesDocumentadasTests` is extended to check boolean rows, since today it reads only integer and "vacío" rows), the «Optimizaciones para un modelo propio» section, a short «Cómo se agrega un término al glosario» note mirroring «Cómo se agrega un ejemplo al catálogo» (the three verifications that run on their own, the rule that a value must be verifiable, and the invariant that the glossary and the datasets stay disjoint), `docs/architecture/modelo-local.md` §6 (and §8 with the measurement), and `docs/architecture/domains/asistente.md` where it describes the fixed part of the prompt. The "Categoría 0" inconsistency is recorded in `docs/quality/tech-debt.md`.

## Risks / Trade-offs

- **The vocabulary is unvalidated.** Mined from the repository and from open PRs, not from the Department (ARS-65 is open). A term the Department never says costs prefix and may mislead; a term it says that is missing is just the status quo. → The file is versioned and small; the glossary says "valor exacto" and not "siempre"; a "how to add or change a term" note; validation stays an open follow-up.
- **Tuning to the test.** → The vocabulary was deliberately not derived from the datasets; adding a term in response to a failing item is out of scope and the README note states the disjointness invariant (like the examples catalog).
- **Prefix growth and prefill cost.** About 1k tokens more in a 13,312-token slot, paid once per slot with cache and on every call without. → The task that measures the real prompt size; an over-budget result is escalated, not trimmed. The option is off by default.
- **A line that contradicts a column comment or a prompt rule.** The known tension is rule 8 (`unaccent … ILIKE`) against exact comparisons (D7). Another is `estado`'s comment, which already says `en_lote (aprobado y agrupado…)`: the glossary says the same thing from the other direction. → Terms point at stored codes only; the two roles share one block; a review of each entry against the comments of its columns is part of writing the file.
- **Staleness when the interface renames a state again.** PR #32 is not merged. → The tests check the stored literal, not the label; labels are synonyms.
- **Polysemy.** → D8.
- **Cassettes.** With the option on, the fixed part differs from every committed cassette, so replay mode misses. → Expected: the option is for the local model, and the Claude re-recording is deferred and joint with ARS-161. A miss without the re-record variable fails and never reaches the network.
- **One option hides which half helped** (glossary or dedication values). → Accepted (D2); the follow-up split is cheap.
- **A `CHECK` rewrite breaks the literal check.** If a future migration moves a state out of a `CHECK`, rule 5 fails by design. → That failure is the point: it names the term whose value is no longer verifiable.

## Migration Plan

1. Ship with the default `false`: no behavior, prompt or cassette change in any environment.
2. Run the local evaluator (D10) and decide the promotion.
3. If promoted, add the option to the RTX 3070 profile and restart the backend; otherwise leave it off.
4. Rollback: set `Asistente__GlosarioEnElPrefijo=false` or remove it and restart. Nothing is persisted and no migration is involved.

## Deferred (not part of this change)

The paid re-recording of the corpus with Claude, and the Claude-side evaluation of the ticket's acceptance criteria ("capacidad and robustez improve without worsening diálogo or social, per-item gate" with Claude), are deferred and stay coupled to ARS-161: both change the fixed part of the prompt, so there is a **single** paid re-recording for both. Nothing here prepares or runs it. Because the option is off by default, the default prefix does not change and the cassettes stay valid, so nothing forces it now.

## Measured outcome (2026-10-03)

Setup: evaluator against Qwen3-8B Q4_K_M on an RTX 3070 (llama-server build 11371, one slot of 16,384 tokens), optimized profile. One run per configuration (the runs are deterministic: three identical runs changed zero items). Reports in `eval-local/ARS-164/control/` and `eval-local/ARS-164/glosario/` (untracked).

| Run                        | Capacidad | Cap. normalized @0.5 / @1.0 / @2.0 | Robustez | Diálogo | Social         | Truncated | Longest prompt | Run time | Turn p50 / p95 |
| -------------------------- | --------- | ---------------------------------- | -------- | ------- | -------------- | --------- | -------------- | -------- | -------------- |
| Control (option off)       | 26/34     | 66.2 % / 55.9 % / 35.3 %           | 12/15    | 10/11   | 18/20 (80.0 %) | 0         | 9,797 tokens   | 307 s    | 2.9 s / 7.5 s  |
| `GlosarioEnElPrefijo=true` | 25/34     | 61.8 % / 50.0 % / 26.5 %           | 12/15    | 10/11   | 17/20 (70.0 %) | 1         | 11,034 tokens  | 319 s    | 3.5 s / 7.2 s  |

- The control equals the local baseline (ARS-162: 26/34, 12/15, 10/11, 18/20) with zero changed items. With the option on the prefix hash differs, so the evaluator seal changes, as designed.
- Size: the glossary block is 4,836 characters; the whole prefix with it is 31,018 (compact schema). The longest request grows by 1,237 tokens and fits both the 16,384 slot and the documented 13,312 one.
- Changed items (capacidad): `cap-014` wrong to correct; `cap-023` abstained to correct; `cap-013`, `cap-016` and `cap-026` (0 rows instead of 3) correct to wrong; `cap-008` wrong to truncated by the token ceiling. Social: `soc-016` correct to "the router captured a legitimate question". Robustez and diálogo: no item changed. Net in capacidad: 2 gained, 3 lost, one false answer more, one truncation that did not exist.

**Decision (D10): not promoted.** Capacidad and social worsen. The option stays off; `.env.example` and `infra/` are untouched (group 8 skipped). Nothing was re-recorded; the Claude-side re-recording and evaluation stay deferred with ARS-161.

**Hypothesis, unverified.** The reports do not keep the generated SQL, so the cause is not established. Two of the three losses use «cerraron» and «terminado»; the glossary declares «cerrado» and «terminado» as synonyms of «finalizado», a request state, and may have pulled questions about designations and education toward the request state. Those two synonyms were proposed in the draft without a source in the repository; the UI- and PR-sourced ones have one.

**Possible follow-up, not done.** A second version restricted to sourced synonyms. Risk: such a correction would be informed by the evaluation set, which strains the disjointness invariant; it must be measured on the full datasets and not only on the items that failed.

**Judgment calls made during implementation.** The bare synonym «tipo» was dropped from «tipo de pedido» (approved by the product owner on 2026-10-03); «carga horaria» is explained as the weekly hours of the designation, following the column comment; «finalizado» includes `en_lote`, following the UI of PR #32 (unmerged), although the column comment does not call it terminal; `pedido_historial.accion = 'aceptar'` as "approved" and the five `propietario_actual` values come from the code, not from column comments.
