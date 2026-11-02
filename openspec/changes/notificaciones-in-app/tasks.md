## 1. Identity queries

- [x] 1.1 Add to `IConsultasIdentity` an efficient query for active users holding a system role in a scope (materia, carrera or global) and a persona to usuario lookup, both resolved in SQL. Verify: integration tests for each scope, inactive user, inactive role, soft-deleted assignment and persona without usuario.

## 2. Notificaciones module

- [x] 2.1 Scaffold `Modules.Notificaciones` and `Modules.Notificaciones.Contracts` (projects, solution, Host reference, `AddNotificacionesModule`, ping, smoke script). Verify: build and the solution lists both projects.
- [x] 2.2 Scaffold the first migration with `scaffold.mjs` (schema, table, index) and keep the EF mapping and snapshot coherent; register the context in `comun.mjs`. Verify: `node --test scripts/tests/migrations.test.mjs`, `node scripts/migrations/validate.mjs`, `--validar-recursos`, `MigracionesRuntimeTests` inventory counts.
- [x] 2.3 Implement `INotificador` (best effort, own scope and context) and the repository/service/controller of the API. Verify: integration tests for every endpoint with 401, 400, 404, idempotence, profile filtering, `todas` + `hasta`, several jefes with independent state and a throwing writer.

## 3. Emitters

- [x] 3.1 Aulas: emit after commit on assign, update, reject, cancel reservation and teacher cancel. Verify: integration tests per event, including same-room no-op, persona without usuario and a throwing notifier leaving the action committed.
- [x] 3.2 Designaciones: emit after commit on accept, reject and return. Verify: integration tests for each stage, `en_lote` sending nothing and the owner role scope.
- [x] 3.3 Tareas: emit on create, reassignment, state change and comment. Verify: integration tests including actor exclusion.
- [x] 3.4 Architecture tests: emitting modules reference only `Modules.Notificaciones.Contracts`. Verify: `ArquitecturaIdentityTests` stays green.

## 4. Frontend

- [x] 4.1 Add `features/notificaciones` API, keys (with the active role), queries, mutations with optimism and rollback, pure helpers (grouping, selection, badge text). Verify: unit tests for helpers and hooks against intercepted HTTP.
- [x] 4.2 Add the toast host, the bell (sidebar and top bar) and the panel with tabs, groups, states, selection and infinite scroll; wire it into the shell. Verify: component tests for bell states, tabs, groups, states, selection, rollback and profile key.
- [x] 4.3 Navigate and highlight on click; highlight on the three detail pages. Verify: component test for navigation.
- [x] 4.4 Copy: "Cancelar reserva" dialog and `ModalConfirmacionAccion`. Verify: updated tests.

## 5. Documentation and close-out

- [x] 5.1 Update `dependency-graph.md`, `module-anatomy.md`, `api-contracts.md`, `data-model.md`, `domains/notificaciones.md`, the infrastructure smoke list and the affected design specs.
- [x] 5.2 Run the full verification and the security pass. Verify: all green or any pre-existing failure proven.
