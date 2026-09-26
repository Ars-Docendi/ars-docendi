## Context

Three independent additions to `Modules.Asistente`. See `proposal.md` for
motivation; this covers how each one fits the module's existing shape.

Ground truth gathered from the current code (all read-only, no code changed
by this planning change):

- `asistente.registro_analitico` (`database/asistente/002_asistente_registros.sql`)
  has a random `uuid` primary key (`gen_random_uuid()` default), no actor, and
  no precise timestamp (`dia date`), by design (TD-012). `asistente.registro_operativo`
  has `actor_id` + `ocurrido_en` but never the question text. The two tables
  share no key. A documented residual channel remains: physical row order
  (`ctid`) still correlates with operational insertion order; this change
  does not touch that, and must not add a _second_, easier channel.
- The schema `asistente` is denied entirely to both read-only roles
  (`REVOKE ALL ON SCHEMA asistente FROM asistente_ro, asistente_ro_pii`),
  declared once in `002_asistente_registros.sql` and mirrored as a
  schema-level `"denegado"` entry in `database/asistente/manifiesto-privilegios.json`
  (no per-table entries for that schema — only exposed schemas list tables).
- `ResultadoDelTurno` / `RespuestaDelAsistente` (`Application/Turno/ResultadoDelTurno.cs`,
  `Api/ModelosAsistente.cs`) carry **no** analytic-row identifier today. The
  analytic row is written by `IRegistroDelTurno.RegistrarAsync`, fire-and-forget,
  after the HTTP response is normally already decided — the id is generated
  wherever the insert happens and is never returned to the caller.
- `Sugerencias` (`Application/Abstencion/Sugerencias.cs`) already exists and
  is already catalog-only and model-free: `Sugerencias.Para(pregunta, ejemplos)`
  picks catalog entries by lexical (Jaccard) similarity to the _failed_
  question, falling back to the first entries of the catalog. It is wired
  only into `NoContestable` / `SinDatos` / the pending-clarification path
  (`CarrilSql.cs`, `CapaConversacional.cs`) — **never** into the successful
  (`Respondida`) path (`CarrilSql.cs` `Respondida`/success branch passes no
  `Sugerencias:` argument, so it defaults to empty).
- Critically, `ISelectorDeEjemplos`/`SelectorDeEjemplos` (used by `Sugerencias.Para`)
  has **no privilege awareness at all** — it is a pure in-process lexical
  matcher over the full embedded catalog. The actual per-actor executability
  check lives in `Infrastructure/CatalogoDeCapacidades.cs`
  (`ElegirEjemplosAsync` / `EjecutableAsync`, `internal static`): it runs
  `EXPLAIN <sql>` on a connection with the actor's RLS context applied and
  keeps only the candidates that don't fail with `42501`. This is the
  mechanism `GET /api/asistente/capacidades` uses for its own `Ejemplos`
  field, and it is the one this change must reuse for follow-up suggestions,
  since none currently exists on the rejection path either — a pre-existing
  gap this change does not silently inherit for the _new_ surface it adds.
- The example catalog (`ejemplos-sql.json`, read by `SelectorDeEjemplos`)
  exposes exactly three fields per entry: `pregunta`, `sql`, `categoria`. The
  six category values are fixed and generic (`consulta_simple`,
  `filtro_temporal`, `cruce_de_tablas`, `agregacion`, `no_contestable`,
  `ambigua`) — there is no table/domain metadata on either the catalog
  entries or `GeneracionDeSql` beyond that same `categoria` field. "Relation
  to the last answer" can only mean _category match_ today; matching by
  "tables touched" would require parsing SQL (catalog and/or generated) with
  no existing utility for it.
- The conversational thread (`IAlmacenDeHilos`) is in-memory, keyed by actor,
  TTL 2 hours (`README.md`, "La capa conversacional"). `IIdempotencia` is the
  existing precedent for actor-scoped, in-memory, non-persisted, short-TTL
  state in this module (doubles-submit guard) — the pattern this design
  reuses for the feedback token's validity window.
- `POST /api/asistente/consultas` already requires `[Authorize(Policy = Permisos.AsistenteConsultar)]`
  and an `Idempotency-Key` header; the actor never comes from the request
  body (`ConsultaDelAsistente` deliberately excludes it).

## Goals / Non-Goals

**Goals:**

- Let a user rate a turn without weakening the operational/analytic split.
- Let a user get the rendered table as a real `.csv` file, safely.
- Surface catalog-backed, capability-safe follow-up questions after a
  successful answer.

**Non-Goals:**

- Re-identifying the author of an analytic row for any purpose, including
  abuse investigation of feedback itself (out of scope by the same TD-012
  reasoning the module already accepted).
- Building a general-purpose SQL "tables touched" extractor. Category match
  is the only relation signal this change ships; a table-level relation is a
  larger, separate effort (see Open Questions).
