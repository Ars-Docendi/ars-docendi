## Context

The portal reading shipped with `asistente-lee-portal` and `asistente-portal-por-ambito` (archived 2026-11-06). Everything works for an actor who holds `portal.ver_trayectoria_ajena`, but no role does. The jefe de cátedra case needs three small things: the grant, one more table (projects), and evidence that the search with related terms works.

Facts checked before writing this design:

1. **Who consumes the permission.** Only the assistant: `ConsultorDeAlcance.PermisoDeTrayectoriaAjena`, the portal RLS (`20261020001500_RlsAsistentePortal.sql` and, through `identity.asistente_alcanza_a`, `20261020001700_RlsPorAmbitoPortal.sql` and `database/designaciones/20261103001400_...`), and the evaluator fixture (`GeneradorDeFixture`). A grep over `backend/src`, `frontend/src`, `database` and `scripts` finds no endpoint, authorization policy or screen that reads it. Personas uses `personas.ver_ambito` (`AlcancePersonas.cs`).
2. **`portal.proyectos`** columns: `id, perfil_id, nombre, rol, descripcion, desde, hasta, doi, created_at`; FK `perfil_id` to `portal.perfiles`; audited (`audit.attach`). It has no assistant policy today and is `denegada-explicita` in the manifest.
3. **The portal policies** all go through `identity.asistente_alcanza_a(persona)` (own profile, or permission AND (global OR current designación in the actor's materias)).
4. **`database/asistente/001_asistente_grants.sql`** carries the portal grants (lines 162-167); `MigradorAsistente` re-applies it convergently. RLS and comments for portal live in `database/portal/` incremental migrations.
5. **Measurement of 2026-11-07**: the longest request on the 3070 profile is 12,846 input tokens (+600 output ceiling = 13,446) of a 16,384 slot. `asistente-lee-aulas` adds some more.

## Goals / Non-Goals

**Goals:** let a jefe de cátedra search the docentes of their materias by what the docentes loaded in Mi Portal; say plainly what the portal can and cannot support; keep the engine boundary falsifiable; keep the prefix inside the local profile.

**Non-Goals:** candidates outside the cátedra; requirements per materia; `canonica_id`; CV content; contact; project descriptions; any UI.

## Decisions

All of D1 to D7 are team decisions of 2026-11-07 and are not reopened here.

### D1 — Grant `portal.ver_trayectoria_ajena` to `jefe_catedra`; scope unchanged (option A)

A new Identity migration (`node scripts/migrations/scaffold.mjs Identity PermisoPortalTrayectoriaAjenaJefeCatedra`) inserts into `identity.rol_permisos` for `jefe_catedra` only, `ON CONFLICT (rol_id, permiso_id) DO NOTHING`, with the pattern of `20261107000000_PermisosComisionesIdentity.sql`. The migration that created the permission is published and is not edited; its header (which says nobody holds it) is superseded by this one's header.

Scope stays `asistente_alcanza_a`: the jefe sees the trayectoria of docentes with a current designación in the materias they head. **Candidates outside their cátedra are not visible: accepted limitation (option A).** The answer states the coverage, so the limit is visible to the user.

**Blast radius.** The permission is consumed only by the assistant (fact 1). Granting it changes: what the assistant returns to a jefe de cátedra for portal queries; the capability catalog, which announces the docente search to holders; the evaluator fixture. It changes nothing in REST endpoints, the Personas screen (parity: it already shows the same profile to the jefe under `personas.ver_ambito`), or any other module. Existing per-user role assignments are not touched. `sys_admin` does not inherit it (the earlier decision in `20261103000400` stands).

**Rejected:** widen to the carrera (option C: changes BR-portal-007 and exposes many more profiles); leave the search with Secretaría (option B: leaves the use case unanswered).

### D2 — Projects: five columns, never `descripcion`

Grant `portal.proyectos(id, perfil_id, nombre, rol, desde, hasta, doi, created_at)` to both read roles in `001_asistente_grants.sql`. A Portal migration (`node scripts/migrations/scaffold.mjs Portal RlsAsistenteProyectos`) enables RLS and creates `asistente_ve_proyectos` with the `educaciones` pattern (the policy names the actor through `identity.asistente_alcanza_a(pf.persona_id)` on the owning profile). A second scaffold (`ComentariosAsistenteProyectos`) adds `COMMENT ON`. The manifests: `proyectos` becomes `concedida` with granted columns and `descripcion` denied (free text, no per-column masking, same reason as `experiencias.descripcion`); the portal schema `motivo` is reworded (it says `proyectos` is not granted); `manifiesto-sensibilidad.json` classifies the columns. `proyecto_documentos` stays denied. The ordering constraint of fact 4 holds: the grants in `001` run after the RLS migration.

**Rejected:** grant `descripcion` (cannot be masked; a project can contain names of third parties).

### D3 — No `canonica_id`; examples teach related terms

Nothing populates `habilidades.canonica_id`, so granting it would add prefix for a column that is always null. Instead, three or four examples in `ejemplos-sql.json` search skills with several related terms through `termino_norm` (`ILIKE ANY` / `IN`): «Cálculo», «Análisis matemático», «Ecuaciones diferenciales»; one joins education and one joins projects. They are disjoint from the eval items (existing disjointness test).

### D4 — Business rules and product record rewritten

`BR-portal-001` and `BR-portal-002` (`docs/business-rules/portal.md`) stop saying «pending Secretaría»: they record the team decision of 2026-11-07 (who decided, what: `jefe_catedra` holds the permission within its scope; Proyectos without descripción), keep the honest note that the decider is not the titular of the data bank, and point to D5 for the unverified legal citations. BR-portal-002 «who may grant» keeps its administrative-boundary statement. `BR-portal-007` is unchanged (option A). `docs/product/consulta-secretaria-portal-asistente.md`: the section «Ampliación del 2026-11-06» (with uncommitted edits made earlier in this session, which are ours and obsolete) becomes a decision record: the jefe gets the permission, option A scope, Proyectos without descripción, and what the system still cannot do. The test `PermisoYPersonaPortalTests.El_permiso_existe_y_no_lo_tiene_ningun_rol` changes to assert exactly `jefe_catedra`.

### D5 — The legal check stays open, and does not block

Task 0.4 of `asistente-lee-portal` (verify the Ley 25.326 citations against the current text; consult the área legal on registering the data bank with the AAIP) was moved here. It is **not done**, owner **team / área legal**, and does not gate the code. The citations in BR-portal-001/002/007 keep their «pending verification» marker.

### D6 — Evaluator

`GeneradorDeFixture` gives the jefe/materia eval actor the permission and portal data (skills with related terms, education with a posgrado, projects with a DOI) for some docentes designated in their materias and one outside. About 5 capacidad items: skills with several terms, posgrado, projects with DOI, a docente outside the cátedra that must not appear, and the coverage sentence; plus 1 or 2 robustez variants. The funded run is not part of this change's implementation: the orchestrator does it.

### D7 — Prompt budget

Measured on 2026-11-07: 12,846 input (+600 output = 13,446) of 16,384. This change adds a table with five columns and its comments, plus the examples; `asistente-lee-aulas` adds its own. The orchestrator re-measures after implementation; if it does not fit, compact comments, then examples (never drop a granted column first).

## Risks / Trade-offs

- **Candidates outside the cátedra are invisible**, which is the common case for filling a vacancy. Mitigation: coverage sentence and abstention text say the answer covers the jefe's cátedra only.
- **The portal is nearly empty**, so most answers will say few docentes loaded anything. Mitigation: coverage is always stated.
- **Free text**: «Cálculo» and «Análisis matemático» are different terms until unified; the examples reduce but do not remove this.
- **The administrative boundary**: the permission can still be granted or revoked from `/roles` (BR-portal-002). The role-level grant is a default, not a barrier.
- **Rollback order**: revoke the role grant first (new incremental Identity migration), then the `proyectos` grants and policy.
