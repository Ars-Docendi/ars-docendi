## Context

See proposal.md for motivation. The relevant current state:

- **Three screens, two features.** `features/sistema/routes.tsx` mounts `/sistema`
  (`sistema.estado.ver` → `DashboardPage`) and `/auditoria` (`auditoria.ver` →
  `AuditoriaPage`); `features/asistente/routes.tsx` mounts `/asistente/administracion`
  (`asistente.administrar` → `AdministracionAsistentePage`, with its own `PageHeader`
  «Uso del asistente»). `app/shell/nav.ts` lists «Dashboard del sistema» and «Registros de
  auditoría» under «Sistema» and «Uso del asistente» under «Configuración». `NavItem` and
  `RequirePermission` accept exactly one permission. All three permissions are seeded only
  to `sys_admin`, but nothing ties them together.
- **Feature isolation is enforced.** Features never import each other, and ESLint forbids
  `app/**` from reaching inside a feature (only `features/<x>` or `features/<x>/routes`).
  `features/asistente/index.ts` already exports more than `routes` for the same reason.
- **Health.** `sistemaApi.consultarEstadoSistema` pings `/api/{aulas,tareas,designaciones,
portal}/ping` (5000 ms timeout, `performance.now()` latency) and calls
  `GET /api/administracion/sistema/estado` (`SELECT 1`, 3 s, returns
  `EstadoBaseDatosDto { estado, comprobadoEn, duracionMs }`). `GET /api/asistente/ping` is
  anonymous. Maintenance lives in `asistente.modo_mantenimiento` behind the module-internal
  `IDisponibilidadDelModulo.ConsultarAsync → EstadoDeMantenimiento(Activo, Razon)`; the only
  public read is `GET /api/asistente/capacidades`, which requires `asistente.consultar`.
- **Audit.** `RepositorioAuditoria` runs EF LINQ over `audit.change_log` with left joins to
  `identity.users`/`identity.personas`, filters, `LongCountAsync`, then
  `OrderByDescending(changed_at).ThenByDescending(id).Skip/Take` (5 s timeout).
  `ServicioAuditoria.Mapear` computes action label, module, object, summary and masked
  changes in C# from static dictionaries (`EtiquetasModulos`, `EtiquetasObjetos`,
  `EtiquetasCampos`, `CamposConValorSeguro`, `CamposPersonalesOSecretos`); the file is
  already 264 lines. `audit.log_change` stamps `changed_by` and `request_id` from session
  GUCs that `AuditDbConnectionInterceptor` sets on every pooled connection: both are empty
  (→ NULL) when there is no HTTP context, and `changed_by` is also NULL for an anonymous
  request or a non-UUID user claim.
- **Assistant administration log.** `asistente.auditoria_administracion (id, actor_id NOT
NULL, ocurrido_en, accion, antes, despues)` is written only by
  `AuditoriaDeAdministracionReal` from `AdministracionAsistenteController`, with five action
  codes and hand-built JSON: `presupuesto.rol` `{rol, cupo}`, `presupuesto.usuario`
  `{actorId, cupo|null}`, `tope_organizacional` `{topeMensualUsd}`,
  `mantenimiento.activar|desactivar` (serialized `EstadoDeMantenimiento {Activo, Razon}`).
  Retention 365 days; volume is human-rate administrative edits.
- **Project graph.** `backend/manifiesto-de-aristas.json` is checked by
  `ManifiestoDeAristasTests` (and acyclicity by `AciclicidadDelGrafoTests`).
  `Modules.Asistente.Contracts` is registered as `huerfano`, with the keep-or-delete
  decision open in its README. `ArquitecturaAsistenteTests.El_modulo_solo_referencia_ArsDocendi_Shared`
  pins `Modules.Asistente`'s only reference. The Host references every module project for
  composition, and already consumes `Modules.Designaciones.Contracts` from its
  administration surface (`IAdministracionDesignaciones`) — the precedent followed here.
- **`unaccent`** is created by `database/asistente/001_asistente_grants.sql`, not by any
  identity/audit script.
- **UI library.** `@ars-docendi/ui` `Tabs` renders `role="tablist"`, buttons with
  `role="tab"`, `aria-selected`, `aria-controls="panel-<id>"`, `id="tab-<id>"`, roving
  `tabIndex` and ArrowLeft/ArrowRight; `TabItem.label` is a `ReactNode`. `Pagination`
  accepts a `meta` node. `StatusBadge` kinds are designation states only.

### Dependency on in-flight changes

`mejorar-dashboard-y-auditoria` (un-archived) modifies «Consulta paginada y de solo lectura
de auditoría»; this delta was written over **its** text, not the main spec's, and keeps all
its scenarios. One of its scenarios says an unidentified actor is not attributed to an
automatic process "sin evidencia"; D6 below supplies the evidence rule and changes the
wording, so the two changes must be archived in order (see Migration Plan).
`asistente-administracion-de-uso` (un-archived) owns the usage panel's behavior; it names no
frontend route, so the new placement is specified in `administracion-sistema` only.

