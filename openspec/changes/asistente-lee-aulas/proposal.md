## Why

The assistant now reads `identity`, `designaciones`, `portal` and `comisiones`. It cannot answer what the Aulas screen already shows and the Departamento asks every exam period: which reservation requests are pending, which aula was assigned to an approved request, how many requests were rejected per materia, and (for a jefe de cátedra) what happened to the requests they made.

The data is one table, `aulas.solicitudes_reserva`. The assistant's manifest lists the whole `aulas` schema as `denegado` with the reason "no question of the catalog needs it". That stopped being true once the Departamento asked for it.

## What Changes

- **Expose `aulas.solicitudes_reserva` to both read roles**, every column granted column by column and listed in `manifiesto-privilegios.json`, every column classified in `manifiesto-sensibilidad.json`. `motivo_rechazo` and `motivo_cancelacion` are `sensible-texto` (shown to the user, never sent to the model); the rest is `publica`.
- **RLS on the table** (`ENABLE`, not `FORCE`), one `FOR SELECT` policy that mirrors the Aulas screen: `aulas.aprobar` sees every request, `aulas.solicitar` sees only the actor's own.
- **`COMMENT ON` for the table and every column**, stating that these are reservation requests (not the aula where a comisión is taught), that there is no aula catalog, and when each nullable column exists.
- **New domain `Aulas` in the abstention rule**: "zero rows means none" holds only for an actor with `aulas.aprobar`. An actor with only `aulas.solicitar` gets an answer scoped to their own requests and told so; without either permission the policy returns nothing.
- **Disambiguation with comisiones**: "in which aula is X taught" keeps going to `comisiones.sga_comision_horarios`; the requests table is used only when the question says reserva, solicitud or pedido de aula, or a request state.
- **Capability catalog** announces aulas questions by the live permissions: approvers hear "all requests", jefe de cátedra hears "your requests".
- **Measurement**: requests in the synthetic fixture in all four states, new capacidad and robustez items, three SQL examples, prefix size before and after.

**Out of scope**: "which aulas are free" and aula capacity (no catalog, they stay non-answerable: `cap-048`, `cap-049` unchanged), links and cards to `/aulas/solicitudes/:id` and `/personas/:personaId` (separate change), the compiled plan, any change to the Aulas DDL, screen or API.

## Capabilities

### New Capabilities

- `aulas-visibilidad-asistente`: who can read reservation requests through the assistant, what is exposed, and how an answer built on them is scoped and declared.

### Modified Capabilities

- `asistente-alcance-por-actor`: a separate added requirement for the aulas domain rule (not a MODIFIED of the existing one, which the still-active `asistente-lee-comisiones` change already modifies).
- `asistente-capacidades`: the catalog announces aulas questions per the live permissions (added requirement).
- `asistente-prefijo-esquema`: the aulas comments carry the facts the model needs, and the prefix stays within the local profile budget (added requirement).
- `asistente-eje-capacidad`: the capacity axis covers reservation requests, including items that must abstain (added requirement).

## Impact

- **Database**: `database/asistente/011_asistente_grants_dominios.sql` and both manifests; two new migrations in the Aulas chain (RLS, then comments). No change to the `aulas` DDL.
- **`Modules.Asistente`**: `DetectorDeDominios` (`Aulas` flag), `PoliticaDeAbstencion`, `PerfilDelActor` (two flags read live in `ConsultorDeAlcance`), `EtiquetasDeAreas`, `PresentacionPorRol`, `CatalogoDeCapacidades`, `ejemplos-sql.json`.
- **Evaluator**: `GeneradorDeFixture`, `capacidad.json`, `robustez.json`; baselines are re-recorded by the funded run, not here.
- **Dependency graph**: no new project reference. The boundary is the database engine (AGENTS.md rule 11), not `Contracts`.
- **Docs**: `docs/architecture/domains/asistente.md`, `docs/architecture/data-model.md`, `backend/src/Modules.Asistente/README.md`, `docs/business-rules/asistente.md` (BR-asistente-011). There is no Centro de ayuda article about the assistant, so rule 9 adds none.
- **Rollback**: revert the grants (001 and manifests) first, then the RLS and comment migrations. Nothing is written to existing data.
- **Sequencing**: applies on top of `asistente-lee-comisiones`, which is still active; both extend `PoliticaDeAbstencion` and `DominioConsultado`.
