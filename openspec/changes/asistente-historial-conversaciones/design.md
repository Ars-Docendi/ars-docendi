## Context

See `proposal.md` for motivation. This covers how persisted history fits the
module's existing shape, and confronts the one thing this change necessarily
changes about the module's privacy posture: today `asistente.registro_analitico`
is deliberately unlinkable to any actor (TD-012); this change adds a _second_,
brand-new table that **is** actor-linked by design, because "own history" only
means something if it is attributable to its owner.

Ground truth from the current code (read-only research for this planning
change; nothing below was modified):

- `HiloConversacional` (`Application/Conversacion/HiloConversacional.cs`) is
  the in-memory conversational thread: a `List<TurnoDelHilo>` of
  `(Pregunta, Cuando, SqlEjecutado)`, TTL 120 minutes
  (`OpcionesAsistente.VigenciaDelHiloMinutos`), one per actor, lost on
  redeploy, by explicit documented decision ("no se persiste, y es una
  decisión tomada"). **This is exactly the shape this change needs to persist**:
  question, resolved SQL, nothing else — the class comment already states the
  invariant this change must keep: "Guarda preguntas y nunca filas."
- `CapaConversacional.ResponderAsync` (`Application/Turno/CapaConversacional.cs`)
  is the single choke point every turn passes through on every exit path
  (success, budget timeout → `ServicioDegradado`, unhandled exception →
  `Fallo`): it always calls a private `RegistrarAsync` that writes
  `IRegistroDelTurno` (the two anonymized/attributed registers). The
  in-memory thread append (`conversacion.Agregar(...)`) happens only on the
  one path that resolves through the SQL carril (success). This is the
  correct hook point for the new persisted-history write, for the same
  reason it is the correct hook point for the existing registers: it is the
  only place that sees every outcome, already has `actor`, `mensaje`,
  `turno`, and a clock.
- `ResultadoDelTurno.ClaveDeRetroalimentacion` (added by
  `asistente-feedback-export-seguimiento`) is `Guid?`, populated only on
  `Respondida`, and is the exact id of that turn's `registro_analitico` row.
  History **must not** store or reference it: the feedback token's
  unlinkability to the actor (D1/D2 of that change) must stay exactly as
  designed, and history has no need for it — see Decision D8.
- The `asistente` schema is denied wholesale to both read-only roles
  (`REVOKE ALL ON SCHEMA asistente`, declared once in
  `002_asistente_registros.sql`, mirrored as a schema-level entry in
  `manifiesto-privilegios.json`). `retroalimentacion_turno` already lives
  there and needed **no** new `GRANT` and **no** manifest change. The two
  tables this change adds follow the identical pattern.
- `PurgaDeRegistros`/`ServicioDePurga` (`Infrastructure/`) is the module's one
  existing scheduled-purge mechanism: a `BackgroundService` ticking every
  `OpcionesAsistente.PeriodoDePurgaHoras`, resolving a scoped `PurgaDeRegistros`
  and calling `PurgarAsync`, which deletes from both existing registers by a
  configurable retention window and never crashes the host on failure (logs
  and retries next tick). This change extends it rather than building a
  second scheduler.
- The `asistente.ver_consulta` permission's seeding is the precedent for
  seeding `asistente.leer_historial_ajeno`: an EF Core migration in
  `ArsDocendi.Shared.Identity.Migrations` that runs an embedded, idempotent
  SQL script from `database/identity/`, `INSERT ... ON CONFLICT (code) DO NOTHING`,
  granted to **no role, not even `sys_admin`**, with the rationale written
  inline that a permission that starts empty is one that gets granted
  deliberately and traceably from `/membresia-roles`, never assumed.
  `Permisos.cs` (`ArsDocendi.Shared.Auth`) is the single source the backend
  checks against; the next free id in the seeded-permission UUID sequence
  (`b2000000-0000-4000-8000-0000000000xx`) is `...028` as of this writing
  (`...026`/`...027` already belong to `011_identity_permisos_pantallas.sql`) —
  tasks.md flags re-confirming this against `develop` at implementation time,
  since more than one branch can reserve ids concurrently (the existing SQL
  files already document this exact collision happening twice before).
- `ArquitecturaAsistenteTests.PuedenUsarLaCadenaDelDueno` is the allowlist of
  files that may reference the owner connection (`CadenaDuena`); every writer
  or reader of the `asistente` schema must be on it, because the two
  read-only roles cannot reach that schema at all.

## Goals / Non-Goals

**Goals:**

- Persist enough per turn to rebuild a conversation's question/answer-outcome
  timeline and to re-drive the rewriter's coreference on resume, without ever
  persisting result rows or the model's drafted answer text.
- Give a user full self-service control over their own history (list,
  rename, search, delete one/all, resume).
- Make "user X asked Y" answerable, honestly and by design, through the
  user's own history and through audited support access — this is a
  requirement, not a side effect: a mode that skips recording would directly
  contradict it.
- Give support a narrow, permissioned, always-justified, always-audited way
  to read another user's history for incident support — and make it
  structurally incapable of becoming a second query surface (no
  re-execution, no row data, no impersonation).
- Confront TD-012 honestly: state exactly what this change changes about the
  module's anonymity guarantees, and update the tech-debt entry rather than
  letting the two documents drift apart.

**Non-Goals:**

- Re-deriving or re-fetching the rows a stored turn once returned. Resume and
  the support-read screen show question/SQL/outcome/timestamps only; a table
  of data only ever comes from the explicit, own-history-only, table-only
  re-execution action.
- Sharing history via a public/anonymous link (explicitly rejected by the
  user).
- A "who accessed my history" surface for the subject (explicitly rejected;
  see Decision D11).
- Fixing TD-016 (shell has no responsive breakpoints). The new history
  surface inherits that pre-existing constraint; it does not need to repair
  it to ship.
- A general table-level "what did this SQL touch" extractor. Not needed here:
  the stored SQL is re-run verbatim, not analyzed.

## Decisions

### D1 — Two new tables, in the `asistente` schema, decoupled from the ephemeral in-memory thread

`asistente.hilo_historico` (one row per persisted conversation: `id uuid`
minted by the application, `actor_id uuid`, `titulo text`, `creado_en
timestamptz`, `ultima_actividad timestamptz`) and `asistente.turno_historico`
(one row per persisted turn: `id uuid`, `hilo_id uuid REFERENCES
hilo_historico(id) ON DELETE CASCADE`, `pregunta text`, `sql_resuelto text
NULL`, `estado text`, `ocurrido_en timestamptz`).

**`hilo_historico.id` is its own identity, independent of
`HiloConversacional.Id`.** The in-memory thread's id is disposable and
2-hours-lived by design (`VigenciaDelHiloMinutos`); the persisted
conversation has to survive 180 days and be resumable long after any
in-memory thread that once carried it has expired. Tying them 1:1 would mean
either (a) a persisted conversation dies with its in-memory thread — directly
contradicts "resume a past conversation" — or (b) resuming always starts a
_new_ persisted conversation, which reads as data loss in the sidebar
(the old item stops updating, a near-duplicate one appears). Decoupling them
avoids both.

The link between the two lives on the in-memory object: `HiloConversacional`
gains a settable `HiloHistorico : Guid?` (default `null`). The
persisted-history write (hooked in `CapaConversacional.RegistrarAsync`, see
D2) mints a `hilo_historico` row and sets `conversacion.HiloHistorico` the
first time a thread writes history; every later turn on the same in-memory
thread appends to that same persisted conversation instead of minting a new
one.

**Alternative rejected**: reusing `HiloConversacional.Id` as `hilo_historico.id`
directly. Simpler (one id, one map), but conflates two different lifetimes
that this feature's whole point is to decouple — see the resume mechanism in
D6, which depends on this separation.

### D2 — The write hook is `CapaConversacional.RegistrarAsync`, unconditional except for the four contract states

The same private method that already writes `IRegistroDelTurno` on every
exit path gains a call to a new `IRegistroDeHistorial.RegistrarTurnoAsync`,
**for every conversation, with no per-conversation opt-out**: the user
explicitly requires "user X asked Y" to be establishable, through the user's
own history and through audited support access (`asistente-acceso-de-soporte-al-historial`),
and a mode that skipped recording would directly contradict that requirement.
Unlike the existing registers, this write is **skipped entirely** for
`EstadoDelTurno.Fallo`: `Fallo` never
produces an HTTP response (the state mapping deliberately throws if asked to
name it), so there is nothing coherent to show on a resumed conversation for
it — recording it would need a client-facing state that does not exist. The
other four states (`Respondida`, `NoContestable`, `NecesitaAclaracion`,
`ServicioDegradado`) are exactly what the contract already exposes, so
resume/list can show precisely what the user saw at the time.

`sql_resuelto` is populated from `resultado.SqlEjecutado`, the same value
`HiloConversacional.Agregar` already receives — not from `turno.Sql`, which
is permission-gated for _display_ and does not exist as a concept for
storage: what gets stored is "the query that answered this," independent of
who is later allowed to see it. Visibility on read is a separate check (D9).

Like `IRegistroDelTurno`, this write must never fail the turn: same
try/catch-and-log discipline as `RegistroDelTurno`/`RegistroDeRetroalimentacion`.

### D3 — Resume mints a fresh ephemeral thread, seeded from the persisted turns

`POST /api/asistente/historial/{hiloHistoricoId}/reanudar` (ownership
checked: `hilo_historico.actor_id == actor`) reads the conversation's
persisted turns and calls a new `IAlmacenDeHilos` method — e.g. `Sembrar(Guid
actor, Guid hiloHistorico, IReadOnlyList<TurnoDelHilo> turnos)` — that creates
a brand-new `HiloConversacional` (fresh random ephemeral id, normal 120-minute
TTL), replays each turn via the existing `Agregar(pregunta, cuando,
sqlEjecutado)`, sets `HiloHistorico = hiloHistoricoId`, and stores it in the
in-memory map exactly like any other thread. The endpoint returns the new
ephemeral id (as `hilo`, the same field `POST /consultas` already round-trips)
plus the full turn list (for the UI to render immediately). The client then
keeps using that `hilo` for follow-up turns exactly as it always has; those
turns append to the _same_ persisted conversation because `HiloHistorico` is
already set (D1).

This needs one small, backward-compatible addition to `IAlmacenDeHilos`
(a new method, not a change to `Resolver`'s signature or behavior) and no
change to `HistorialVigente`/`ConsultasVigentes`, which already cap by
`TopeDeTurnosDelHistorial` regardless of how the list was populated.

**Alternative rejected**: reviving the old ephemeral id verbatim. Not
possible in general (it may be long expired, or — after 2 hours — always
will be for anything worth "resuming"), and would also require every other
piece of `IAlmacenDeHilos` to special-case an externally-supplied id instead
of one it minted.

### D4 — Table-only re-execution, never re-drafting

The task brief asked for a recommendation between two options: (a) table
only (no model call), or (b) table plus a re-drafted answer (model call,
counts against quota). **Recommendation: table only**, via an explicit
"volver a consultar" action (`POST /api/asistente/historial/turnos/{turnoId}/reejecutar`,
own history only, `turno.Estado == Respondida`, `sql_resuelto` not null):

- It mirrors Databricks Genie and Snowflake Cortex Analyst, both cited in the
  brief: the SQL text is the durable artifact, the _answer_ is not — the
  brief's own instruction to never persist the drafted answer text already
  commits to this position, since the alternative (re-drafting) is the only
  way to get an answer back at all once it stops being stored.
  A re-execution is cheap, bounded, and safe to invoke often; a re-draft is
  neither.
- It costs no model call, so it needs no quota/circuit-breaker interaction —
  consistent with how the feedback endpoint was scoped in
  `asistente-feedback-export-seguimiento` (no model spend, no quota gate).
- Re-running under RLS is a **feature, not a limitation**: if the actor's
  scope shrank since the question was asked (e.g. lost a course
  assignment), the table now correctly reflects what they can see today,
  never a cached snapshot of a wider scope they used to have.
- It reuses the existing pipeline pieces almost verbatim: `IEjecutorDeConsulta`
  (transaction-scoped to the current actor, same `LIMIT tope+1` probe-row
  discipline) and the masking/sensitivity classification the live carril
  already applies — no new masking logic to get right a second time.
- A SQL that no longer runs (schema drift, or the actor's current privileges
  reject it with `42501`) must resolve the same way the live carril already
  resolves an unexpected engine error: an abstention-shaped outcome, never a
  raw database error surfaced to the user.

This action does **not** write a new `turno_historico` row (it is not a new
question) and does **not** write to `registro_operativo`/`registro_analitico`
(no model involved, and counting it would inflate usage metrics with an
action that is not a "turn"). If usage ever needs its own visibility, a
lightweight counter can be added later without touching this design — flagged
as a risk, not an open question, in Risks/Trade-offs.

### D6 — Full-text search is server-side, over the actor's own `pregunta` only

`GET /api/asistente/historial?q=<texto>` searches `turno_historico.pregunta`
scoped to `hilo_historico.actor_id = <current actor>` via PostgreSQL's
built-in text search (`to_tsvector('spanish', pregunta)` with a matching GIN
index), not a client-side filter over a full dump and not a bare `ILIKE`.
`'spanish'` (not `'simple'`) because questions are Spanish prose and stemming
("designación"/"designaciones") is exactly what makes search useful instead
of literal.

**Alternative rejected**: `ILIKE '%term%'`. Works for now given the
institution's small user base and per-user conversation counts, but a
leading-wildcard `ILIKE` cannot use a b-tree index and does not do
stemming/accent folding — `to_tsvector` costs one GIN index and one
well-understood query shape, and is the standard tool for exactly this job.

### D7 — Retention: 180 days from last activity, purge extends `PurgaDeRegistros`

`OpcionesAsistente.RetencionDeHistorialDias` (default `180`) cuts
`hilo_historico` (and cascades `turno_historico`) by `ultima_actividad`, not
by each turn's own timestamp — a conversation resumed and added to nine
months later should not have its oldest turns pruned out from under it while
the conversation itself is still active; retention is a property of the
_conversation_, matching the brief's own framing ("from last activity").

This is deliberately **longer** than `RetencionDeRegistrosDias` (90, for the
anonymous registers) and shorter than nothing: 180 days balances "long enough
that resuming a conversation from a prior semester still works" against
Ley 25.326's proportionality principle (no numeric period is set by the law
itself, so the number has to be justified by finalidad — supporting a user's
own continued use of the tool — and not left unconstrained). `PurgaDeRegistros.PurgarAsync`
gains a third sweep (`DELETE FROM asistente.hilo_historico WHERE
ultima_actividad < @corte`, cascading), reusing the exact same
`TimeProvider`-driven, log-and-continue-on-failure shape already proven for
the other two registers — no second background service.

### D8 — History never stores, references, or derives the feedback token

`turno_historico` has no column that could resolve to
`registro_analitico.id`/`ClaveDeRetroalimentacion`, and no code path computes
one from the other. This is a explicit non-goal, not an oversight: linking
them would let anyone who can read a user's own (or, worse, another user's,
via support access) history immediately look up whether — and how — that
specific turn was rated, which is a second, much cheaper channel onto exactly
the anonymous analytic system `asistente-feedback-export-seguimiento`
designed the token _not_ to leak through. Nothing in this change's
requirements needs that link, so it is not built.