## Goals / Non-Goals

**Goals:**

- One route, one nav entry, three independently gated tabs, deep-linkable by hash, with
  shareable audit filters.
- One audit feed across two sources with exact pagination, a label-only search and a single
  masking rule, without the Host reading the `asistente` schema.
- The maintenance state visible to `sistema.estado.ver` holders without widening any other
  permission.

**Non-Goals:**

- Auditing Aulas or Tareas (they have no tables), or attaching `audit.attach` to `asistente.*`.
- Changing retention, triggers, permissions or seeds; merging the three permissions.
- Mobile layout; the design's «Datos de ejemplo» switcher.
- Changing the usage panel's behavior, copy or endpoints beyond removing its page header.
- A general cross-module audit contract for every module: only the assistant needs one,
  because only its log lives outside `audit.change_log`.

## Decisions

### D1. The assistant log reaches the Host only through `Modules.Asistente.Contracts`

`Modules.Asistente.Contracts` gets its first types, DTOs and interfaces only:

```text
IConsultasDeAuditoriaDeAdministracion
  ListarAsync(DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken)
    → LoteDeAuditoriaDeAdministracion(IReadOnlyList<EventoDeAdministracion> Eventos, bool Truncado)
EventoDeAdministracion(long Id, Guid ActorId, DateTimeOffset OcurridoEn, string Tipo,
                       string? Clave, Guid? UsuarioAfectado,
                       IReadOnlyList<CampoDeAdministracion> Campos)
CampoDeAdministracion(string Campo, string? ValorAnterior, string? ValorNuevo)

IConsultaDeMantenimiento
  ConsultarAsync(CancellationToken) → EstadoDeMantenimientoPublico(bool Activo)
```

`Modules.Asistente` implements both (`internal` classes registered in its
`ModuleExtensions`): the first reads `asistente.auditoria_administracion` with the owner
connection, ordered `ocurrido_en DESC, id DESC`, bounded by a cap of 2000 rows
(`Truncado = true` when the cap is hit), and **parses its own JSON** into normalized fields
(`cupo`, `tope_mensual_usd`, `activo`, `razon`), a `Clave` (role code for `presupuesto.rol`)
and `UsuarioAfectado` (for `presupuesto.usuario`). The JSON shape therefore never leaves
the module that writes it. The second wraps `IDisponibilidadDelModulo.ConsultarAsync` and
drops `Razon`.

This adds two edges — **ArsDocendi.Host → Modules.Asistente.Contracts** and
**Modules.Asistente → Modules.Asistente.Contracts** — and moves the project from `huerfano`
to `activo`. Contracts references nothing, so no cycle is possible. The README's open
decision closes as "keep": the assistant now publishes something. The Asistente
architecture test that pins its references to `["ArsDocendi.Shared"]` is updated to allow
exactly its own Contracts.

**Alternatives considered.**
(a) Host SQL or a view over `asistente.auditoria_administracion` — rejected: breaks rule 1;
rule 11's exception covers only the assistant reading others, never the reverse.
(b) Resolve the internal `IAuditoriaDeAdministracion`/`IDisponibilidadDelModulo` from the
Host, which already references `Modules.Asistente` for composition — rejected: those are
internals, and composition references exist to register services, not to consume them.
(c) `audit.attach` on the assistant's configuration tables — rejected: the assistant
deliberately does not use it (purgeable, FK-free tables), the log carries semantic actions
the trigger cannot reconstruct, and it would double-record every edit.
(d) Return raw `antes`/`despues` strings in the contract — rejected: the Host would learn the
assistant's private JSON format, and raw documents must not be exposed anyway.

### D2. Exact two-source pagination: B fetched whole under its cap, A windowed, merged in memory

Let `o = (pagina − 1) × tamano`, `s = tamano`. Total order: `CambiadoEn DESC`, then source
(`cambios` before `asistente`), then numeric id `DESC`.

1. **Assistant side (B).** Fetch through D1 (already bounded by its own 2000-row cap),
   apply every filter in memory (D4), map, sort. This is **always the whole of B**, never an
   incremental slice — the cap is what keeps it cheap, not a partial fetch. `nB = |B|`,
   `B' = B[0 .. min(nB, o + s))`. Only these rows can land in the window.
2. **Change-log side (A).** `a0 = max(0, o − |B'|)`. Fetch `A[a0 .. a0 + s + |B'|)` with the
   existing filtered, ordered query (`Skip(a0).Take(s + |B'|)`), plus `COUNT(*)`.
