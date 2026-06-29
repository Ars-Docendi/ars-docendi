## Why

Tras SCRUM-7, el Jefe de Cátedra ya carga y envía pedidos de designación, pero la cadena que los aprueba no existe: un pedido enviado queda en `en_revision_coordinador` sin que ningún rol pueda avanzarlo, rechazarlo ni devolverlo. SCRUM-8 cierra el circuito digitalizando el **flujo de aprobación** (Coordinador de Carrera → Secretaría Académica → Decanato, con Administración como revisor sin poder de aprobación), de modo que el prototipo sea demostrable de punta a punta y desbloquee SCRUM-81 (manejo de lotes).

Este change es el **segundo de dos encadenados** y, como SCRUM-7, entrega un **prototipo frontend de alta fidelidad con datos mockeados, sin backend**. **EXTIENDE** la fundación de dominio de SCRUM-7 (máquina de estados, seam `api/`, store, hooks) sin reescribirla. Todo punto de contacto con el backend sigue aislado en `features/designaciones/api/` con `// TODO(backend)`.

## What Changes

- **Máquina de estados extendida (núcleo testeable, TDD estricto)**: se suman a `maquinaEstados.ts` las transiciones de revisión, sin tocar las del lado JC:
  - `aceptar`: `en_revision_coordinador` → `_secretaria` → `_decanato` → `en_lote` (terminal-prototipo). **Administración NUNCA acepta** **[BR-015]**.
  - `rechazar`: `en_revision_*` → `rechazado` (terminal); justificativo **obligatorio** **[BR-005, BR-011]**.
  - `devolver`: retrocede un nivel (set `etapaRetorno` + `propietarioActual`); comentario **obligatorio** **[BR-005, BR-014]**.
  - `reenviar`: `devuelto` (propietario = actor) → `etapaRetorno` **[BR-014]**.
  - `priorizar`: cualquier no-terminal, `prioritario = true` sin cambiar estado; justificativo **obligatorio** **[BR-017]**.
  - Guards transversales nuevos: **etapa** (solo el revisor de la etapa actual actúa) **[BR-013]** y **ámbito** (Coordinador solo su carrera; Secretaría/Decanato/Administración todo el depto) **[BR-009]**.