### D9 — Own-history SQL visibility follows the existing `asistente.ver_consulta` gate; support visibility does not add a second gate

On **own** history (list detail, resume), `sql_resuelto` is included in the
response only when the actor has `asistente.ver_consulta` — the exact same
switch that already gates `Sql` on the live turn contract, for the same
reason (a stored `WHERE` can carry a document number just as easily as a live
one).

On the **support** read (`asistente.leer_historial_ajeno`), SQL is always
included when the permission is held — no additional `asistente.ver_consulta`
requirement is layered on top. This is a deliberate, single-gate design:
`leer_historial_ajeno` is itself the diagnostic-access permission (support
cannot do its job of "figure out what went wrong" without seeing the query),
seeded to nobody and grantable per the same `/membresia-roles` administrative
path as any other permission in this module. Requiring _two_ permissions to
see the SQL in someone else's history would not add a real second boundary —
whoever is trusted to read another person's full question history is already
trusted with more than the SQL alone — it would only add friction to the
legitimate incident-support path this capability exists to serve.

### D10 — Support access: own permission, mandatory reason carried in the request body (not the URL), append-only audit, no re-execution, no row data

Two endpoints, both `POST` (not `GET`) specifically so the mandatory
free-text reason travels in the request body and never in a URL/query
string, where it could end up in an access log, a proxy log, or browser
history the way `GET` query parameters routinely do:

- `POST /api/asistente/soporte/historial/{actorId}/listar` — body `{ razon }`.
  Lists the subject's conversations (id, título, creado_en, ultima_actividad).
- `POST /api/asistente/soporte/historial/{actorId}/{hiloId}/leer` — body
  `{ razon }`. Returns one conversation's turns (question, SQL, outcome,
  timestamps).

Both require `asistente.leer_historial_ajeno`, reject an empty/whitespace
`razon` (mandatory, not merely encouraged), and write one row to
`asistente.auditoria_acceso_historial` (`lector_id`, `sujeto_id`,
`hilo_historico_id` — `null` for the list call, the specific id for the read
call — `razon`, `ocurrido_en`) **before** returning the data, so a failure to
audit fails the read rather than silently granting access unaudited. Neither
endpoint accepts re-execution or exposes row data — they are read-only over
`turno_historico`'s own columns, never a path into `IEjecutorDeConsulta`.

**No foreign key from the audit table to `hilo_historico`.** The audit
record has to survive the subject deleting their own history (D12's own
delete-all is a hard delete the user is entitled to; an audit trail that a
subject could sabotage by deleting the very data support looked at would not
be an audit trail). Keeping `hilo_historico_id` as a plain, unconstrained
`uuid` column means a later-deleted conversation still leaves a fully
intelligible audit row (who read what conversation of whose, when, why) —
the row does not need the conversation to still exist, since it never stores
the conversation's content, only its id.