3. **Merge.** `FusionDeFuentesAuditoria` merges the two ALREADY-ORDERED bounded lists —
   `A[a0 .. a0 + s + |B'|)` and `B'` — with an ordinary two-way merge (mergesort join) over the
   total order above, then slices `[o − a0, o − a0 + s)` of that merged sequence, which is
   exactly `[o, o + s)` of the true combined order. The function is a pure, general merge of
   whatever two ordered sequences it receives; it is not a position-formula derivation keyed to
   `a0`, `o` or `s` — that arithmetic lives one layer up, in the code that decides how much of
   each source to fetch before calling it, and stays a plain two-way merge no matter how the
   caller sizes its inputs.
4. `Total = countA + nB`.

**Why fetch B whole instead of also windowing it.** `nB` is capped at 2000 by design (D1), and
that cap is the entire cost argument for B: even at the maximum page size, mapping and sorting
2000 small rows in memory is cheap, and it is _simpler and less error-prone_ than deriving a
partial-B window with its own arithmetic. Windowing A instead is what actually matters for cost,
because `change_log` has no such cap and grows with the system's whole history — that is the
side where a naive full fetch would grow linearly with page depth (see alternative (a) below).

**Cost bound.** Per request: one count and at most `s + min(nB, o + s)` rows from
`change_log` (≤ 200 for page 1 at the maximum page size), plus at most 2000 small rows from
the assistant log, always fetched whole. The depth of the page adds only PostgreSQL's usual
OFFSET scan on the `change_log` side, which the current implementation already pays. The merge
is a pure static function (`FusionDeFuentesAuditoria`) with a property test against the naive
"materialize both, sort, slice" reference over random interleavings, page sizes and tie
timestamps.

When a module filter selects one source, the other is not queried at all (both a cost and
a failure-isolation choice).

**Alternatives considered.** (a) Fetch the first `o + s` rows of each source and slice — the
suggested simple version; correct, but `change_log` rows grow linearly with page depth
(page 20 × 100 = 2000 rows mapped per request) — this is exactly why A, not B, gets the
`a0`-windowed fetch. (b) Push every filter and `q` into the assistant query and `UNION ALL` in
SQL — impossible without cross-schema SQL (D1). (c) Keyset pagination — better for deep pages,
but the design shows numbered pages with «1–N de total» and the existing API is offset-based.

### D3. Event identity and partial results

`EventoAuditoriaDto.Id` becomes a string: `cambios-<change_log.id>` or
`asistente-<auditoria_administracion.id>`. A new `Origen` field (`cambios` | `asistente`)
spares the client from parsing it. `PaginaAuditoriaDto` gains `Parcial: bool` and
`FuentesNoDisponibles: string[]`.

