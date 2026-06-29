## Context

SCRUM-7 dejó construida y testeada la fundación de dominio del lado del Jefe de Cátedra: `types.ts` (con el `EstadoPedido` ya completo, incluidas las ramas que recién ejercita la revisión), `maquinaEstados.ts` (`aplicarAccion` puro con `enviar`/`cancelar`/`editar` y un `switch` deliberadamente **no exhaustivo** — el `default` con `never` obliga a TS a marcar las acciones faltantes), el seam `api/` async (store singleton + `localStorage` + React Query), los hooks, `EstadoPedidoBadge`, y el harness Vitest+RTL+jsdom (Fase 0). Las personas mock single-rol ya existen; el role-switching real quedó **diferido** a este change porque en SCRUM-7 no había superficie revisora a la que saltar.

SCRUM-8 **extiende** esa base —no la reescribe— para cerrar el circuito de aprobación. Sigue siendo un **prototipo frontend de alta fidelidad con datos mockeados, sin backend**; el seam `api/` es el único punto con `// TODO(backend)`. Aplican las invariantes #5 (change apply-ready), #7 (no fake UI), #11 (BR con cita + test), #12 (design spec), #13 (código en español). El alcance, la tabla de transiciones (§6.5) y el plan TDD (§8) están fijados en `docs/product/designs/proyecto-docente-frontend-plan.md` y no se re-litigan.

## Goals / Non-Goals

**Goals:**

- Cerrar el flujo de punta a punta: un pedido enviado viaja por Coordinador → Secretaría → Decanato (acepta/rechaza/devuelve), con Administración como revisor sin aprobación, y persiste entre cambios de rol y recargas.
- **Extender la máquina de estados pura** (TDD estricto, red-green por fila/guard de la tabla §6.5) completando el `switch` con `aceptar`/`rechazar`/`devolver`/`reenviar`/`priorizar` y los guards transversales de **etapa** [BR-013] y **ámbito** [BR-009], sin tocar las ramas de SCRUM-7.
- Construir las superficies del revisor (Kanban de triage + detalle role-aware con cadena de aprobación e historial) reusando `@ars-docendi/ui` y construyendo in-app solo lo que la lib no trae (Kanban/Card).
- Role-switching real con un usuario "Demo (todos los roles)" para recorrer la cadena sin re-loguear (defensa + tests de integración).

**Non-Goals:**

- Backend de cualquier tipo (solo mock). Reescribir lo de SCRUM-7 (se EXTIENDE).
- Notificaciones (campana = placeholder), SSO real, File Storage, cross-module Portal real, cadena de hashes de integridad, concurrencia / bloqueo optimista.
- SCRUM-81 (manejo de lotes, Excel, rectificativas, docentes especiales). El estado `en_lote` es terminal-para-el-prototipo (el flujo real seguiría a Universitaria → Aprobado).

## Decisions

### D1 — Extender la máquina de estados completando el `switch` (no reescribir)

`maquinaEstados.ts` suma a la union `AccionPedido` las variantes `{ tipo: "aceptar"; comentario? }`, `{ tipo: "rechazar"; comentario }`, `{ tipo: "devolver"; comentario }`, `{ tipo: "reenviar" }`, `{ tipo: "priorizar"; comentario }`, y completa el `switch` de `aplicarAccion` con una función por acción (`aceptar`, `rechazar`, `devolver`, `reenviar`, `priorizar`), igual que las del lado JC. El `default` con `never` deja de compilar hasta cubrir todas las ramas — esa es la señal de TS que SCRUM-7 dejó preparada.

- **`aceptar`**: avanza por la cadena via un mapa `SIGUIENTE_ETAPA` (`en_revision_coordinador`→`_secretaria`→`_decanato`→`en_lote`). Administración nunca acepta [BR-015].
- **`rechazar`**: cualquier `en_revision_*` → `rechazado` (terminal); `comentario` (justificativo) obligatorio [BR-005, BR-011].
- **`devolver`**: retrocede un nivel via un mapa `ETAPA_ANTERIOR` que fija `estado: "devuelto"`, `etapaRetorno` (la etapa desde la que se devolvió) y `propietarioActual` (quién corrige: JC / Coordinador / Secretaría); `comentario` obligatorio [BR-005, BR-014].
- **`reenviar`**: `devuelto` con `propietarioActual === actor.rol` → `etapaRetorno` [BR-014]. (El JC también puede `editar` antes de reenviar — guard ya existente de SCRUM-7.)
- **`priorizar`**: cualquier estado no-terminal → `prioritario = true` sin cambiar `estado`; `comentario` (motivo) obligatorio [BR-017]. No reabre terminales.