### D11 — The subject is not shown who accessed their history (final decision, not reopened)

There is no "who viewed my history" endpoint, screen, or field. Trade-off,
stated explicitly:

- **Given up**: some deterrence and trust value a visible access log would
  provide (the three cited precedents — ChatGPT Enterprise Compliance API,
  Claude Enterprise audit logs, Microsoft Purview eDiscovery — all keep this
  kind of log admin-side, not subject-facing, for the same reason below).
- **Gained**: support staff are not individually exposed to the person whose
  data they were asked to look into (a subject-visible log turns every
  legitimate incident lookup into a named encounter the reader has to
  anticipate being seen for), and an active investigation is not tipped off
  mid-flight by the subject noticing they were looked at. Both properties
  matter specifically because the audience for this feature is support
  handling a live complaint or investigation, not a general transparency
  dashboard.

This is not listed as an open question: it is the user's explicit, final
decision for this change.

### D12 — TD-012's anonymity guarantee now has a documented dependency, not a reopening

TD-012 protects `asistente.registro_analitico` against being re-linked to an
actor by anyone who can query it _plus_ `registro_operativo` (or, residually,
`ctid` order). Nothing in this change touches that mechanism: `hilo_historico`/
`turno_historico` are a wholly separate write path, never cross-referenced
with the analytic register (D8), and denied to the exact same two read-only
roles the analytic register is already denied to — the assistant's own SQL
generation still cannot answer "what did fulano ask," because it still cannot
read either table.