If the assistant query throws (other than caller cancellation) or returns `Truncado`, the
service logs a Serilog warning with the source name and exception type only (no filters,
no values), serves the change-log side with `a0 = o` as if `nB = 0`, and returns
`Parcial = true`, `FuentesNoDisponibles = ["asistente"]`, `Total = countA`. The tab shows an
`InlineAlert`: «No se pudieron cargar los registros del asistente. Se muestran los demás.»
A failure of `change_log` itself keeps failing the request as today (it is the primary
store; returning only assistant rows would misrepresent the system's history).

### D4. Search on labels, resolved in C#, executed in SQL

`q` (≤ 100 chars) is normalized once (trim, lower-case, accents removed). Against the static
label dictionaries, C# computes: the schemas whose module label contains `q`, the
`schema.table` pairs whose object label contains `q`, and the column keys whose field label
contains `q`. The change-log predicate is the OR of:

- `schema_name = ANY(@schemas)`; `(schema_name, table_name) ∈ @objetos`;
- `changed_columns && @campos`, or for INSERT/DELETE (null `changed_columns`) the snapshot
  **key** test `new_row ?| @campos` / `old_row ?| @campos` — keys, never values;
- `unaccent(lower(schema_name || '.' || table_name)) LIKE @patron`;
  `unaccent(lower(row_pk)) LIKE @patron`;
- `unaccent(lower(<displayed actor name expression>)) LIKE @patron`, including the literals
  «Proceso automático»/«Actor no identificado» for the corresponding cases.

`@patron` escapes `%`, `_` and `\`. The assistant side applies the same normalized matcher in
memory to its mapped actor name, module «Asistente», object label (which may contain the
affected user's name, D5), synthetic table, key and field labels. **No branch reads
`new_row ->> …`, `old_row ->> …` or a mapped value**, so `q` is not an oracle for any value,
masked or safe. An integration test seeds a masked value and a safe value and asserts both
searches return nothing.

`unaccent` becomes an explicit dependency of the audit infrastructure through a new
idempotent `database/audit/002_audit_busqueda.sql` (`CREATE EXTENSION IF NOT EXISTS
unaccent;`), embedded by `ArsDocendi.Shared` like every audit script and applied by a new
`IdentityDbContext` migration that executes it (the pattern of
`20260923181631_PermisosAdministracionSistema`), so the Host does not depend on the
assistant's migration order. Calls are
schema-qualified (`public.unaccent`), as the assistant already does; EF translation uses
Npgsql's `EF.Functions.Unaccent`/`ILike` or a mapped `DbFunction` — whichever the
implementation finds translates cleanly, verified by the integration test.

`q` replaces the `actor` parameter (same semantics, broader reach); `cambiadoPor` stays for
compatibility.

**Search also finds the humanized subject (fix after the headless check, 2026-09-27).** D5's
object label can name the affected person — «Cuenta de usuario de {nombre}», «Persona
{nombre}», «Roles de {nombre}» — but that name is not a static label D4 already knows, so
`q` missed those three events entirely. Fix: before building the `q` predicate,
`RepositorioAuditoria` runs **one** extra query (`Concat` → `UNION ALL`, same shape as
`ResolverNombresDeSujetosAsync`) resolving which `identity.users`/`identity.personas` rows
have a **current** displayed name matching `@patron` — the same name expression D5's label
already uses (persona's full name if it has both `nombre` and `apellido`, else the account's
`display_name`) — and adds three OR branches keyed on those **ids**, not the name text:
`identity.users` by `row_pk`, `identity.personas` by `row_pk`, and `identity.user_roles` by the
`user_id` **value** of `old_row`/`new_row`. This is the one exception to «no branch reads
`old_row ->> …`»: it extracts `user_id` — the same internal id `IdentidadDeAuditoria` already
extracts for the label itself — through a dedicated query (`(new_row ->> 'user_id')::uuid = ANY
(@ids)`), never any displayed or masked field value, and only ever compares it against the
already-resolved id set, never against `q`'s text. This is a plain `jsonb ->>` cast, not
`EF.Functions.Like` on the `jsonb`-typed column, which Postgres rejects (`operator does not
exist: jsonb ~~ jsonb`) without an explicit cast. Each branch also repeats the exact rename
exclusion D5 already uses for the label — `identity.users` excludes an event whose own
`changed_columns` include `display_name`; `identity.personas` excludes `nombre`/`apellido` —
so a rename event is never a name hit even though the person's current name already matches:
consistent with «the search never shows something the label itself would hide».

**Alternatives considered.** (a) Full-text index over labels — labels are not stored; they
live in C#. (b) Filtering in memory after fetching — breaks server pagination and counts.
(c) Also matching safe values — rejected by product decision: once search reads values, the
safe/masked boundary must be re-proven for every future field; label-only is provably safe.

### D5. Assistant event mapping and value-bearing summaries

| `Tipo`                     | Acción                              | Objeto                        | Tabla                           | Clave de fila    | Campos             | Resumen                                               |
| -------------------------- | ----------------------------------- | ----------------------------- | ------------------------------- | ---------------- | ------------------ | ----------------------------------------------------- |
| `presupuesto.rol`          | Cambio                              | «Cupo diario del rol {rol}»   | `asistente.presupuesto_rol`     | role code        | `cupo`             | «Cupo diario del rol {rol}: {a} → {b}»                |
| `presupuesto.usuario`      | Alta if before is null, else Cambio | «Cupo diario de {nombre}»     | `asistente.presupuesto_usuario` | affected user id | `cupo`             | «Cupo diario de {nombre}: {a} → {b}» (Alta: «…: {b}») |
| `tope_organizacional`      | Cambio                              | «Tope mensual organizacional» | `asistente.tope_organizacional` | —                | `tope_mensual_usd` | «Tope mensual organizacional: {a} → {b} USD»          |
| `mantenimiento.activar`    | Cambio                              | «Mantenimiento del asistente» | `asistente.modo_mantenimiento`  | —                | `activo`, `razon`  | «Mantenimiento del asistente activado»                |
| `mantenimiento.desactivar` | Cambio                              | «Mantenimiento del asistente» | `asistente.modo_mantenimiento`  | —                | `activo`, `razon`  | «Mantenimiento del asistente desactivado»             |

No `Eliminación` exists for assistant events today (no delete path). `Solicitud` is always
«—» (the log records no request id). `{nombre}` is the affected user's display name,
resolved by the Host from identity exactly like an actor name (else «usuario no
identificado»); a name used as an object label
is the same kind of data already shown as actor, not a snapshot value. Field labels:
«Cupo diario», «Tope mensual (USD)», «Mantenimiento activo», «Razón». `cupo`,
`tope_mensual_usd` join `CamposConValorSeguro`; `activo` already is; `razon` is free text and
stays unclassified, hence masked. An unknown `Tipo` maps to «Cambio» with a humanized
object and no values (future-proof fallback).

Change-log summaries: an UPDATE whose changed fields are **exactly one** safe field reads
«{Objeto} #{row_pk}: {Campo} {a} → {b}» (the `#row_pk` part only when `row_pk` is numeric,
so UUIDs do not flood the line), e.g. «Solicitud #1042: Estado pendiente → aprobado».
Every other case keeps the generic form without values — «{Acción} de {objeto} · {campos}»
— including multi-field updates where every field is safe (a one-line summary with several
value pairs is unreadable; the detail panel shows them).