- XLSX export (explicitly rejected in the proposal to avoid a new dependency).
- Feedback moderation UI, abuse handling, or aggregate reporting beyond raw
  rows in the new table.

## Decisions

### D1 — The feedback key is the analytic row's own UUID, minted application-side and returned once

The client needs _something_ stable to submit feedback against. The only
identifier that satisfies "linked to the analytic row, never to the actor"
by construction is `registro_analitico.id` itself.

Today that id is a DB-generated default the application never sees. This
design moves id generation to the application: `IRegistroDelTurno.RegistrarAsync`
(or a thin wrapper around it) generates a `Guid` with `Guid.NewGuid()` (a
random v4 UUID, ~122 bits of entropy — the same guarantee `gen_random_uuid()`
gave), inserts the analytic row with that explicit id, and returns it to the
caller. `ResultadoDelTurno` gains a new field — e.g. `ClaveDeRetroalimentacion : Guid?`
— populated **only** when the turn actually wrote an analytic row (i.e. on
`Respondida`, `NoContestable`, and `ServicioDegradado`+capture cases the same
way `IRegistroDelTurno` already covers them; never fabricated for a turn that
didn't produce one). `RespuestaDelAsistente` maps it to a new optional JSON
field. The turn's existing four-state contract does not change; this is a
strictly additive field.

**Alternative rejected**: `INSERT ... RETURNING id` after letting Postgres
generate it. Works equally well cryptographically, but keeps id generation
inside the fire-and-forget registration path that today deliberately "never
fails the turn" (`IRegistroDelTurno` doc comment) — making the id available
to the _response_ would require either registering before responding (new
ordering dependency the module avoids on purpose: registration failing must
never fail the turn) or a second round-trip. Generating the id in the
application removes that coupling: the id exists before any I/O, is handed to
both the response and the (still fire-and-forget) registration write, and a
registration failure still can't touch the response.

### D2 — Authorization is possession of the token, not actor identity, bounded by a server-side TTL matching the thread's

The analytic row has no actor column _by design_ — adding one to authorize
feedback would reopen exactly the join TD-012 closed. So "only the author may
rate it" cannot be enforced by comparing actor ids. It is enforced instead
by:

1. **Unguessability**: the token is a random UUID, returned by HTTPS exactly
   once, in the same response body as the answer, to the actor who asked. No
   other endpoint or log line this change adds re-exposes it (see D3 for why
   this doesn't already leak through something else).
2. **A short validity window**, not the row's own 90-day retention: an
   in-memory store (same shape as `IIdempotencia`: no persistence, cleared on
   redeploy) records `token → mintedAt` and rejects (404 — a stale/unknown
   token gets the same treatment as "never issued", not 403, so a probe can't
   distinguish "expired" from "made up") any feedback submitted more than 2
   hours after minting — the same TTL as `IAlmacenDeHilos`, so a token stops
   being live at roughly the point the _thread_ it was born into would also
   have expired. This bounds the exposure window without adding a second
   actor-keyed store: the store is keyed by the token itself, not by actor,
   which is what keeps it from becoming a third place that maps a token back
   to who asked.
3. Still requiring `[Authorize(Policy = Permisos.AsistenteConsultar)]` on the
   feedback endpoint — the token authorizes _which turn_, the policy
   authorizes _that this is an assistant user at all_.

**Accepted residual risk, stated explicitly**: any actor who has the token —
including one it wasn't minted for, if it leaked through a channel outside
this design's control (e.g. shared screen, browser history sync) — can submit
feedback for that turn. The value protected (a thumbs vote + a coarse reason
on an already-anonymous analytic row) does not justify a heavier scheme; this
mirrors how the module already accepts the `ctid` residual channel in
TD-012 as disproportionate to close. If this changes, it will be because the
_value_ of a feedback row changed, not because the token scheme did.

**Alternative rejected**: signing the token (HMAC) instead of an in-memory
TTL store. Would allow stateless expiry validation, but adds a new secret to
manage for a value this low, and the module has zero precedent for
application-level token signing — `IIdempotencia`'s in-memory pattern is the
one already trusted here for exactly this shape of problem (short-lived,
per-turn, no persistence).

### D3 — Auditing this endpoint's own request logs must not become the second re-linking channel

The question the proposal asks explicitly: could the feedback endpoint itself
re-link actor↔analytic row via HTTP/request logging, or via the quota/
idempotency stores? Checked against the actual interfaces:

- `ICuotaDelActor` and `IIdempotencia` are keyed by `(actor, ...)` and never
  see the analytic id (they operate on `ResultadoDelTurno`/turn counts before
  the record ever exists as two rows). The feedback endpoint does not touch
  either — it has no reason to spend a call against the model, so it is not
  quota-gated, and it is naturally idempotent by content (see D4), so it does
  not need the idempotency store either.
- Structured request logs (Serilog) _do_ create the channel if they're not
  careful: a log line for `POST /api/asistente/consultas` naming the actor
  (from `ICurrentUser`) sits at one timestamp, and a later log line for
  `POST /api/asistente/retroalimentacion` naming the same actor plus the
  token links that actor to that specific analytic row just as surely as a
  SQL join would — worse, it doesn't even need DB access, just log access.
  **Decision**: neither endpoint's structured log entry may include the
  feedback token and the actor id in the same log event. The turn's log entry
  logs the actor (as it already does for quota/observability) but not the
  token; the feedback endpoint's log entry logs the token but not the actor.
  This is a tasks.md item, not a code sketch here, but it is load-bearing:
  without it, this feature would open the exact channel TD-012 closed, one
  layer up the stack.

### D4 — Feedback is an upsert, one row per analytic id, last vote wins

"Idempotency/one-feedback-per-turn: can a user change their vote?" — yes.
Modeled as `INSERT ... ON CONFLICT (analitico_id) DO UPDATE` (single row per
analytic id, primary/unique key = `analitico_id`), so re-submitting (change
of mind, or a retried request) is naturally idempotent without a separate
idempotency-key header. No history of prior votes is kept — this is a rating,
not an audit trail, and keeping one would be a second place a reason-code
history could re-identify a rare complaint the way TD-012 already warns
about for `intencion_sombra`.

### D5 — Which turn states are rateable

Only `Respondida` (an answer was actually given). `NecesitaAclaracion` blocks
the turn (nothing to rate yet — the interaction isn't over) and
`ServicioDegradado` isn't the assistant's answer to rate, it's the absence of
one. `NoContestable` is excluded too: rating a rejection would mean rating
"the system correctly said it doesn't know", which the abstention design
already treats as a distinct, successful outcome in its own right, not a
failure to grade. Only `Respondida` turns get a `ClaveDeRetroalimentacion`
(D1); the feedback endpoint 404s for a token it never issued, which already
covers "tried to rate a turn that wasn't answered".

### D6 — Follow-up suggestions run after `Respondida`, server-side, matched by category, filtered by the existing executability check

Given the catalog only exposes `categoria` (Context), the relation signal is:
same `categoria` as the answered turn's `generacion.Categoria`, excluding the
catalog entry whose `sql` is textually identical to the one just executed
(don't suggest the question just asked). This reuses `Sugerencias`'s existing
shape (`Application/Abstencion/Sugerencias.cs`) with a new selection strategy
next to the existing lexical one, rather than replacing it — the rejection
path keeps matching by lexical similarity to the failed question (that
signal is still the right one there; there is no "answer" to relate to).

Privilege filtering **must** be server-side: it requires a live, actor-scoped
DB connection to run `CatalogoDeCapacidades.EjecutableAsync`'s `EXPLAIN`
check (the same `internal static` helper `/capacidades` already uses),
which a client can never do. Concretely: filter catalog entries by category
first (cheap, in-process), then run the `EXPLAIN` check only on that
category-filtered, deduplicated candidate set (bounded — the catalog has ~20
entries total, so worst case is a handful per category), capped at 3
(matching the existing `Sugerencias.Cuantas` convention). If zero candidates
survive the `EXPLAIN` check, `Sugerencias` on the response is empty — no
filler, per the proposal's explicit instruction, exactly like today's "no
lexical match → generic catalog fallback" _except_ there is no generic
fallback here: an unfiltered fallback would risk suggesting something the
actor can't run, which is the one thing this feature must never do.

**Alternative rejected**: computing suggestions client-side from the
`/capacidades` payload's `Ejemplos` (already privilege-filtered) intersected
with category client-side. Rejected because `/capacidades` deliberately
returns 4–6 _diverse_ examples across categories for discovery, not "all
executable examples of category X" — reusing it would mean either a second,
differently-shaped client-side `/capacidades` call per turn (defeats "cheap,
already denied elsewhere" cost story) or accepting a much weaker match. Doing
it server-side, once, right after the turn already has an open,
actor-scoped connection open in `EjecutorDeConsulta`'s flow, is both cheaper
and correct.

### D7 — CSV export: injection, encoding, truncation, all client-side

Purely a frontend concern — no new API surface. `TablaDeResultado.tsx`
already has the exact `filas`/`columnas` (with `Sensible` masking already
applied by the time they reach the component) and the existing `Truncado`
flag used today for the on-screen "truncated" indicator (`ModelosAsistente.cs`
`RespuestaDelAsistente.Truncado`). The CSV formatter sits next to
`tablaComoTsv` in `portapapeles.ts`.

- **Formula/CSV injection**: any cell whose _first character_ is `=`, `+`,
  `-`, `@`, tab, or CR is prefixed with a single leading `'` before quoting.
  This is the same mitigation OWASP documents for CSV injection and is
  applied uniformly — including to numeric-looking cells that happen to
  start with `-` or `+` (a negative number), because a spreadsheet reading a
  leading apostrophe on a numeric string still displays the number as text
  reliably in Excel/LibreOffice/Sheets, and the alternative (special-casing
  "looks numeric") reintroduces exactly the ambiguity the mitigation exists
  to remove.
- **RFC 4180 quoting**: any cell containing a comma, double quote, or line
  break is wrapped in double quotes with internal double quotes doubled
  (`"` → `""`), applied _after_ the injection prefix above so the leading `'`
  itself never needs escaping.
- **Encoding**: file is written as UTF-8 with a leading BOM (`﻿`) — the
  documented, standard fix for Excel (Windows) misreading UTF-8 accented
  characters (Spanish names, "ó", "ñ", etc.) without one. Line endings CRLF
  (`\r\n`), the format Excel expects.
- **Truncation disclosure**: when `truncado` is `true`, the export must say
  so — not silently emit a partial file that looks complete. Decision:
  append a trailing comment-like row (a row whose single populated cell
  reads, e.g., `"Resultado truncado: se muestran menos filas de las que
cumplen la consulta."`) rather than encoding it in the filename alone
  (filenames get renamed/lost; a screen-reader user opening the file in a
  spreadsheet app should not have to know to check the filename). The
  filename additionally carries a `-truncado` suffix as a secondary,
  glanceable signal, matching the project's existing pattern of a boolean
  flag never leaking a count (`Truncado` is a bool everywhere in this module,
  on purpose — RF constraint already documented on `ResultadoDelTurno`); the
  trailing row states the fact, never a number of missing rows.
- **File naming**: `asistente-resultado-<hilo-corto>-<fecha-ISO>[-truncado].csv`,
  ASCII-safe, no characters that need escaping across OSes.
- **Masking**: the export iterates the _already-rendered_ cell values (the
  same array `TablaDeResultado` paints), never re-fetches or reaches for an
  unmasked value — there is no unmasked value available client-side to leak
  in the first place, since the backend already replaced sensitive values
  before the response left the server (enmascaramiento). This is stated as a
  requirement anyway (spec `asistente-exportacion-csv`) so a future refactor
  that threads a hidden raw value through for some other purpose can't
  silently make the export un-mask it.

## Risks / Trade-offs

- **[Risk]** A new response field (`ClaveDeRetroalimentacion`) is one more
  thing a client can hold onto and replay. → Mitigated by D2's short TTL and
  by scoping its only power to "vote on this one turn", a low-value action.
- **[Risk]** Category-only matching (D6) is a coarse relation signal — two
  unrelated questions sharing `cruce_de_tablas` will match. → Accepted: the
  alternative (no metadata to do better with) is either building a table
  extractor (bigger, separate effort) or shipping nothing; category match is
  strictly better than nothing and never violates the "must be executable"
  constraint, which is the one that actually matters for safety.
- **[Risk]** Moving analytic-id generation into the application (D1) means a
  future contributor could be tempted to also thread the id into
  `registro_operativo` "for consistency" (the same anti-pattern the module's
  SQL comments already warn about for `intencion_sombra`). → Call this out
  explicitly in the new migration's comments, following the file's existing
  convention of writing the "why not" inline.
- **[Risk]** The new table lives in the `asistente` schema, already denied
  wholesale — a future migration to a _different_ schema for feedback would
  need its own explicit `REVOKE`, unlike today. → Mitigated by keeping it in
  `asistente` (D-decision, see tasks.md) and adding a regression test
  alongside `ManifiestoPrivilegiosTests` that fails if the new table ever
  becomes readable by either read-only role, so the protection is verified
  and not just structurally implied.

## Migration Plan

Additive only: one new SQL migration file (new table + comments, no `ALTER`
of existing tables besides the new nullable field surfaced through existing
records — the analytic id column itself already exists and is not altered,
only its id is now supplied by the application instead of the default).
`ResultadoDelTurno`/`RespuestaDelAsistente` gain one new optional field each
— backward compatible for any client ignoring unknown fields. No rollback
concerns beyond the standard "new table, drop if truly needed"; nothing
existing is renamed, dropped, or retyped, so `ArquitecturaAsistenteTests`'s
DDL-shape guard should require no exemption beyond acknowledging the new
table.

## Open Questions

- **Table-level (not just category-level) relation for follow-up suggestions**:
  would need either parsing SQL for touched tables or adding table metadata
  to the catalog schema (`ejemplos-sql.json`). Deferred: category match ships
  now; if the coarse match proves unsatisfying in real use, extending the
  catalog schema with an explicit `tablas` field is the cheaper follow-up
  (versus SQL parsing) and does not change this change's specs or contract.
