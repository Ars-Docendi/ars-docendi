## 1. Database and backend

- [x] 1.1 Scaffold the Aulas migration with `scaffold.mjs`, add `motivo_cancelacion` and `rol_solicitante` plus the CHECK, and keep the EF mapping and snapshot coherent. Verify: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, `--validar-recursos`.
- [x] 1.2 Add `GET /api/aulas/solicitudes/{id}` (owner or `aulas.aprobar`, otherwise 404) and the `MotivoCancelacion` / `EsPropia` fields. Verify: integration test for owner, approver, other requester, role without permissions and unknown id.
- [x] 1.3 Add `POST /api/aulas/solicitudes/{id}/cancelar-reserva` (`aulas.aprobar`). Verify: integration tests for success, blank reason (400 `motivo`), non-approved (409), role policy (403) and the CHECK rejecting a reason outside `cancelada`.
- [x] 1.4 Persist `rol_solicitante` on creation. Verify: integration test reads the stored role.

## 2. Shared UI

- [x] 2.1 Add the pure date/time helpers and their unit tests. Verify: `camposFechaHora.test.ts`.
- [x] 2.2 Add `CampoFecha` and `CampoHora` and the shared form CSS. Verify: `CampoFecha.test.tsx` (mask, native picker, `showPicker` failure).
- [x] 2.3 Add the layer stack and `ModalDeFormulario` with its CSS. Verify: `ModalDeFormulario.test.tsx` (focus, trap, Esc on the topmost only, pending lock, overlay press/release, dirty question, focus return).

## 3. Aulas frontend

- [x] 3.1 Write the failing regression test for the stale "Nueva solicitud" form, then rebuild `ModalNuevaSolicitud` (sections, per-field validation, focus on the first invalid field, mounted per opening). Verify: `IndexPage.test.tsx` red before and green after; `validacionNuevaSolicitud.test.ts`; `ModalNuevaSolicitud.test.tsx`.
- [x] 3.2 Update the API functions, hooks and types for the detail and the cancel-reservation endpoint.
- [x] 3.3 Rebuild Rechazar, Cancelar solicitud and the new Cancelar reserva on the shell; remove `ModalAsignarAula` and `ModalDetalleSolicitud`. Verify: `DetalleSolicitudPage.test.tsx`.
- [x] 3.4 Add the route `/aulas/solicitudes/:id` and `DetalleSolicitudPage` (data card, actions panel, header, "Volver a Reserva de aulas" to `/aulas`). Verify: `DetalleSolicitudPage.test.tsx` (loading, error, owner, approver, closed, reasons).
- [x] 3.5 Make both tables open the detail with one click, with a real link in the primary cell and no double click. Verify: `TablasSolicitudes.test.tsx`.
- [x] 3.6 Fix the sidebar entry of "Reserva de aulas" to require `aulas.solicitar` or `aulas.aprobar`. Verify: `Sidebar.test.tsx`.

## 4. Documentation and close-out

- [x] 4.1 Update `api-contracts.md`, `data-model.md`, `domains/aulas.md`, `reserva-aulas-design-spec.md` and note the superseded decision 8 in `reserva-aulas/design.md`. Verify: review of the diff.
- [x] 4.2 Run the full verification (migrations, build, `--validar-recursos`, backend tests, frontend tests, lint, build, `pnpm format:check`, `openspec validate --strict`) and a security pass. Verify: all green or any pre-existing failure proven.