**Humanized identity events (added after the 11.4 visual check, user-approved 2026-09-26).**
Safe boolean values (`activo`, `is_active`, `es_sistema`, …) are formatted «Sí»/«No» by the
mapper, for summaries and for the `cambios` values sent to the detail panel. For
`identity.users`, `identity.personas` and `identity.user_roles` the mapper names the subject
person: the Host resolves the person id — `row_pk` for users/personas, and the `user_id`
snapshot **key's value** for `user_roles` (an internal id used only for lookup, never
displayed) — through the same `RepositorioAuditoria` name resolution used for actors, and
the role name from `identity.roles` by `role_id`. Labels: «Cuenta de usuario de {nombre}»,
«Persona {nombre}», «Roles de {nombre}» for a role assignment, and for `user_roles`
INSERT/DELETE the summary «Rol {rol} asignado a {nombre}» / «Rol {rol} quitado a {nombre}».
**The real revocation flow is a soft delete, not a physical DELETE**: `ServicioUsuarios.
ReemplazarAsignaciones` sets `deleted_at` via `UPDATE`, so an INSERT/DELETE-only rule would
never show «quitado a» for an actual revocation — confirmed against the D5 acceptance
scenario when a real `ServicioUsuarios` round-trip (assign then revoke) surfaced the gap.
The summary rule therefore also covers a `UPDATE` of `identity.user_roles` whose changed
columns include `deleted_at`: `deleted_at` going null → non-null reads «Rol {rol} quitado a
{nombre}», and non-null → null (a reactivation, symmetric with the delete case even though
`ReemplazarAsignaciones` does not exercise it today — a re-grant always inserts a fresh row)
reads «Rol {rol} asignado a {nombre}». The action chip still reflects the actual action
(`UPDATE` → «Cambio»); only the summary text is special-cased. The name comes from current
identity rows, never from the snapshot; if the event changed `nombre`/`apellido`/
`display_name`, or the lookup misses, the label stays generic so a rename is never exposed
as a diff. Resolution is one batched query per page (ids collected across the page), not one
per row. Alternative rejected: reading names from the snapshots — they are masked personal
fields.

**Detail panel value formatting (fix after the audit-panel UI review, 2026-09-27).**
`MapearCambio`/`IntentarObtenerValor` read a JSON `null` and previously round-tripped
`JsonValueKind.Null` through `GetRawText()`, which is the literal text `"null"` — the panel
showed it verbatim instead of «—». Fixed: a JSON `null` now maps to a real `null` in
`CambioAuditoriaDto`, same as an absent property. A safe TIMESTAMPTZ field (`created_at`,
`deleted_at`) and a safe DATE field (`vigente_desde`, `vigente_hasta`) are additionally
converted to the institution's time zone (`America/Argentina/Buenos_Aires`) and formatted
«d/m/aaaa HH:mm:ss» or «d/m/aaaa» respectively — the same formatter feeds both the `cambios`
values and any value-bearing summary that reuses them, so there is exactly one place that
knows the institution's time zone on the backend. Separately, `ServicioAuditoria.Mapear` now
drops a field from `cambios` entirely when it is absent/`null` on **both** sides (checked on
the raw JSON snapshots, before masking — a masked field's DTO is always `null`/`null`
regardless of its real value, so the omission check cannot read the already-masked DTO). This
kills the «Fecha de baja: — → —» noise on an INSERT while keeping a masked field that does
carry a value on at least one side. Finally, `EtiquetasCampos` (`EtiquetasAuditoria.cs`) grew
one entry per column of every `audit.attach`-ed table that still fell back to `Humanizar()`
(e.g. `carrera_id` → «Carrera», `granted_at` → «Otorgado el» — see the DDL under
`database/*/`), because that fallback is what produced raw «X id» / English labels; masking
classification (`CamposConValorSeguro`/`CamposPersonalesOSecretos`) is unchanged.

Code organization, to respect the ~300-line cap: `EtiquetasAuditoria.cs` (dictionaries,
normalization, label → key resolution for D4), `MapeadorEventoAuditoria.cs` (actor, action,
masking, summaries for both sources), `FuenteAuditoriaAsistente.cs` (contract call, name
resolution, in-memory filters), `FusionDeFuentesAuditoria.cs` (D2), and a slimmer
`ServicioAuditoria.cs` (validation + orchestration). `RepositorioAuditoria` keeps
`change_log` and gains `ResolverNombresAsync(ids)` for assistant actors and affected users.

### D6. «Proceso automático» requires the absence of request context

