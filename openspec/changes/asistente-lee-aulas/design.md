## Context

The assistant reads foreign schemas with two read roles and column-by-column `GRANT` (AGENTS.md rule 11). `asistente-lee-comisiones` built the pattern this change copies: RLS migration plus `COMMENT ON` migration in the owning module's chain, grants in `database/asistente/011_asistente_grants_dominios.sql`, both manifests, a domain flag in `DetectorDeDominios`, a permission flag read live in `ConsultorDeAlcance`, per-domain abstention, fixture, evaluator items.

Facts checked before writing this design:

1. **`aulas` has one table**, `aulas.solicitudes_reserva` (requests for an aula for an exam). `aula_asignada`, `motivo_rechazo`, `motivo_cancelacion` and `comision` are free text; there is no aula catalog anywhere.
2. **Nullability follows the circuit**: `aula_asignada` is set iff `aprobada`; `motivo_rechazo` iff `rechazada`; `motivo_cancelacion` only when an approver cancels (null when the docente cancels their own pending request); `rol_solicitante` is null on old rows.
3. **Permissions** (`Permisos.cs`, `ServicioSolicitudesAula`): `aulas.aprobar` (secretaria, administrativo, sys_admin) sees all requests; `aulas.solicitar` (docente, jefe de cátedra) sees only requests where `docente_id` is their persona. Docentes have no assistant access, so in practice approvers see all and jefe de cátedra sees their own.
4. `identity.asistente_tiene_permiso(text)` and `identity.asistente_persona()` (NULL without a persona) already exist and are granted.
5. The `aulas` table has no RLS today and the Aulas module connects as owner, so `ENABLE` (not `FORCE`) leaves the screen untouched.
6. `asistente-lee-comisiones` is still active and MODIFIES `asistente-alcance-por-actor`; this change therefore ADDS a separate requirement there.

## Goals / Non-Goals

**Goals:** answer the questions of the proposal through the text-to-SQL lane; keep the boundary falsifiable in the engine; never present a scoped or empty result as a fact about the Departamento; keep the prefix inside the local 3070 profile.

**Non-Goals:** free aulas and capacity (no catalog); links and cards (separate change); the compiled plan; changing the Aulas DDL, screen or API.

## Decisions (taken by the team; recorded, not reopened)

### D1 — RLS mirrors the Aulas screen

`ENABLE` (not `FORCE`) row level security and one `FOR SELECT` policy:

`identity.asistente_tiene_permiso('aulas.aprobar') OR (identity.asistente_tiene_permiso('aulas.solicitar') AND docente_id = identity.asistente_persona())`

**Why:** the assistant must not know more or less than the screen. A requester without a linked persona compares to NULL and sees nothing (fails closed). The owner is exempt, so the screen and its notifications keep working. Migrations: `Aulas RlsAsistenteSolicitudes` runs before the grant; `Aulas ComentariosAsistenteSolicitudes` carries the comments.

### D2 — Grant every column; two are `sensible-texto`

All 14 columns are granted to both roles. `motivo_rechazo` and `motivo_cancelacion` are `sensible-texto` (the user sees them in the result table, they never travel to the model); everything else is `publica`. **Why:** the free-text motives can name people or give reasons that do not admit rule-based masking; suppressing the column for the model is the existing mechanism. The requester is expressed by `docente_id`, joined to `identity.personas` under that table's own grants.

### D3 — New domain `Aulas`; "none" is a fact only for approvers

`DominioConsultado.Aulas` is derived by the same detector as the other domains. `PoliticaDeAbstencion.AlcanzaTodo` requires `aulas.aprobar` for it (`PerfilDelActor.VeSolicitudesDeAula`); `aulas.solicitar` is a second flag (`VeSolicitudesPropias`) used only for announcing. An actor with only `aulas.solicitar` gets the existing scope notice on both empty and non-empty answers; with neither, the policy returns nothing and the answer is also not a fact. The catalog and the presentation announce "all requests" to approvers and "tus solicitudes de reserva de aula" to requesters. Both flags are read live in `ConsultorDeAlcance`, in the same query as the other permissions.

### D4 — Disambiguation with comisiones

"En qué aula se dicta X" and "aula de una comisión" keep going to `comisiones.sga_comision_horarios`. `aulas.solicitudes_reserva` is used only when the question says reserva, solicitud or pedido de aula, or a request state. The rule is written in the table's `COMMENT ON`, in the schedule-versus-request wording of the column comments, and in the SQL examples. **Rejected:** a lexical router in code; the prefix is the single source the model reads.

### D5 — Free aulas and capacity stay non-answerable

There is no aula catalog, so `cap-048` (free aulas) and `cap-049` (capacity) are unchanged. The presentation never offers them, and the comments say the table is not a catalog (`aula_asignada` exists only when approved).

### D6 — Evaluator

The fixture seeds twelve requests: three pending, four approved (two on one date), three rejected with motive, two cancelled (one with motive by an approver, one without by the docente). Several dates and materias. `aulas.aprobar` goes to the global role and `aulas.solicitar` to the jefe role; the jefe persona owns four requests, so the global actor sees twelve and the jefe four (a strict subset, verified by test against any seed). Six capacity items (`cap-051` to `cap-056`) and two robustness variants (`rob-019`, `rob-020`), plus three SQL examples. The vocabulary normalizer maps "solicitud" to "pedido" (designaciones), so dataset questions use "reservas" or "pedidos de aula" wording where a stricter phrasing collided with existing catalog examples; the disjointness checks (`DatasetDeCapacidadTests`) are the guard.

### D7 — Business rule

`BR-asistente-011` records D1/D3 with "No es normativa" and its test mapping, like BR-asistente-010. BR-asistente-003 gets the aulas clause.

## Risks / Trade-offs

- **The prefix grows** by the table comments (measured in task 3.3). Mitigation: comments are short; the 3070 measurement is a separate task for the funded run.
- **The `sensible-texto` motives are invisible to the narration**, so the model cannot paraphrase them; the table shows them. Accepted.
- **Requester scope depends on `users.persona_id`**; an unlinked requester sees nothing instead of everything (fails closed).

## Migration / Rollback

Two incremental migrations in the Aulas chain (RLS first, comments after); the grant lives in `011_asistente_grants_dominios.sql`, which `MigradorAsistente` re-applies as a whole. Rollback: revert the grant and manifests first, then the migrations; nothing is written to existing data.
