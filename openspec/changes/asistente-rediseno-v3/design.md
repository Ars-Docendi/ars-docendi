## Context

See proposal.md for motivation. The relevant current state:

- **Two mounts.** `LanzadorAsistente` (top-bar modal, 880 px) and `AsistentePage`
  (`/asistente`) each own a `useAsistente` + `useHistorialAsistente` pair and render the
  same `PanelAsistente`. History is an overlay drawer (`ListaDeConversaciones`) opened by
  `AbrirHistorial` in each mount's header. There is no `/asistente` item in `nav.ts`; the
  route is reachable by URL and by the launcher's absence of alternatives.
- **History backend.** `asistente.hilo_historico` / `turno_historico`
  (`database/asistente/004_asistente_historial.sql`) are written with the owner
  connection (`CadenaDuena`) by `RegistroDeHistorial` and read by `ConsultasDeHistorial`;
  `DELETE` is immediate and cascades. `PurgaDeRegistros`, run by the `ServicioDePurga`
  `BackgroundService` every `PeriodoDePurgaHoras` (default 24 h), enforces the 180-day
  retention. The `asistente` schema is revoked wholesale from `asistente_ro` and
  `asistente_ro_pii` (002), so new columns inherit the denial.
- **Suggestions.** `Sugerencias.Para` (refusals, lexical match with generic fallback),
  `Sugerencias.ParaCategoria` + `SugerenciasDeSeguimiento` (answers, EXPLAIN-filtered),
  and the meta-question passing `capacidades.Ejemplos` to `FabricasDelResultado.SinDatos`.
  `RunnerSocial` requires suggestions on no-contestable items.
- **Feedback.** Closed reason set in `RazonesDeRetroalimentacion` mirrored by the
  `retroalimentacion_turno_razon_valida` CHECK (003); tokens live in memory
  (`ValidezDeRetroalimentacionEnMemoria`, 120 min).
- **Turn pipeline.** `CapaConversacional.ResponderAsync` takes the per-actor lock first,
  resolves the thread (`IAlmacenDeHilos`), resolves the turn, registers it
  (`RegistroDelTurno` + `RegistroDeHistorial`), charges quota in `finally`. The thread
  (`HiloConversacional`) stores each turn's interpreted question and executed SQL, which
  are carried into follow-ups (`ConsultasVigentes`). The SQL generator puts everything
  turn-specific in the user message (`GeneradorDeSql.ArmarMensaje`), never in the cached
  prefix.
- **Scope functions.** `identity.asistente_materias_visibles()` (SECURITY DEFINER,
  granted to both assistant roles) and the RLS policies on `designaciones.*` scope what
  the assistant actor sees, once `PreambuloDelActor` fixes the actor for the transaction.
  `identity.materias`, `identity.carreras`, `identity.personas` (`id`, `legajo`, `nombre`, `apellido`) and `designaciones.cargos` are granted to the basic role; `materias` and
  `personas` have no RLS.

### Dependency on in-flight changes

None of the ten modified capabilities exists in `openspec/specs/` yet; each is defined by
an un-archived change. `openspec validate --strict` passes (it checks delta syntax), and
reports as INFO that archive would refuse the MODIFIED deltas until their targets exist.
Therefore **this change MUST be archived after** these changes, in this order where they
overlap:

| Capability                                                   | Defined / last changed by                                                                             |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| `asistente-superficie-frontend`, `asistente-accesibilidad`   | `asistente-frontend` → `asistente-historial-conversaciones` → `asistente-feedback-export-seguimiento` |
| `asistente-conversacion`                                     | `asistente-rediseno-conversacion` → `asistente-razonamiento-solo-en-debug`                            |
| `asistente-historial-conversaciones`, `…-acceso-de-soporte…` | `asistente-historial-conversaciones`                                                                  |
| `asistente-contrato-de-respuesta`                            | `asistente-superficie-api` → `asistente-feedback-export-seguimiento`                                  |
| `asistente-retroalimentacion`, `asistente-exportacion-csv`   | `asistente-feedback-export-seguimiento`                                                               |
| `asistente-abstencion`                                       | `asistente-carril-sql` → `asistente-presupuesto-degradacion`                                          |
| `asistente-eje-social`                                       | `asistente-ejes-de-evaluacion`                                                                        |

