## Context

The result-link port (`IResolutorDeVinculos`) maps cell text to a destination; the Host composes it with the owning module's authority. Types are names, the client owns routes.

## Decisions

- **D1 Types**: `persona` and `solicitud-reserva-aula`. Client routes: `/personas/:id`, `/aulas/solicitudes/:id`.
- **D2 Ids as candidates**: uuid cells are already candidates (length 36, no spaces). The composite resolver in the Host asks each adapter; ids are random uuids so adapters never collide.
- **D3 Internal key**: `DestinoDelVinculo` gets `EsClave`. When set, `CierreDelTurno` moves the link to the first non-key text cell of the row (the label), and removes the key column from columns, sensitivity and rows. If a row has no label cell the key column stays visible and linked. Text-position links are computed before the move, so a surname repeated in two people never links to the wrong one.
- **D4 Persona authorization**: `VinculosDePersonas` mirrors `PersonasController`: `AlcancePersonas.MateriasVisiblesAsync` (null = all) then `ServicioAdministracionPersonas.EstaEnAmbitoAsync` per id. No permission or an out-of-scope id yields no link.
- **D5 Aula authorization**: `IAulasQueries.SolicitudesAbribles(ids)` lives in `Modules.Aulas` and reuses the detail rule (`aulas.aprobar` OR own + `aulas.solicitar`). The resolver is keyed on the id only; it does not depend on the assistant grant existing.
- **D6 Card**: `Principal` also applies when every link points to the same persona (several designaciones rows). Trámites keep the one-row rule. `FichasDePersonas` returns apellido, nombre and current designaciones within the actor's scope; plus the institutional email (account UPN) under the same gate; never document, phone or personal email. The ficha is built in the turn closer after narration and only reaches the client.
