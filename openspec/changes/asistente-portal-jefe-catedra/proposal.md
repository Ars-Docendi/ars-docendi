## Why

The assistant can search docentes by what they loaded in Mi Portal (Educación, Experiencia, Certificaciones, Habilidades, Intereses), but `portal.ver_trayectoria_ajena` is granted to no role, so only the owner of a profile sees anything. The case that motivated the portal reading, «como jefe de cátedra de Análisis Matemático II, quiero saber qué docentes tienen el perfil para dictarla», cannot be answered by the person who asks it.

The team decided on 2026-11-07 (no Secretaría consultation) to open it to the jefe de cátedra, within the scope the system already enforces: the docentes with a current designación in the materias they head. The Personas screen already shows the jefe the same «Perfil en Mi Portal» for those docentes, so this is parity, not a new exposure.

There is no «requisitos por materia» page. The assistant finds who wrote «Cálculo», not who «meets the requirements». Answers must say so and state coverage («de N docentes, M cargaron…»), which `CoberturaDelPortal` already does.

## What Changes

- **Grant `portal.ver_trayectoria_ajena` to the system role `jefe_catedra`** with a new Identity migration (idempotent, `ON CONFLICT DO NOTHING`). No change to RLS scope: `identity.asistente_alcanza_a(persona)` stays as is.
- **Expose `portal.proyectos` (`nombre`, `rol`, `desde`, `hasta`, `doi`) to both read roles**, never `descripcion` (free text). New RLS policy (same pattern as `educaciones`), `COMMENT ON` for the table and granted columns, both manifests updated.
- **Few-shot examples** in `ejemplos-sql.json` that search skills with several related terms (`termino_norm`), instead of granting `canonica_id` (nothing populates it).
- **Business rules and product record**: BR-portal-001 and BR-portal-002 stop saying «pending Secretaría» and record the team decision of 2026-11-07; the «Ampliación del 2026-11-06» section of `docs/product/consulta-secretaria-portal-asistente.md` becomes a decision record.
- **Measurement**: the evaluator fixture gives the jefe eval actor the permission and portal data (some docentes inside and one outside their materias); about 5 capacidad items and 1 or 2 robustez variants.

**Not done, explicit**: the legal check carried over from `asistente-lee-portal` task 0.4 (Ley 25.326 citations, AAIP registration of the data bank) stays open, owner team / área legal. It does not block the code.

**Out of scope**: candidates outside the jefe's cátedra (accepted limitation, option A), `canonica_id`, CV content, personal contact, `proyecto_documentos`, any compiled-plan change, any new UI.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `portal-visibilidad-asistente`: the permission is held by `jefe_catedra` out of the box; `portal.proyectos` becomes readable without `descripcion` (modifies «Las tablas fuera de alcance no se conceden»; adds two requirements).
- `asistente-prefijo-esquema`: comments for the projects table; the prefix keeps fitting the local profile (added requirements).
- `asistente-eje-capacidad`: the capacity axis covers the jefe-de-cátedra profile search, including a docente outside the cátedra that must not appear (added requirement).
- `asistente-capacidades`: the jefe's catalog announces the docente search (added requirement; the existing permission-driven rule needs no change).

## Impact

- **Blast radius of the grant**: `portal.ver_trayectoria_ajena` is consumed only by the assistant: `ConsultorDeAlcance` (`PermisoDeTrayectoriaAjena`), the portal RLS (`20261020001500`, `20261020001700` through `identity.asistente_alcanza_a`, `database/designaciones/20261103001400_...`), and the evaluator fixture. No endpoint, frontend code or other module reads it, so granting it changes nothing outside the assistant. The Personas screen is governed by `personas.ver_ambito`.
- **Database**: one Identity migration, one Portal migration (RLS plus comments, or two via scaffold), `database/asistente/011_asistente_grants_dominios.sql`, both manifests.
- **`Modules.Asistente`**: `ejemplos-sql.json`; prompt prefix grows by the projects table.
- **Evaluator**: `GeneradorDeFixture`, `capacidad.json`, `robustez.json`; baselines are NOT re-recorded here (funded run belongs to the orchestrator).
- **Docs**: `docs/business-rules/portal.md`, `docs/business-rules/asistente.md` if its examples cite the permission as unassigned, `docs/product/consulta-secretaria-portal-asistente.md`, `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md`, `docs/architecture/domains/portal.md`, `backend/src/Modules.Asistente/README.md`. No Centro de ayuda article about the assistant exists, so rule 9 adds none.
- **Rollback**: revoke the role grant first (a new incremental Identity migration deleting the `jefe_catedra` row of `identity.rol_permisos`; the published migration is never edited). With the grant gone the jefe sees only their own profile again. Then, if needed, revert the `proyectos` grants and policy. Nothing is written to existing data.
- **Sequencing**: `asistente-lee-comisiones` MODIFIES «El alcance se evalúa por dominio consultado» in `asistente-alcance-por-actor`; this change does not touch that requirement. `asistente-lee-aulas` is in flight and edits `database/asistente/*`, the fixture and the datasets: implement after it merges or rebase on it.