What _does_ change: this feature deliberately builds, in the same
schema, a table that answers exactly the question TD-012 refuses to let the
analytic register answer — but attributed on purpose, for its owner, and for
an audited support reader. **The analytic register's anonymity now holds only
against a reader who additionally lacks history access** (the assistant's own
roles, and everyone else who isn't the owner or a permissioned, audited
support reader). This was already implicitly true — anyone with the owner
database connection could always read both tables directly — but this change
makes reading the actor↔question link a designed, permissioned, in-application
capability for the first time, so TD-012's text should say so plainly instead
of describing a boundary that quietly grew a documented door in it. Task: add
a short cross-reference from TD-012 to this change and to `BR-asistente-006`
(the new permission's rule) — not a rewrite of TD-012's existing content,
which remains accurate for what it always described (the _analytic_ register
alone).

**The `ConnectionId`/`RequestId` Serilog log-context channel, re-evaluated now
that there is no temporary mode.** D3 of `asistente-feedback-export-seguimiento`
closed the "same structured log _event_" channel (a turn's log entry never
names the actor and the feedback token together). It did not — and could
not, by its own scope — close a _same-connection_ channel:
`UseSerilogRequestLogging()` plus `Enrich.FromLogContext()`
(`ArsDocendi.Host/Program.cs`) means Kestrel's `ConnectionId` (and, on a
structured sink, `RequestId`) is available to correlate two different
requests that reuse the same keep-alive HTTP connection, even across two
different log _events_.

