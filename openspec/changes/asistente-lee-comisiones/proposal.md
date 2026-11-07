## Why

The assistant answers over `identity`, `designaciones` and `portal`. It cannot answer what the Departamento asks every term and what the Comisiones screen already shows: which comisiones a docente has, on which days, times and aulas; who teaches comisión 02 of a materia and with what responsabilidad; how many comisiones each docente carries; which comisiones run at night or on Saturdays.

The data exists since PR #58: the `comisiones` schema is a SIU-Guaraní snapshot managed by `Modules.Designaciones`. The assistant's manifest lists it as `denegado` with the reason "no question of the catalog needs it". That reason stopped being true when the snapshot was merged. It also unlocks the cross-checks that matter for payroll and accreditation: a docente who teaches a comisión with no current designación in that materia, and the reverse.

## What Changes

- **Expose four `comisiones` tables to both read roles**: `sga_comisiones`, `sga_comision_carreras`, `sga_comision_docentes`, `sga_comision_horarios`. Column-by-column `GRANT`, listed in `manifiesto-privilegios.json`, every column classified in `manifiesto-sensibilidad.json`.
- **`sga_comision_docentes.nombre` is granted and classified `publica`**, like `identity.personas.nombre`, because `persona_id` is nullable and docentes not matched to a persona would otherwise vanish from answers.
- **`sga_importaciones` stays denied**, with its reason rewritten: it is an operational import log that names the importing user and answers no catalog question.
- **RLS on the four exposed tables**, predicate `identity.asistente_tiene_permiso('comisiones.ver')` and no scope. It mirrors the Comisiones screen, which shows every comisión to anyone holding the permission.
- **`COMMENT ON` for every granted object**, which is what lets the model map "comisión 02", "turno noche" and "los sábados" to columns.
- **Abstention by domain**: "zero rows means there are none" now depends on which domains the query touched. A comisiones-only query reaches everything iff the actor has `comisiones.ver`; a query that also touches `designaciones` must satisfy that domain's rule too.
- **Empty comisiones answers say which períodos are imported**, because an empty result can mean "that período was never imported".
- **Capability catalog announces comisiones questions** only to actors who hold `comisiones.ver`.
- **Measurement**: comisiones rows in the synthetic fixture, new capacidad and robustez items (including non-answerable ones), baselines regenerated, and a prefix-size measurement against the local 3070 profile.

**Out of scope**: aulas as an entity (`asistente-lee-aulas`), portal access for jefe de cátedra, links and cards to `/designaciones/comisiones`, the compiled plan (hardwired to docentes with a designación; comisiones go through text-to-SQL), and "which aulas are free" questions (`aula` is free text with no catalog, so they stay non-answerable).

## Capabilities

### New Capabilities

- `comisiones-visibilidad-asistente`: who can read the Guaraní comisiones snapshot through the assistant, what is exposed, what stays denied, and how a comisiones answer is scoped and declared.

### Modified Capabilities

- `asistente-capacidades`: the catalog announces comisiones questions only to actors with `comisiones.ver` (added requirement).
- `asistente-prefijo-esquema`: the comisiones comments carry the facts the model needs, and the prefix stays within the local profile budget (added requirement).
- `asistente-eje-capacidad`: the capacity axis covers the comisiones domain, including items that must abstain (added requirement).

## Impact

- **Database**: `database/asistente/011_asistente_grants_dominios.sql` and both manifests; two new migrations in the Designaciones chain for RLS and comments (see design D4). No change to the comisiones DDL.
- **`Modules.Asistente`**: `PoliticaDeAbstencion`, `PerfilDelActor`, `ConsultorDeAlcance`, `CarrilSql`, `CarrilDelPlan` (call-site only), `EtiquetasDeAreas`, `ejemplos-sql.json`, a small coverage reader for imported períodos.
- **Evaluator**: `GeneradorDeFixture`, `capacidad.json`, `robustez.json`, baselines. Existing item `cap-019` ("¿En qué aula se dicta Sistemas Operativos?") stops being non-answerable and must be reclassified.
- **Dependency graph**: no new project reference. The boundary is the database engine (AGENTS.md rule 11), not `Contracts`.
- **Docs**: `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md`, `backend/src/Modules.Asistente/README.md`, `docs/business-rules/asistente.md` (BR-asistente-001 and 003 wording). There is no Centro de ayuda article about the assistant, so rule 9 needs no new article.
- **Rollback**: revert the grants (001 and manifests) first, then the RLS and comment migrations. With the grants gone the assistant cannot read the schema even if RLS remains. Nothing is written to existing data.
- **Sequencing**: `asistente-fundaciones`, `asistente-lee-portal` and `asistente-portal-por-ambito` were archived on 2026-11-06, so the per-domain abstention is a MODIFIED delta on `asistente-alcance-por-actor` (see design D6).