**Guards transversales nuevos**, aplicados antes de cada transición de revisión:

- **Etapa** [BR-013]: solo el revisor cuyo rol corresponde a la etapa actual del pedido puede aceptar/rechazar/devolver. Un mapa `ROL_DE_ETAPA` (`en_revision_coordinador`→`Coordinador`, etc.) define el revisor esperado; Administración es revisor válido en cualquier `en_revision_*` (salvo aceptar).
- **Ámbito** [BR-009]: el Coordinador solo actúa sobre pedidos de su carrera (`pedido.carrera === actor.carrera`); Secretaría/Decanato/Administración son depto-wide. Se valida con un helper `actorAlcanzaAmbito(pedido, actor)` reutilizado por la lectura (`listarPedidosPorAmbito`) y por los guards.

**Por qué**: el dominio es la pieza de mayor valor testeable y la más estable frente al backend; modelarla pura y extender de forma aditiva evita reescritura entre los dos changes encadenados. **Alternativa descartada**: meter la autorización (etapa/ámbito) en la capa `api/` o en los componentes — se descartó porque son reglas de negocio (BR-009/013/015), deben vivir en el dominio puro y testearse sin React ni red.

### D2 — Lectura por ámbito en el seam `api/`

`pedidosApi.ts` suma `listarPedidosPorAmbito(actor)` (filtra el store con `actorAlcanzaAmbito`) y las cinco funciones de acción (`aceptarPedido`/`rechazarPedido`/`devolverPedido`/`reenviarPedido`/`priorizarPedido`), cada una async, que recuperan el pedido, delegan a `aplicarAccion(pedido, accion, actor)` y guardan. Cada función lleva su `// TODO(backend)` con el formato fijo del §7. `contextoActor.ts` extiende el mapa de ámbito para Secretaría/Decanato/Administración (depto-wide, sin `carrera`), manteniendo la firma `construirActorContexto(rol, nombre)`.

**Por qué**: respeta el seam único (regla dura: `// TODO(backend)` solo en `api/`). La autorización real (rol+ámbito) vivirá en el backend (RNF-1); hoy el filtro mock refleja la misma forma. **Trade-off**: el filtro de ámbito se evalúa tanto en lectura (`api/`) como en el guard de dominio (`maquinaEstados`) — duplicación deliberada: la lectura decide visibilidad (qué ves en el Kanban), el guard decide autoridad (qué podés ejecutar); ambas citan BR-009 pero protegen cosas distintas.

### D3 — Kanban de revisión in-app, columnas por estado filtradas por ámbito

La lib no trae Kanban/Card. Se construyen in-app, presentacionales: `PedidoCard` (docente + cátedra + novedad + `EstadoPedidoBadge` + flag prioritario, click → detalle), `ColumnaKanban` (título + cuenta + lista de cards), y `TableroRevision` (orquesta las 4 columnas). Las columnas son **Pendiente (mi etapa)** / **Aprobado** (`en_lote`) / **Rechazado** / **Devuelto**; "Pendiente (mi etapa)" contiene los `en_revision_*` que matchean la etapa del actor (Administración: todos los `en_revision_*`). La página `TableroRevisionPage` consume `usePedidosPorAmbito(actor)` y renderiza Loading/Empty/Error/Success. El CSS sigue los tokens del design system (no se inventan colores).

**Por qué**: decisión grill #5 (triage del revisor = Kanban sin drag). Sin drag porque el avance de etapa es una acción con regla (aceptar/rechazar/devolver), no un movimiento libre de columna — un drag implicaría transiciones sin el comentario obligatorio. **Alternativa descartada**: una tabla filtrable — pierde la lectura de un vistazo del estado del circuito que da el Kanban.

### D4 — Detalle role-aware con mapeo español→símbolos de la lib