With every conversation now going to history (no opt-out — D2), the actor↔question
link this channel could reconstruct **already exists as a designed,
in-application capability**: the owner sees it in their own history, and a
permissioned support reader sees it too, both through `asistente-acceso-de-soporte-al-historial`.
So this channel is no longer "a way to learn something the system otherwise
keeps secret" — it is a way to learn it **without holding the
`asistente.leer_historial_ajeno` permission and without leaving an audit
row**, i.e. a bypass of the access gate and the audit trail, not a bypass of
anonymity itself (there is none left to bypass for history; TD-012's
anonymous _analytic_ register is untouched and unaffected by this channel,
which is about request logs, not that table). That is still worth closing,
but it is a logging-configuration fix orthogonal to persisting conversations,
so it is recorded as a new tech-debt entry (tasks.md §15) rather than
undertaken in this change: mitigation is to strip `ConnectionId`/`RequestId`
from the turn's and the feedback endpoint's log events if a structured sink
is ever configured (today's default console sink does not expose them
anywhere a reader could correlate across requests).

## Risks / Trade-offs

- **[Risk]** A brand-new, actor-linked, 180-day table is a materially bigger
  privacy surface than anything the module has stored before. →
  Mitigated by: schema-level deny-by-default identical to the existing
  registers (no new `GRANT`, verified by a regression test); a single,
  narrow, seeded-to-nobody permission for any access beyond the owner;
  mandatory reason and append-only audit on every non-owner read; a hard
  178–180-day retention ceiling with the same purge discipline as the rest
  of the module; and the honest TD-012 update in D12 instead of silence.
- **[Risk]** Re-execution ("volver a consultar") is a request an actor can
  repeat cheaply against their own historical SQL. → Mitigated by: no model
  cost (nothing to exhaust there), the existing per-statement DB timeout
  (`TimeoutDeSentenciaMs`) already bounding any single execution, and the
  action being scoped to the actor's own historical, already-once-validated
  SQL (bounded cardinality: at most as many distinct queries as they have
  `Respondida` turns). If abuse is observed in practice, a lightweight
  rate limit can be added later without changing this design.
- **[Risk]** Decoupling `hilo_historico.id` from the ephemeral thread id (D1)
  adds one more identifier for the frontend to track (ephemeral `hilo` for
  live turns vs. persisted `hiloHistorico` for the sidebar). → Accepted: the
  alternative (D1's rejected option) trades this away for either data loss on
  resume or sidebar duplication, both worse for the user-facing behavior this
  change exists to deliver.
- **[Risk]** The support-read surface, however narrow, is still a new way for
  one person's typed questions to be read by another. → Mitigated by the
  full D10/D11 design (own permission, mandatory reason, append-only audit,
  no re-execution, no row data) and by D11's explicit trade-off: this is the
  same posture the three cited industry precedents ship with, not a weaker
  one.

## Migration Plan

Additive only, same discipline as every prior migration in this module: two
new SQL files (`004_asistente_historial.sql`, `005_asistente_auditoria_soporte.sql`),
no `ALTER`/`DROP`/`RENAME` of anything existing, new tables denied to both
read-only roles in the same file that creates them (mirroring
`002_asistente_registros.sql`'s "revoke right after create, in the same
transaction" pattern for a schema-level deny that already exists — these two
tables inherit it, so no new `REVOKE` statement is even needed, only the
manifest/test _confirmation_ that inheritance holds). One new `identity`
migration seeds the new permission, following `014_identity_permiso_ver_consulta.sql`
verbatim in structure. `ConsultaDelAsistente`'s contract is unchanged by this
migration — persisting history requires no new request field, since every
conversation is recorded. `IAlmacenDeHilos` gains one new method (`Sembrar`) — backward compatible,
existing `Resolver` callers are untouched. No rollback concerns beyond "new
tables/column, drop if truly needed"; nothing existing is renamed, dropped,
or retyped, so `ArquitecturaAsistenteTests`'s DDL-shape guard needs no
exemption beyond the allowlist additions already called out in D-decisions
above.

## Open Questions

None. Every ambiguity the task brief raised (re-execution table-vs-redraft,
subject transparency, retention windows, TD-012 handling) is resolved above
as a decision, not deferred.