- **Tablero de revisión (Kanban sin drag)** (`/designaciones/revision`): columnas **Pendiente (mi etapa)** / **Aprobado** / **Rechazado** / **Devuelto**, filtradas por ámbito **[BR-009]**. `PedidoCard` (docente + cátedra + novedad + `StatusBadge` + flag prioritario) → click al detalle. `ColumnaKanban`, `PedidoCard` se construyen in-app (la lib no trae Kanban).
- **Detalle del pedido** (`/designaciones/pedidos/:id`): vista role-aware con `DataList` + `ApprovalTimeline` + `AuditLog` (+ `Tabs` Solicitud/Historial/Documentos). Para el revisor de la etapa en su ámbito: botones **Aceptar** (primary) / **Rechazar** (destructive) / **Devolver** (warning) / **Marcar prioritario** (ghost) que abren `ModalAccionRevision` (Modal + Textarea con la regla de comentario **[BR-005]**). JC y demás roles: solo lectura + timeline.
- **Seam `api/` extendido**: `aceptarPedido` / `rechazarPedido` / `devolverPedido` / `reenviarPedido` / `priorizarPedido` (async, delegan a `aplicarAccion`) + `listarPedidosPorAmbito(actor)` para los revisores; cada una con su `// TODO(backend)`. `contextoActor.ts` suma el ámbito depto-wide de Secretaría/Decanato/Administración.
- **Hooks React Query**: `useAccionesPedido.ts` suma las mutations de revisión (invalidan `["pedidos"]`); `usePedidos.ts` suma `usePedidosPorAmbito`.
- **Routing / nav / gating**: ruta `revision` (gate Coordinador/Secretaría/Decanato/Administración con `RequireRole`) y `pedidos/:id` (cualquier rol, visibilidad por ámbito, acciones gated por etapa). `nav.ts` suma el ítem "Revisión" para los revisores (sin links muertos, invariante #7).
- **Role-switching real (diferido de SCRUM-7)**: usuario **"Demo (todos los roles)"** + `mockSession.ts` extendido para trackear el rol activo de un usuario multi-rol, usando el `RoleMenu` existente para recorrer la cadena sin re-loguear.
- **Tests de integración (RTL sobre el store mock)**: happy-path (JC envía → Coordinador → Secretaría → Decanato → `en_lote`) y devolución (Coordinador devuelve → JC reenvía → vuelve a `en_revision_coordinador`).
- **Registro de reglas de negocio**: se agregan a `docs/business-rules/designaciones.md` las BR-005/009/011/013/014/015/017 (hoy listadas como "Pendientes (SCRUM-8)") con mapping a test **(invariante #11)**.
- **Design spec**: se extiende `docs/product/designs/proyecto-docente-design-spec.md` con tablero de revisión + detalle + timeline **(invariante #12)**.
- **NO incluye backend**: sin controllers/services/EF/PostgreSQL ni endpoints reales. Sin notificaciones (campana placeholder), SSO real, File Storage, ni cross-module Portal real (horas de investigación mock inline). Sin SCRUM-81 (lotes/Excel/rectificativas/docentes especiales).

## Capabilities

### New Capabilities

- `aprobacion-pedidos-designacion`: circuito de aprobación de pedidos de designación por la cadena Coordinador → Secretaría → Decanato (+ Administración como revisor sin aprobación). Cubre las transiciones de revisión de la máquina de estados (`aceptar`/`rechazar`/`devolver`/`reenviar`/`priorizar`) con sus guards de etapa **[BR-013]** y ámbito **[BR-009]** (BR-005/011/014/015/017), el tablero Kanban de triage filtrado por ámbito, el detalle role-aware con cadena de aprobación e historial, y el seam `api/` + hooks de revisión. Construida **sobre** la capability `pedidos-designacion` (SCRUM-7), que reservó explícitamente estas transiciones para este change.

### Modified Capabilities

<!-- Ninguna a nivel de spec. La capability `pedidos-designacion` (SCRUM-7) reservó
     explícitamente las transiciones de revisión para `aprobacion-pedidos-designacion`,
     y su spec vive en un change aún no archivado (no está en openspec/specs/). Todo el
     comportamiento nuevo de revisión se modela como capability nueva; el código de la
     máquina de estados se EXTIENDE de forma aditiva (nuevas ramas del switch + guards),
     sin cambiar los requisitos ya especificados del lado JC. -->

## Impact

- **Módulo afectado**: Designaciones (solo frontend en este change). Sin cambios en backend, Contracts ni en el grafo de dependencias (`docs/architecture/dependency-graph.md` no se toca: no hay edges nuevos ni cambios de API real).
- **Frontend** — `frontend/src/features/designaciones/`:
  - `api/`: se EXTIENDEN `maquinaEstados.ts` (ramas `aceptar`/`rechazar`/`devolver`/`reenviar`/`priorizar` + guards etapa/ámbito), `pedidosApi.ts` (funciones de revisión + `listarPedidosPorAmbito`, con `// TODO(backend)`) y `contextoActor.ts` (ámbito depto-wide). `pedidosSeed.ts` suma pedidos en etapas de revisión para que cada Kanban se vea real.
  - `components/`: nuevos `TableroRevision.tsx`, `ColumnaKanban.tsx`, `PedidoCard.tsx`, `ModalAccionRevision.tsx`.
  - `hooks/`: se EXTIENDEN `useAccionesPedido.ts` (mutations de revisión) y `usePedidos.ts` (`usePedidosPorAmbito`).
  - `pages/`: nuevas `TableroRevisionPage.tsx`, `DetallePedidoPage.tsx`.
  - `routes.tsx`: se extiende (rutas `revision` + `pedidos/:id`). App shell: `nav.ts` (ítem "Revisión" para revisores), `shared/auth/dev/mockUsers.ts` (usuario Demo multi-rol) y `shared/auth/dev/mockSession.ts` (tracking del rol activo).
- **Dependencias**: usa `@ars-docendi/ui` (pin `release/v1.0.2`) — exports verificados: `ApprovalTimeline`, `AuditLog`, `Tabs`, `Drawer`, `Modal`, `Textarea`, `DataList`, `StatusBadge` presentes con sus tipos (`TimelineStep`/`TimelineStatus`, `AuditEntry`/`AuditVerb`). `@tanstack/react-query` y el harness Vitest+RTL+jsdom ya están (Fase 0 de SCRUM-7). No hay bump de versión.
- **Gotcha de mapeo (lib en inglés ↔ dominio en español)**: `AuditVerb` (`create|update|attach|approve|return|reject`) y `TimelineStatus` (`done|current|pending|returned|rejected`) son símbolos de la lib: se usan tal cual. La `accion` del `EventoHistorial` va en español; se mapea español→`AuditVerb` al alimentar `AuditLog` y la cadena de etapas→`TimelineStep[]` al alimentar `ApprovalTimeline`. El mapeo vive en componentes in-app, no es lógica de dominio (no rompe la regla del seam).
- **Docs en el mismo PR**: invariante #6 no aplica (todo mock, sin schema/API real). Se actualizan `docs/business-rules/designaciones.md` (invariante #11: BR-005/009/011/013/014/015/017) y `docs/product/designs/proyecto-docente-design-spec.md` (invariante #12: tablero + detalle + timeline).
- **Riesgo / rollback**: bajo. Aditivo y frontend-only; no migra datos ni rompe APIs. Rollback = revertir el/los PR(s). Las citas normativas de BR-005..017 son decisiones de proceso (a validar con Secretaría Académica); no bloquean el prototipo.
- **Encadenamiento**: construido sobre la rama `feature/proyecto-docente-flujo-aprobacion`, que ya contiene SCRUM-7. PRs work-unit por fase (≤ ~400 líneas, skill `chained-pr`).