Requirements this change only relies on (not modified): `asistente-cupo-visible` and
`asistente-turno-exclusivo-del-actor` (`asistente-administracion-de-uso`), and the
debug-only reasoning requirement (`asistente-razonamiento-solo-en-debug`). All these
changes are already implemented in code on this branch; only their archive is pending, so
there is no implementation-order dependency, only an archive-order one.

## Goals / Non-Goals

**Goals:**

- One mount, one owner of the conversation state, one history surface (the rail).
- Deferred deletion whose finality does not depend on the browser.
- Structured mentions that make the SQL lane filter exactly without widening what the
  model provider learns.
- A smaller response contract (no `sugerencias`) and an evaluation that measures what the
  contract now promises.

**Non-Goals:**

- Narrow/mobile layouts (TD-016, new TD-024).
- Question versions («N / M»), per-reason refusal templates (ARS-139).
- The deterministic intent lane consuming references (it runs in shadow mode; the SQL
  lane is where the answer comes from).
- Changing the privilege or sensitivity manifests, or any other module.

## Decisions

### D1. Single mount; the launcher owns everything

`LanzadorAsistente` keeps owning `useAsistente` and `useHistorialAsistente`; `AsistentePage`
is deleted and the `index` child of `features/asistente/routes.tsx` becomes a redirect
(D8). `PanelAsistente` becomes the v3 layout: `RailDeConversaciones` (new) + main column
(header, thread, composer, status strip). `AbrirHistorial` and the overlay
`ListaDeConversaciones` are removed; their list/rename/menu logic moves into the rail.
The modal renders its own 56 px header inside the body; the dialog keeps the constant
accessible name «Asistente» (the library `Modal` still has no header-actions slot —
TD-020 unchanged — so the library title is kept visually hidden rather than re-creating
the `h4` naming bug TD-020 documents). Alternative considered: keep the page as a thin
wrapper of the modal — rejected by the PO.

### D2. Rail preference in `localStorage`, keyed by user

`asistente.rail.<userId>` = `colapsado` | `expandido`, read/written inside `try/catch`,
default expanded. It is a UI preference, not conversation data, so it does not conflict
with "nothing of the conversation is stored in the browser", which the modified
requirement now states precisely.

### D3. Archive is a nullable timestamp; list returns everything with a flag

`hilo_historico.archivada_en timestamptz NULL`. `GET /historial` returns all
non-pending conversations with `archivada: bool`; the client splits active rows from
the «Archivadas» section, and search results keep the flag. Endpoints:
`POST /historial/{id}/archivar` and `POST /historial/{id}/desarchivar` → `204`, `404` for
unknown/foreign/pending (same body). Archiving does not touch `ultima_actividad`, so
retention (D6 of the history change) is unchanged; `RegistroDeHistorial` clears
`archivada_en` when it appends a turn (new activity unarchives). Archiving or deleting
the **active** conversation resets the live thread to the welcome screen, as the mock
does; «Deshacer» re-resumes it.

### D4. Deferred deletion: mark + batch id + read-time filter + one-minute sweep

- Columns `borrado_pendiente_desde timestamptz NULL` and `lote_de_borrado uuid NULL` on
  `hilo_historico`, partial index on `borrado_pendiente_desde WHERE NOT NULL`.
- `DELETE /historial/{id}` and `DELETE /historial` mark the rows (the latter: every
  non-pending conversation of the actor, archived included) with `now()` and a fresh batch
  id, and return `200 { "loteDeBorrado": "<uuid>" }`.
- `POST /historial/borrados/{lote}/deshacer` clears the mark for rows of that batch owned
  by the actor whose mark is younger than the server window → `204`; otherwise `404`
  (unknown, foreign or expired are indistinguishable).