| `changed_by`     | `request_id` | Shown as                | Evidence                                                           |
| ---------------- | ------------ | ----------------------- | ------------------------------------------------------------------ |
| resolves         | any          | person / display name   | identity join                                                      |
| does not resolve | any          | «Actor no identificado» | stamped id without account (practically prevented by the FK)       |
| null             | present      | «Actor no identificado» | an HTTP request wrote it but its user was anonymous or not a UUID  |
| null             | null         | «Proceso automático»    | no HTTP context: migration, seed, background service or direct SQL |

Person names switch from «Apellido, Nombre» to the design's natural order «Nombre
Apellido» (e.g. «Lucía Fernández»), for actors and for affected users alike, so a row never
reads «Fernández, Lucía · Asistente · Cupo diario de Lucía Fernández»; the fallback to the
account's display name is unchanged, and the actor search matches the displayed form.

The response adds `TipoActor` (`persona` | `proceso` | `no_identificado`) so the avatar is
neutral for the last three rows without comparing strings. This satisfies the product
decision (D5 of the brief) and supplies the evidence that `mejorar-dashboard-y-auditoria`'s
scenario "no lo atribuye a un proceso automático sin evidencia" demanded; that scenario is
replaced by the delta's «Actor no identificado» and «Automatic process» scenarios. Action
labels change to Alta / Cambio / Eliminación; «Eliminación» stays unambiguous because only a
physical `DELETE` produces it.

**Alternative considered.** Keep «Actor no identificado» for every null — rejected by the
product decision, and it hides the common, legitimate case of seeds and background jobs.

### D7. Maintenance state on the status endpoint

`EstadoBaseDatosDto` becomes `EstadoSistemaDto { estado, comprobadoEn, duracionMs,
mantenimientoAsistente }` (additive; existing fields keep their names), with
`mantenimientoAsistente ∈ { "activo", "inactivo", "desconocido" }`. `ServicioEstadoSistema`
runs the `SELECT 1` and `IConsultaDeMantenimiento.ConsultarAsync` concurrently, the latter
under its own 3 s timeout; any failure yields `desconocido` and a Serilog warning. Minimal
by design: the card copy («Consultas pausadas por un administrador») needs no reason, time or
actor, and `sistema.estado.ver` must not become a way to read who toggled the switch.
Unknown maintenance renders the assistant card by its ping alone, with the note «Estado de
mantenimiento sin comprobar», and never contributes to the maintenance banner.

**Alternatives considered.** (a) Call `/api/asistente/capacidades` from the section —
requires `asistente.consultar`. (b) A new anonymous or `sistema.estado.ver` endpoint inside
the assistant module — a second place that answers "is the system healthy", and the module
would need to know a Host permission's purpose.

### D8. Frontend composition across two features

- `features/asistente` extracts the body of `AdministracionAsistentePage` into
  `components/PanelAdministracionAsistente.tsx` with prop `actualizacion: number` (a counter;
  when it changes, the panel invalidates its own `["asistente","administracion"]` and
  `["asistente","capacidades"]` queries, keeping its selected period) and exports it from
  `features/asistente/index.ts`. The page file is deleted; the `administracion` route
  becomes `<Navigate to={{ pathname: "/sistema", hash: "#asistente" }} replace />` inside the
  same `RequirePermission` gate.
- `features/sistema/routes.tsx` exports `crearRutas({ PanelAsistente })` instead of a constant.
  `app/router.tsx` — the composition root, allowed to import both barrels — passes
  `PanelAdministracionAsistente`. `features/sistema` never imports `features/asistente`.
- `/auditoria` becomes `<Navigate to={{ pathname: "/sistema", hash: "#auditoria" }} replace />`.

**Alternatives considered.** Move the usage panel into `features/sistema` (splits the
assistant feature's API client and types across two features); lift it to `shared/` (it is
not shared UI, it is assistant domain).

