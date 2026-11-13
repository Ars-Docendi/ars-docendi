## Why

Assistant answers already link a trámite number to its detail screen, but a question about people ("who teaches Algoritmos?") or about room requests returns plain names and ids. The Personas screen (with each docente's «Perfil en Mi Portal») and the room-request detail already exist; the user has to leave the assistant and search again.

## What Changes

- **Persona links**: a result that carries `identity.personas.id` links the docente's name to `/personas/:personaId`. A list links each name. An answer about a single docente shows a «Tarjeta de docente» (apellido y nombre, current designaciones as materia · cargo · carrera, «Ver perfil»).
- **Aula request links**: rows carrying `aulas.solicitudes_reserva.id` link to `/aulas/solicitudes/:id`.
- **Internal-key columns**: an id column that only exists to build a link is moved onto the row's label cell and removed from the table, so the user never sees a uuid.
- **No dead links**: the link is emitted only when the actor can open the target. Personas: `personas.ver` (all) or `personas.ver_ambito` with the persona designated in one of the actor's jefatura materias (same rule as `AlcancePersonas`). Aula requests: `aulas.aprobar`, or the request is the actor's own and the actor holds `aulas.solicitar` (same rule as the request detail). Otherwise the same data is shown without a link.
- **Generation hint**: the SQL instructions ask the model to also select the id of the people or requests it lists.

**Out of scope**: aulas schema exposure (`asistente-lee-aulas`), links to other entities, any change to grants, manifests, RLS, domain detection or evaluation datasets.

## Capabilities

### Modified Capabilities

- `asistente-vinculos-del-resultado`: persona and room-request links, internal-key columns, a card for a single docente (added requirements).
- `asistente-superficie-frontend`: the client renders the docente card and the new link kinds (added requirement).

## Impact

- **Backend**: `Modules.Asistente` (`DestinoDelVinculo`, `BuscadorDeVinculos`, `CierreDelTurno`, generation instructions); Host composition (`VinculosDePersonas`, `VinculosDeAulas`, a composite resolver, `FichasDePersonas`); `Modules.Aulas.Contracts.IAulasQueries` gains one query implemented in `Modules.Aulas`.
- **Dependency graph**: Host → `Modules.Aulas.Contracts` (already allowed edge); no Asistente edge.
- **Frontend**: `features/asistente` (`destinos.ts`, `FichaDelElemento`, styles).
- **Docs**: design spec `asistente-conversacional-design-spec.md`, `docs/architecture/api-contracts.md` if it lists ficha keys. There is no Centro de ayuda article for the assistant, and this change adds no new permission or screen, so rule 9 needs no article.
- **Rollback**: pure code; reverting restores unlinked names.