- **Server window = 15 s** (`VentanaDeDeshacerSegundos`, default 15): the 10 s the toast
  shows plus 5 s of grace so a click at 9.9 s over a slow network still succeeds. The
  toast timer starts when the `DELETE` response arrives.
- **Logical finality at the window, everywhere.** Every owner query filters
  `borrado_pendiente_desde IS NULL`; support queries filter
  `borrado_pendiente_desde IS NULL OR borrado_pendiente_desde > now() - window` and
  expose `pendienteDeBorrado`. The live turn registration never writes into a pending
  conversation: if the thread's `HiloHistorico` is pending, the turn mints a new
  conversation.
- **Physical purge server-side, ≤ window + 60 s.** A new `BarridoDeBorradosPendientes`
  `BackgroundService`, same pattern as `ServicioDePurga` (own scope per tick, logs and
  survives failures), runs a single `DELETE` of the rows whose `borrado_pendiente_desde` is older than the window every `PeriodoDeBarridoDeBorradosSegundos`
  (default 60); the cascade removes turns. The same statement is added to
  `PurgaDeRegistros` as a backstop. Idempotent, so safe on several instances.
- Alternatives: client-delayed `DELETE` (rejected: dies with the tab, violating the PO
  requirement); relying only on the 24 h purge (rejected: a deleted conversation would
  physically survive up to a day); `pg_cron` (rejected, same reasoning as
  `PurgaDeRegistros`' header).

### D5. Result table sort and expand are pure client state

Sort state `{ columna, direccion }` lives per turn in the conversation owner (so the
inline and expanded views share it and it resets on replacement). The sorter works on an
array of original row indexes, which keeps `vinculos` (keyed `fila:columna` by original
index) correct and lets CSV/TSV export the displayed order. Column type is inferred per
column from the displayed values (`formatearCelda`): all non-empty numbers → numeric; all
non-empty ISO `yyyy-mm-dd[Thh:mm…]` → date; otherwise a Spanish `Intl.Collator` (numeric, base sensitivity). The expanded view is an absolutely positioned layer over the
modal body (inside the dialog, so `inert` on `#root` still contains focus); Escape is
captured in the capture phase with `stopPropagation`, the same technique
`useHistorialAsistente` already uses to keep Escape from closing the modal.

### D6. Action bar visibility

Controls are always in the DOM and in tab order; visibility is opacity-only, driven by
`:hover`/`:focus-within` on the turn, and forced visible on the last turn and on voted
turns. This implements the mock's quiet toolbar while honoring the existing anti-pattern
"never hide actions only behind hover".

### D7. Feedback reasons, now a list, plus a bounded free-text comment

**PO-changed (2026-09-26): `lento` is removed entirely, not kept as legacy.** Nothing has
shipped to production — the feedback table never reached `develop` — so there is no
existing row to preserve. `RazonesDeRetroalimentacion.Todas` is exactly
`datos_incorrectos`, `no_entendio_la_pregunta`, `faltan_datos`, `otro`; `lento` never
appears anywhere in code, SQL comments, tests or docs.

**PO-changed (2026-09-26): the 👎 panel now takes zero or more reasons plus a free-text
comment**, matching the mock (toggle pills, `aria-pressed`, «Contanos qué esperabas
ver…», «Omitir» / «Enviar comentario»). The column shape changes with it:

- `razon text` is retired in favor of `razones text[]` (zero or more of the four values,
  no duplicates — validated by the controller, not by the CHECK) and `comentario text`
  (trimmed server- and client-side, empty ⇒ `null`, max 500 characters, enforced by a
  CHECK and by the controller). A thumbs-up still ignores whatever the client sends in
  either field, server-side, same rule as before.
- **Fresh bases** get the final shape directly from the `CREATE`: no `razon` column at
  all, `razones` with `CONSTRAINT retroalimentacion_turno_razones_validas CHECK (razones
IS NULL OR razones <@ ARRAY[the four values]::text[])`, `comentario` with `CONSTRAINT
retroalimentacion_turno_comentario_longitud CHECK (comentario IS NULL OR
char_length(comentario) <= 500)`.
- **Old dev bases** (the one that matters: `arsdocendi_pr_140`, which already has the
  single-`razon` shape from before this PO round) cannot be altered destructively
  (`ArquitecturaAsistenteTests`' no-`DROP` rule). 003 adds `razones` and `comentario` via
  two `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` statements (one column each, no inline
  CHECK — a CHECK naming the four-value array literal has commas, which the allowed-form
  regex rejects), then a guarded `DO` block per constraint that adds it only when missing
  by name. Both names are new ratified entries in `ArquitecturaAsistenteTests.ReemplazosDeCheckRatificados`
  (a bare `ADD CONSTRAINT`, with no matching `DROP`, still needs ratification: the
  detector's allowed form is only `ADD COLUMN IF NOT EXISTS`, so any other `ALTER TABLE`
  — including a fresh `ADD CONSTRAINT` — is destructive unless listed by name). The old
  `razon` column and its original CHECK are left exactly as they are: nothing reads or
  writes them anymore, so there is nothing to converge. This is the "old base" case
  `MigracionDelAsistenteTests` exercises explicitly.
- The former `retroalimentacion_turno_razon_valida` ratification entry is removed: no
  statement in 003 touches `razon` or that constraint anymore, so there is nothing left to
  ratify.

Alternative considered: keep `razon` as a single value and add a second nullable column
for "extra reasons" — rejected, it is the same list wearing a disguise and would need its
own uniqueness rule anyway. Alternative for the comment: unlimited length — rejected, a
free-text field next to an otherwise-anonymous row is exactly the re-identification risk
TD-012 exists to bound; 500 characters plus the "no incluyas datos personales" hint is the
existing risk-acceptance pattern, made explicit instead of avoided (see TD-012 addendum in
`docs/quality/tech-debt.md`).

### D8. `/asistente` redirect

`routes.tsx` index → `<Navigate to="/portal?asistente=abrir" replace />` (`/portal` is the
target of the app's own index redirect, i.e. the home). `LanzadorAsistente` reads the
`asistente=abrir` search param: with access it opens the modal and replaces the URL
without the param; without access it only strips the param (the launcher renders
nothing, so the hook that strips it lives in the component before the early return).
The "close on navigation" rule compares pathnames, so the redirect does not immediately
close the modal it opens. Alternative: router `state` — rejected because the app index
redirect drops it.

### D9. Edit last question: replacement target, not "last by position"

`ConsultaDelAsistente` gains `Reemplaza?: string` — the client id of the turn being
replaced, which is its Idempotency-Key for live turns and the `turno_historico.id` for
turns restored by «Reanudar». Each `TurnoDelHilo` gains `ClaveDelCliente` and
`TurnoHistoricoId` so the thread can check the target. `CapaConversacional`:

1. After the lock, if `Reemplaza` is set, require a live thread whose last turn matches;
   otherwise `409` (new `ReemplazoInvalido` exception mapped by the controller).
2. Resolve the new question against a **view** of the thread without its last turn
   (segment start and pending clarification snapshotted before that turn, kept on the
   turn itself), so the context is the one the replaced question had.
3. Only when the turn produces a registrable outcome: swap the thread's last turn,
   revoke the old feedback token (`IValidezDeRetroalimentacion.Revocar`), and in
   `RegistroDeHistorial` delete the old `turno_historico` row and insert the new one in
   one transaction (touching `ultima_actividad`, not the title).
4. Lock rejection, `409`, transport failure or `Fallo` change nothing, so a retry with
   the same key and target is safe; idempotency returns the cached result if the first
   attempt completed.

Quota and lock need no new code: a resend is a normal `POST /consultas`. The replaced
turn's analytic row and any vote stay (anonymous, TD-012). Alternative: "replace
whatever is last" — rejected: after a transport error the client cannot know whether
its turn reached the server, and it could silently replace the previous, good turn.

### D10. Mentions: search endpoint and scoping (AGENTS.md rule 11)

`GET /api/asistente/menciones?tipo=materia|docente&q=…` (`asistente.consultar`) →
`{ resultados: [...], hayMas: bool }`, max 6, `q` 2–100 chars (else `400`). It runs on
`AperturaDeLectura` basic role with `PreambuloDelActor` (read-only transaction, actor
fixed), so every boundary is the engine's:

- **Subjects**: `identity.materias ⋈ identity.carreras` filtered by
  `m.id IN (SELECT identity.asistente_materias_visibles())` and `m.is_active`, matched by
  normalized prefix on words of the name or on the code. One result per materia row
  (name, career name, code), so a subject taught in two careers appears twice with its
  career — this is what disambiguates homonyms. (The mock grouped careers as tags of a
  single row; a single row cannot map to one exact id.)
- **Teachers**: `identity.personas` that have at least one row in
  `designaciones.designaciones` visible under its RLS (which already conjoins
  `designaciones.ver` and the visible-materias scope), with the cargo of their most
  recent visible designation. `nombre`/`apellido` are basic-role columns classified
  `publica`.

No GRANT, RLS policy or manifest changes. Rule 11's falsifiability is kept by adding
tests next to `RlsAlcanceTests`/`PrivilegiosLecturaTests`: a career-scoped actor does not
find foreign subjects or teachers; an actor without `designaciones.ver` finds no teachers;
the endpoint never returns a column classified `sensible-*`. **Sensitivity manifest
review**: the search reads only `publica` columns plus `id` (`identificador`). Returning
`id` to the owner's browser is outside what that classification governs (the SQL lane's
result path to the caller and the model); the id still never reaches the model (D11). The
manifest is unchanged; the domain doc records this reasoning.

### D11. Mentions: contract and SQL-lane integration via bound markers

- `ConsultaDelAsistente` gains `Referencias?: [{ tipo: "materia"|"docente", id: uuid }]`,
  max 5. The controller re-validates each id with the D10 query by id; any miss → one
  `400` ("Una de las menciones ya no está disponible. Volvé a elegirla."), before the lock
  and the pipeline (no quota, no history).
- `GeneradorDeSql.ArmarMensaje` gets a "Menciones" block: for each reference its display
  data (subject name + career, or teacher name) and a marker `$ref1`, `$ref2`…, with the
  instruction to filter `materias.id` / `personas.id` (or the matching FK) **by the
  marker, never by name**. Markers are per segment and numbered after those already in
  the thread. The prefix is untouched (cache and cassette fingerprints unaffected).
- `ValidadorDeSql`/`TokenizadorSql` accept `$refN` tokens only if declared for the turn;
  an undeclared marker or a declared-but-unused one is a validator rejection (so, per the
  existing abstention rule, not answerable without regeneration).
- `EjecutorDeConsulta` rewrites `$refN` to positional parameters and binds `uuid`s via
  Npgsql. The model sees markers only; the thread stores SQL with markers plus the
  bindings (`TurnoDelHilo.Referencias`), so carried-over queries never leak ids.
  `turno_historico` gets `referencias jsonb NULL` (marker → tipo, id) so «Volver a
  consultar» and «Reanudar» bind them again; RLS re-scopes at execution as today.
- `DetectorDeAmbiguedad` skips the terms covered by a reference's display text.
- **History chips (PO-changed 2026-09-26, row 15):** `GET /historial/{hiloId}` and
  `POST /historial/{hiloId}/reanudar` add `menciones: [{ tipo, id, etiqueta }]` to each
  turn, built by `TurnoDeHistorialDto` re-resolving `turno_historico.referencias`
  through `IBuscadorDeMenciones.ResolverAsync` for the actor reading NOW (own history:
  the caller; never the actor who originally asked). A reference outside the current
  reader's scope is omitted from the list — its question text stays plain — the same
  never-leak, never-crash rule D11 already applies to «Volver a consultar». `etiqueta`
  is the exact composer text (`"@Nombre"`/`"#Nombre"`, `textoDeLaMencion`'s format), so
  the frontend's existing `ubicarMenciones` (already used for «Editar y reenviar»)
  places the chip in the question text without the backend computing positions; if the
  entity's name changed since the question was asked, the text no longer matches and
  the mention silently reverts to plain text, same as deleting the mention text today.
  `SoporteHistorialController.Leer` does NOT gain this field: the reader there is the
  support agent, never the subject whose scope must decide visibility, and that
  endpoint already limits itself to text and moments with no interactive affordance.
  The field is nullable and `[JsonIgnore(Condition = WhenWritingNull)]` so it is
  entirely absent from the support response instead of an always-empty array.
- **Persisting references independent of the SQL lane (fixed 2026-09-26, gap found before
  commit):** `turno_historico.referencias` was only ever written from
  `ResultadoDelTurno.ReferenciasEjecutadas`, which only `CarrilSql`'s fully-resolved
  success path sets. A mention turn that ended in refusal, a clarification menu,
  degradation, or a social/meta reply never enters that path — `sql_resuelto` stays
  `null`, and so did `referencias`, so it resumed as plain text and «Editar y reenviar»
  lost the reference — exactly the most common edit case (people edit after a refusal).
  `CapaConversacional.RegistrarAsync` now falls back: when `ReferenciasEjecutadas` is
  null or empty, it numbers the request's own declared-and-validated `mencionesNuevas`
  with `MarcadoresDeReferencias.Asignar` (no `consultasAnteriores`, since there is no SQL
  segment to continue) and persists that instead. `sql_resuelto` stays `null` either way
  — only `referencias` gains the fallback. The live in-memory thread
  (`TurnoDelHilo`/`HiloConversacional.ReferenciasVigentes`) is untouched: only the SQL
  lane ever adds a `TurnoDelHilo`, so this fallback never affects follow-up inheritance
  inside a segment. «Volver a consultar» is unaffected — it already gates on
  `SqlResuelto is null` before ever looking at `Referencias`.
- Alternatives: literal UUIDs in the prompt (simplest, but sends a stable pseudonymous
  identifier of a person to the provider on every question about them, linkable across
  users, against the spirit of `identificador` and TD-022); natural keys (materia code is
  unique only per career; `legajo` is nullable for new staff).

### D12. Removing suggestions

Delete `ISugerenciasDeSeguimiento`, `SugerenciasDeSeguimiento`, `Sugerencias` (both
functions), the `sugerencias` parameter of `FabricasDelResultado.SinDatos`,
`ResultadoDelTurno.Sugerencias`, `RespuestaDelAsistente.Sugerencias`, the TS field and
`Sugerencias.tsx`. `CarrilSql` loses the post-answer call (and its EXPLAIN connection per
answered turn). `RunnerSocial.EvaluarNoContestable` passes on abstention alone;
`social.json`'s description and `backend/eval/README.md` are updated; integration tests
asserting suggestions are rewritten to assert their absence. `EstadoInicial`/capabilities
examples are untouched. ARS-139 (backlog) was already amended in Linear to ship without
chips.

### D13. Response names its conversation

`RespuestaDelAsistente.Conversacion: Guid?` = `HiloConversacional.HiloHistorico` after
registration (null if the history write failed or the turn was not recorded). The client
uses it to highlight the rail row and title the header, and invalidates the history
query after each turn. It is never logged nor written next to the feedback token or the
analytic id (TD-012 / D8 of the history change); the owner already holds both.

### D14. Existing states in v3

«Consultando…» moves visually into the pending turn (dots, `aria-hidden`), while the
existing threshold-gated `role="status"` announcer stays outside the log, so the
live-region requirements hold. «Dejar de esperar» moves into the composer's button slot.
Quota indicator and blocked text stay in a strip under the composer, visible to
everyone. **PO-changed (2026-09-26, row 14):** the metrics line joins them in the same
strip, but only in frontend debug mode (`VITE_ASISTENTE_DEBUG`) — the same switch
`asistente-razonamiento-solo-en-debug` uses for «Cómo lo interpreté» — instead of
unconditionally, superseding the author's original guess. `FranjaDeEstado`/
`LineaDeMetricas` take a `debug` prop defaulting to `modoDebugAsistente`, the same
pattern `Mensaje` already uses. The user bubble changes from accent to the neutral v3
bubble. Degraded/clarification keep
their `InlineAlert` severities. **PO-changed (2026-09-26): the 👎 panel now follows the
mock exactly** — multiple reason pills (`aria-pressed` each, independently toggled, no
mutual exclusion) plus the textarea («Contanos qué esperabas ver…», `maxLength=500`, a
visible counter, and the hint «No incluyas datos personales.» underneath); the submit
button is «Enviar comentario» (the mock's label), «Omitir» stays for skipping. The
comment is never logged, never sent to the model, and has no read surface anywhere in
the UI (no admin screen) — see D7 and the TD-012 addendum.

### D15. Migrations follow the module's in-file convention

The module's migrator re-applies each embedded SQL idempotently; its headers (002, 004, 006) prescribe adding columns in the same file as `ALTER TABLE … ADD COLUMN IF NOT
EXISTS` next to the `CREATE`. 004 gains the three `hilo_historico` columns, the index and
`turno_historico.referencias`; 003 gains the D7 constraint swap. No new numbered file is
needed, and the change stays versioned SQL (rule "no manual DB edits").

## Decisions taken by the author, pending PO confirmation

| #   | Open point                                    | Decision                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| --- | --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | What support sees during the window           | Visible, marked «Pendiente de borrado», until the 15 s server window expires; then invisible and purged within a minute (D4). Support reads stay audited.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 2   | «Borrar todas»                                | Keeps the inline confirmation (copy now mentions archived and the 10 s undo) **and** gets the undo window; single delete loses its confirmation.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 3   | CSV order                                     | Displayed (sorted) order, in inline and expanded views, consistent with "export exactly what is displayed".                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 4   | Existing `lento` votes                        | **PO-changed (2026-09-26):** `lento` is removed entirely, superseding the author's original guess (kept untouched, aged out by purge). Nothing has shipped to production (the feedback table never reached `develop`), so there is no legacy to preserve. Every "legacy `lento`" provision (code, SQL comments, tests, docs) is deleted rather than kept; the reason set is exactly `datos_incorrectos`, `no_entendio_la_pregunta`, `faltan_datos`, `otro` (D7).                                                                                                                                                                                                                              |
| 5   | Redirect target                               | `/asistente` → `/portal?asistente=abrir`, which opens the modal and strips the marker; without access, just the home (D8).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| 6   | Server-side purge                             | Mark + batch id; 15 s server window (10 s toast + 5 s grace); read-time filtering; one-minute `BackgroundService` sweep plus the daily purge as backstop (D4).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 7   | Mentions contract and SQL lane                | `referencias: [{ tipo, id }]` (max 5) re-validated server-side; search `GET /api/asistente/menciones`; scoping by `asistente_materias_visibles()` and designaciones RLS on the read-only role; bound `$refN` markers so ids never reach the model (D10, D11). **Inherited-mention revalidation**: a marker a turn reuses without asking again — «Volver a consultar» on an own turn, or the first turn after «Reanudar» — is re-validated against the actor's CURRENT scope with the same lookup, never the scope it had when first asked; a scope that shrank since resolves as any SQL that no longer runs (a friendly abstention), never a crash or an execution against the wrong entity. |
| 8   | Free-text comment in 👎 panel                 | **PO-changed (2026-09-26):** implemented after all, matching the mock — multiple reasons (toggle pills) plus a free-text comment, «Omitir» / «Enviar comentario» (D7, D14). Supersedes the author's original "not implemented, single-choice" guess.                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| 9   | Date groups in the rail                       | Keep the existing Hoy / Ayer / Últimos 7 días / Anteriores (the mock shows only two because of its sample data).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| 10  | Search in the rail                            | Kept (not drawn in v3) because archived conversations must stay findable.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| 11  | Archive/delete of the active conversation     | Resets to the welcome screen (as in the mock); «Deshacer» re-resumes it; a new turn in an archived conversation unarchives it.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 12  | Editable last question                        | Also a question restored by «Reanudar»; a replacement never changes the conversation title.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| 13  | Mention results per career                    | One row per subject-and-career instead of one row with several career tags (exact id per row).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| 14  | Metrics line                                  | **PO-changed (2026-09-26):** shown only in frontend debug mode (`VITE_ASISTENTE_DEBUG`) — the same switch `asistente-razonamiento-solo-en-debug` uses for «Cómo lo interpreté» — next to the quota indicator, which stays visible for everyone. Supersedes the author's original guess that it stayed unconditionally in the strip (D14).                                                                                                                                                                                                                                                                                                                                                     |
| 15  | Resumed/history mentions render as plain text | **PO-changed (2026-09-26):** implemented after all. Each history turn exposes `menciones: [{ tipo, id, etiqueta }]`, built by re-resolving `turno_historico.referencias` for the actor reading NOW via `IBuscadorDeMenciones.ResolverAsync` — never the actor who asked; a reference outside the current scope is omitted, never leaked. `etiqueta` is the exact composer text (`"@Nombre"`/`"#Nombre"`), so the frontend's existing `ubicarMenciones` places the chip without the backend computing positions. `SoporteHistorialController` does NOT gain this field (D11). Supersedes the author's original "known limitation, not covered" guess.                                          |

## Risks / Trade-offs

- [The model ignores or misuses a marker] → treated as a validator rejection (abstains,
  never answers about the wrong entity); evaluation gets mention items; watch the
  abstention rate after release.
- [Marker plumbing touches validator, executor, thread and history] → isolated behind
  one type (`ReferenciaDeConsulta`) and covered by unit tests of tokenizer/validator and
  an integration test "the provider never receives the uuid" using the recorded provider.
- [15 s server window vs 10 s toast] → a deletion is final up to 5 s after the toast
  closes; the toast copy says 10 s, so the user never loses an undo they were shown.
- [A deleted conversation lingers physically up to window + 60 s] → invisible everywhere
  from the window's end; accepted.
- [Removing `sugerencias` is a contract break] → only the in-repo frontend consumes it;
  both ship together.
- [Mobile users lose the assistant] → accepted by the PO; TD-024 records it next to
  TD-016.
- [Two background services] → same pattern, same failure isolation; the sweep is one
  indexed DELETE.
- [Free text in `comentario` can carry identifying content in an otherwise anonymous row]
  → 500-character cap, trimmed, the "no incluyas datos personales" hint, the same 90-day
  purge as the vote, and no read surface anywhere in the UI (TD-012 addendum in
  `docs/quality/tech-debt.md`).

## Migration Plan

1. Deploy backend and frontend together (the response and delete contracts change).
   The SQL in 003/004 is additive and idempotent; the migrator applies it at start.
2. No data backfill: existing conversations get `archivada_en = NULL`, no pending marks;
   no `retroalimentacion_turno` row exists yet on any real base (nothing shipped), so
   there is nothing to migrate for `razones`/`comentario` beyond adding the columns.
3. Rollback: before redeploying the previous version, run the sweep's DELETE once
   (`DELETE FROM asistente.hilo_historico WHERE borrado_pendiente_desde IS NOT NULL`) so
   the old code, which ignores the marks, does not resurface conversations users deleted.
   The extra `hilo_historico`/`turno_historico` columns and the new `retroalimentacion_turno`
   columns/CHECKs are additive and harmless to the old code, which never reads or writes
   them.

## Open Questions

- Whether support should also see _who_ archived (always the owner today) — not needed
  by any current flow.