`DetallePedidoPage` (`/designaciones/pedidos/:id`) consume `usePedido(id)` + `useActorContexto`. Muestra `DataList` (datos del docente y del pedido, incl. materia y horas de investigación mock), `ApprovalTimeline` (cadena Coordinador→Secretaría→Decanato derivada del estado actual + historial) y `AuditLog` (historial completo), opcionalmente dentro de `Tabs` (Solicitud / Historial / Documentos). Para el revisor de la etapa en su ámbito (predicado `puedeRevisar(pedido, actor)` derivado de los guards del dominio): botonera Aceptar (primary) / Rechazar (destructive) / Devolver (warning) / Marcar prioritario (ghost), cada botón abre `ModalAccionRevision`. JC y demás: solo lectura + timeline.

**Gotcha de mapeo** (la lib está en inglés, el dominio en español): `AuditVerb` (`create|update|attach|approve|return|reject`) y `TimelineStatus` (`done|current|pending|returned|rejected`) son símbolos de la lib (invariante #13, excepción de framework/lib): se usan tal cual. Dos funciones puras de mapeo —`accionAAuditVerb(accion)` (español→`AuditVerb`) y `derivarTimeline(pedido)` (estado+historial→`TimelineStep[]`)— viven **en los componentes de presentación** (`AuditLog`/`ApprovalTimeline` adapters), no en `maquinaEstados.ts`: son traducción de vista, no lógica de dominio, así que no violan la regla del seam ni meten `// TODO(backend)` fuera de `api/`.

**Por qué**: reusar los componentes ya verificados (`ApprovalTimeline`/`AuditLog`/`DataList`/`Tabs` presentes en `v1.0.2`) y concentrar la fricción idiomática en dos funciones puras testeables. **Alternativa descartada**: castear las strings del dominio directamente a los tipos de la lib — rompería en cuanto un verbo no calce (p. ej. `priorizar` no tiene `AuditVerb` propio → se mapea a `update`).

### D5 — `ModalAccionRevision`: comentario obligatorio según la acción

Un único componente in-app reusa `Modal` + `Textarea` + `Button` y recibe la acción (`aceptar`/`rechazar`/`devolver`/`priorizar`). Aplica la regla de comentario [BR-005]: `rechazar`/`devolver`/`priorizar` exigen texto no vacío (bloquean el confirmar y muestran el error inline); `aceptar` lo deja opcional. Al confirmar, dispara la mutation correspondiente de `useAccionesPedido`. La validación de "comentario obligatorio" es la misma regla que el dominio ya exige (la mutation fallaría con `ErrorDominioPedido`); el modal la adelanta en la UI para no pegarle al store con una acción que se va a rechazar (no es fake UI: es validación de entrada, el dominio sigue siendo la autoridad).

**Por qué**: un solo modal parametrizado evita cuatro componentes casi idénticos (archivos chicos, DRY). **Trade-off**: la regla de comentario queda expresada en dos lugares (modal + dominio); se acepta porque el dominio es la fuente de verdad testeada y el modal solo mejora el feedback.

### D6 — Role-switching reactivo: el rol activo vive en `mockSession`, `useCurrentUser` lo observa

Hoy `useCurrentUser()` devuelve `getMockUser() ?? STUB_USER` con rol **fijo**, y `AppLayout` mantiene un `role` local que solo afecta al shell (TopBar/Sidebar) pero **no** llega a `useActorContexto` (que llama a `useCurrentUser` directo). Para que el usuario "Demo (todos los roles)" recorra la cadena y la capa `api/` vea el cambio de rol, el rol activo debe ser global y reactivo:

- `mockSession.ts` suma `setRolActivo(rol)` / `getRolActivo()` persistidos en `localStorage` (clave `adoc.dev.mockRol`), y un mini event-emitter (`suscribirMockSession(cb)`) que notifica cambios. `getMockUser()` aplica el rol activo como override **acotado a `currentUser.roles`** (si el usuario no tiene ese rol, ignora el override).
- `useCurrentUser()` se vuelve reactivo con `useSyncExternalStore` suscrito a `suscribirMockSession`, manteniendo su firma `(): CurrentUser`. Así, al cambiar de rol, **re-renderizan todos los consumidores** (TopBar badge, Sidebar nav, `useActorContexto`, páginas) sin prop-drilling.
- `AppLayout` deja de cablear un `role` local: `onSwitchRole` pasa a `mockSession.setRolActivo`. Se agrega el usuario "Demo (todos los roles)" a `mockUsers.ts` con `roles: [todos]` (su `currentUser.role` arranca en uno por defecto y el `RoleMenu` existente ya renderiza porque `roles.length > 1`).

**Por qué**: es la forma mínima de que el cambio de rol propague a la capa de datos sin reescribir la auth. `useSyncExternalStore` es la API de React 19 para fuentes externas y evita un context provider extra. **Compatibilidad**: la firma de `useCurrentUser` no cambia, así que SCRUM-7 (incluida `MisPedidosPage.test.tsx`) no se rompe. **Alternativa descartada**: un `RolActivoContext` nuevo — más boilerplate y obligaría a envolver el árbol; el store externo es más chico y testeable.

### D7 — Seed extendido para que cada Kanban se vea real

`pedidosSeed.ts` suma pedidos en `en_revision_secretaria`, `en_revision_decanato`, `rechazado`, `en_lote` y de otra carrera (para ejercitar el ámbito del Coordinador [BR-009]), además de los del lado JC ya existentes. Los `EventoHistorial` del seed se escriben con fechas fijas (sin `Date.now()` en el seed) para que el historial y el timeline se vean coherentes.

**Por qué**: invariante #7 (no fake UI / no empty state simulado): cada columna del Kanban de cada revisor debe tener datos demostrables. **Trade-off**: el seed crece; se mantiene legible con el helper `desdeSemilla` ya existente, extendido con los nuevos estados.

## Risks / Trade-offs

- **[El guard de ámbito/etapa se filtra a la UI en vez del dominio]** → Regla dura: la autoridad (quién puede ejecutar) vive en `maquinaEstados.ts` y se testea pura; la UI solo decide visibilidad/affordance con predicados derivados (`puedeRevisar`). El criterio "sin `// TODO(backend)` fuera de `api/`" y el reviewer lo verifican.
- **[Role-switching reactivo rompe tests de SCRUM-7]** → `useCurrentUser` mantiene firma; `setupFiles` ya limpia `localStorage` entre tests, reseteando también el rol activo. Se corre toda la suite (no solo la nueva) antes de cerrar.
- **[Mapeo español→`AuditVerb`/`TimelineStatus` incompleto]** → Funciones puras con `switch` exhaustivo (`never`) y test propio por verbo/estado; un verbo sin mapeo no compila.
- **[Duplicación de la regla de comentario (modal + dominio) diverge]** → El dominio es la fuente de verdad (testeada); el modal es UX. Un test de integración confirma que enviar sin comentario por la UI no muta el store.
- **[El Kanban del Coordinador muestra pedidos de otra carrera]** → `actorAlcanzaAmbito` filtra por carrera; un test de dominio (`coordinadorFueraDeCarreraDenegado`) y uno de la lista cubren visibilidad y autoridad.
- **[Citas normativas de BR-005..017 sin confirmar]** → Son decisiones de proceso (a validar con Secretaría Académica); se implementan y se registran como tales con mapping a test. No bloquean el prototipo.

## Migration Plan

No hay migración de datos ni de API (todo mock, aditivo, frontend-only). Despliegue = merge del/los PR(s) a `develop` sobre la rama `feature/proyecto-docente-flujo-aprobacion` (que ya contiene SCRUM-7). Rollback = revertir el/los PR(s); el store vive en `localStorage` del navegador, así que un rollback de código no corrompe datos del backend (no existe). Orden de entrega (slices reviewables, ≤ ~400 líneas): Fase 1 (extensión de dominio: `maquinaEstados` + guards + su suite TDD) → Fase 2 (seam `api/` + hooks + `contextoActor` + seed) → Fase 3 (UI revisor: Kanban + detalle + modal + routing/nav/gating + role-switching + tests de UI e integración) → Fase 4 (cierre: BR + design spec + `/evaluate`).

## Open Questions

- Nomenclatura definitiva del estado post-Decanato (`en_lote` vs `Aprobado`) — afecta el rótulo de la columna "Aprobado" del Kanban; se deja `en_lote` interno + etiqueta "En lote".
- ¿Administración ve y actúa en TODAS las etapas `en_revision_*` o solo en alguna? Se asume depto-wide en cualquier etapa (puede rechazar/devolver, nunca aceptar) por el §6.5; confirmar con el cliente.
- Concurrencia / bloqueo optimista cuando un pedido cambia de estado con un revisor mirándolo (P-09) — diferido al backend.