**D8.1 Visual redesign of `PanelAdministracionAsistente` (ARS-156, follow-up).** The panel
extracted in D8 kept the pre-existing markup — plain stacked sections with no CSS of their
own. It was rebuilt to match the «Uso del asistente» Claude Design canvas: a pressed-button
period control (`FiltrosAuditoria`'s period-toggle pattern) instead of a full-width
`<select>`; `KpisDeUso` (four cards) + `TopeOrganizacionalCard` for the organization-wide
totals, replacing the old «Organización» table row; and `PanelDeUso` rebuilt around `Tabs`
(«Por usuario» / «Por rol», each with a count) over one searchable, sortable table, with the
daily quota edited inline per row via `EditorDeCupoEnFila` — retiring `EditorDeLimite` and
its two free-text «Código de rol» / «Id del usuario» fields entirely.

**Task 12.8, closed in part (apply report, `sistema-seccion-unificada`).** At D8.1's original
writing, `EditorDeCupoEnFila` and `TopeOrganizacionalCard` could not read a persisted
quota/cap (`PUT`-only endpoints, D2/D3 unchanged): "known" stayed "saved this session". A new
`GET /api/asistente/administracion/presupuestos` (Controller → `IPresupuestosAdministrables`
→ Postgres, same layering as the `PUT`s it sits next to) now exposes the persisted org
monthly cap, each role's default daily quota, each user's override, and the current calendar
month's estimated spend — computed by calling the same `IConsultasDeUso` the usage dashboard
uses, over `[start of month, now)`, so the two numbers can never disagree. Both card and
table now start from the persisted value and stay there after a save
(`PanelAdministracionAsistente` invalidates the query on every successful `PUT`); the "saved
this session" workaround is removed. `TopeOrganizacionalCard` gained the canvas's spend-vs-cap
bar, colored at the same 50/80/100% thresholds `DetectorDeUmbrales` already logs server-side.

**Still out of scope** (unchanged from the original D8.1, and from this task's own
instructions): the cloud/local provider switch with local-server telemetry (GPU, KV cache,
request slots), the per-day usage trend chart, and a per-user/per-role access on/off toggle —
none of the three has backend surface, and the last one has no backend concept at all yet.
`tasks.md` 12.8 is split accordingly: the slice above is checked off, and the three
still-missing pieces are recorded as a separate, explicitly deferred line — not silently
dropped.

### D9. Routing, guards and navigation

- `/sistema` is guarded by `RequirePermission`, whose `permission` prop widens to
  `string | readonly string[]` (any-of). Existing single-string call sites are unchanged.
- `NavItem.permiso` widens the same way; `filtrarNavegacion` keeps an item when the session
  holds any listed permission. The «Sistema» group holds one entry
  `{ to: "/sistema", icon: "settings", label: "Sistema", permiso: [three permissions] }`; the
  three old entries are removed. This is the smallest change: no new type, no new component,
  and `navegacion-por-permisos` stays satisfied (permission-derived, never role-derived).
- Tab state: a `useSeccionSistema` hook reads `location.hash`, resolves the initial tab
  (valid **and** permitted, else first permitted in Estado → Asistente → Auditoría) and
  `replace`-navigates when it had to correct the hash, so the URL always names the tab shown.
  Changing tab pushes a history entry (Back returns to the previous tab).
- Audit filters live in the query string, owned by `useFiltrosAuditoria`:
  `q`, `periodo` (`hoy` | `7d` | `30d` | `todo`, default `7d` omitted from the URL),
  `accion`, `modulo`, `desde`, `hasta`, `tabla`, `clave`, `pagina`, `evento`. Defaults are
  omitted so «Limpiar filtros» is "remove every filter param". Query params and hash coexist
  (`/sistema?periodo=30d#auditoria`).
- API params: `desde`, `hasta`, `accion` (`INSERT|UPDATE|DELETE`), `modulo` (`identity`,
  `designaciones`, `portal`, `asistente`, or any schema name ≤ 63 chars for the fallback),
  `tabla` (`schema.tabla` or bare table name, ≤ 127 chars), `rowPk`, `q`, `cambiadoPor`,
  `pagina`, `tamanoPagina`. `schema` is replaced by `modulo` and `actor` by `q` — the
  frontend is the only consumer and changes in the same diff.

### D10. Tabs and the detail panel

`Tabs` from `@ars-docendi/ui` is used as is: each `TabItem.label` composes a local
`PuntoDeEstado` (a dot plus visually hidden text such as «, sin problemas», «, componentes no
disponibles», «, en mantenimiento», «, estado pendiente») with the tab name. Panels render
`role="tabpanel" id="panel-<id>" aria-labelledby="tab-<id>"` to match the library's ids.
The library offers Arrow keys but not Home/End; with at most three tabs that is accepted
rather than forking the component. Only the active panel is mounted, except that Estado's
health queries run whenever the session holds `sistema.estado.ver`, because the Estado dot
needs them on any tab.

The detail panel is a local `<aside>` in a two-column grid (`minmax(0,1fr) 380px`) — the list
narrows, nothing overlays it — not `Drawer`, which is a modal overlay. The selected row gets
`aria-current="true"`; the panel's close button is labelled «Cerrar detalle»; opening moves
focus to the panel heading and closing returns it to the row. The panel is derived from
`evento` in the URL and the loaded page: if the id is not in `elementos`, `evento` is removed
(replace), which implements "closes when its event leaves the result".

**Escape closes the panel too (ARS-160 acceptance criterion, fix after the audit-panel UI
review, 2026-09-27)**: `onKeyDown` on the `<aside>` calls the same `onCerrar` the «×» button
calls when `event.key === "Escape"` — one close path, so focus-return to the row (the effect
keyed on the URL's `evento` going back to `null`) already covers it without a second code path
to keep in sync.

Chips and the period control are toggle-button groups (`aria-pressed`), composed locally —
the library has no chip or segmented control. Action chips reuse the design's three tones
via tokens.

### D11. Periods, days and the institution's time zone

All dates are rendered and bucketed in `America/Argentina/Buenos_Aires` via
`Intl.DateTimeFormat({ timeZone })`, regardless of the browser zone. `hoy` = start of today;
`7d` = start of the day six days ago (seven calendar days including today); `30d` = start of
the day 29 days ago; `todo` = no bounds. The upper bound is left open (live data). «Desde» /
«Hasta» (`datetime-local`) are interpreted as wall time in that zone and converted to an ISO
instant with the zone's offset for that instant (computed through `Intl`, not hard-coded, so
a future DST rule does not silently shift filters). Day headers, «Hoy»/«Ayer» and the recent
changes' «Hoy 22:10» / «Ayer 19:12» / «23/9 14:30» use the same helpers
(`utils/zonaHoraria.ts`, `utils/agruparPorDia.ts`), unit-tested with a pinned clock and a
non-Argentine `TZ`.

### D12. Health probes and «Cambios recientes»

One React Query per component (`["sistema","salud",<id>]`), so «Reintentar» refetches one
key and «Actualizar» refetches the group. `asistente` joins the ping list. The estado
endpoint feeds both the PostgreSQL card and the assistant's maintenance. Card and banner
logic is a pure function `resumirSalud(resultados)` returning the banner variant, names and
counts, tested exhaustively. Card notes beyond the design: a failed ping that did not time
out reads «Respondió con error» (claiming «No respondió en 5 s» would be false); a ping that
answered without `status: "ok"` counts as unavailable with the same note.

«Cambios recientes» calls the same audit endpoint with `tamanoPagina=4` and no dates, only
when the session holds `auditoria.ver`. A row navigates to
`/sistema?periodo=todo&evento=<id>#auditoria`: with no other filter and period «Todo», an
event among the four newest is on page 1 unless more than 46 events arrived in between, in
which case the list simply opens without a panel (D10) — no extra "get by id" endpoint. The
empty state reads «Todavía no hay cambios registrados.».

### D13. Module chips are a static list

The chips are `Todos`, `Identidad`, `Designaciones`, `Portal`, `Asistente`, declared once in
`features/sistema` next to the backend label they mirror. They are "derived from modules
with audited data" by decision, not discovered at runtime: which schemas call
`audit.attach` changes only with a migration, and a runtime endpoint would be one more
surface to keep consistent for a list that changes once per module. When Aulas or Tareas
gain audited tables, adding their chip is part of that change (noted in the design spec).

## Risks / Trade-offs

- [Direct SQL sessions without GUCs are shown as «Proceso automático»] → Documented in the
  API contract; the only other writers without request context are migrations, seeds and
  background services, and editing the database by hand is already forbidden by AGENTS.md.
- [The assistant log could exceed the 2000-row cap in one range] → Surfaced as a partial
  result, never as silently wrong counts; at human edit rates and 365-day retention the cap
  is years away. Raising it is a constant.
- [Offset merge correctness is subtle] → Pure function with a property test against the
  naive reference, including equal timestamps across sources. B is always fetched whole under
  its own 2000-row cap (D1) rather than windowed like A, which keeps the merge a plain two-way
  merge of two already-bounded, already-ordered lists instead of a second position-formula
  derivation.
- [`unaccent` over the actor expression prevents index use] → The expression was already
  unindexable (`lower(...)` over a join); the 5 s timeout, the 100-char bound and the
  default 7-day window keep the scanned range small.
- [Health queries run on every tab] → Six lightweight requests on section open, the same
  as the dashboard today; skipped entirely without `sistema.estado.ver`.
- [Hiding tabs could be mistaken for authorization] → Every endpoint keeps its own policy;
  integration tests assert 403 per endpoint for each missing permission.
- [Breaking DTO changes (`id` string, `accionEtiqueta`, `actor`/`schema` params)] → The
  frontend is the only consumer; both ship in one deploy.
- [Subtitle mentions areas a partially permitted user cannot see] → Accepted: it describes
  the section, not the user's access, and no action is offered for hidden tabs.

## Migration Plan

1. Archive `mejorar-dashboard-y-auditoria` and `asistente-administracion-de-uso` before this
   change; `openspec archive` of this change must run against the main spec as those leave
   it (the MODIFIED audit requirement assumes the former's text).
2. Deploy is a single backend + frontend release. `002_audit_busqueda.sql` is idempotent and
   runs through its `IdentityDbContext` migration at startup, like the other audit and
   identity scripts; no data is migrated.
3. Old bookmarks (`/auditoria`, `/asistente/administracion`) keep working through redirects.
4. Rollback: redeploy the previous backend and frontend together. Leaving the `unaccent`
   extension installed is harmless (the assistant already requires it). Reverting the
   Contracts edges restores the manifest's `huerfano` entry from git.
