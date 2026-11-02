## 1. Navigation core

- [x] 1.1 `resolverVolver` with per-entity allowlist, `EnlaceVolver`, `useEstadoVolver`; replaces `origenDetalle`. Verify: unit tests for allowlist, forged paths and fallbacks; the Mis pedidos, edit, detail loop keeps passing.
- [x] 1.2 URL-state hooks (`useFiltrosURL`, `useOrdenURL`, `usePestanaURL`). Verify: unit tests for defaults omitted, arrays, prefixes, replace versus push.
- [x] 1.3 Scroll restoration in the shell. Verify: unit test of save/restore/reset rules.
- [x] 1.4 Central blocker (`GestorDeNavegacion`), guard store, shared dialog, overlay stack with Back handling. Verify: tests with `createMemoryRouter`.
- [x] 1.5 Page chrome: title, focus, live region, skip link, 404, no access, `errorElement`. Verify: router tests.

## 2. Volver adoption

- [x] 2.1 Task detail (red test: deep link and child task), project detail, pedido detail/edit/delete, solicitud detail, persona and rol editors, error alerts with `Link`. Verify: tests per page.

## 3. List state in the URL

- [x] 3.1 Aulas (two tables), Mis pedidos, Revisión, Personas, Tareas, Proyectos, Períodos, Roles. Verify: a test that filter plus sort survive back, and one per prefix collision.

## 4. Accessibility

- [x] 4.1 `Pestanas` wrapper in Revisión and Personas. Verify: keyboard test.
- [x] 4.2 Rows: real link in the primary cell, `propsFilaClickeable`, focus ring; notification highlight on list rows. Verify: tests for Tareas and Proyectos rows.

## 5. Unsaved changes

- [x] 5.1 Guards in pedido form, persona and rol editors, solicitud aula field, pedido justification, task detail fields. Verify: leaving dirty asks; saving does not.

## 6. Overlays and fields

- [x] 6.1 `Modal`/`Drawer` wrappers and migration of every modal, the mobile menu and the notifications panel; `sucio`/`pendiente` per form. Verify: stacked Esc, focus return, Back closes the top-most overlay.
- [x] 6.2 `CampoFecha`/`CampoHora` replace native and library pickers; form modals use the shared form classes. Verify: ISO values reach the mutation.

## 7. Routes and login

- [x] 7.1 `RequirePermission` renders no-access in place; `path: "*"`; pedido edit route guarded.
- [x] 7.2 Post-login return keeps path, query and hash (same-origin relative only). Verify: red test first.
- [x] 7.3 Read the backend Microsoft `target` validation; fix only what is needed.

## 8. Responsive

- [x] 8.1 Aulas pages and modals, solicitud detail and notifications panel from 360 px (portal approach).

## 9. Documentation

- [x] 9.1 `docs/architecture/frontend-conventions.md`, design specs and `frontend-remote-data.md` pointer; `openspec validate navegacion-consistente --strict`.

## 10. Validation against the prototype (real browser)

- [x] 10.1 Fixes found by driving the real app: initial focus kept under StrictMode in modals and in the notifications panel; focus follows a replaced `h1`; page title no longer right-aligned below 560 px; marca and breadcrumb links reach 24 px; aula field 44 px; text filters in the URL keep every letter; Tareas "cuadros" in the URL; "Nuevo pedido" cancel keeps "Mis pedidos" filters; role change and notification click ask before leaving a dirty page. Verify: Vitest regression test per fix and the browser matrix at 1200, 400 and 360 px.
- [x] 10.2 Changing the role from a role-gated page lands on the new role's home: the navigation no longer depends on the modal staying mounted while the session reloads (TD-011). Verify: `AppLayout.cambioDeRol.test.tsx` with the real layout (development and Microsoft sessions, dirty form, failed change) and the real browser.
