## Why

El Jefe de Cátedra hoy no tiene forma de cargar el proyecto docente de su cátedra dentro del sistema: las designaciones se gestionan por fuera de la herramienta. SCRUM-7 abre el circuito digitalizando la **carga y gestión de pedidos de designación docente** sobre un período abierto (cimiento ya construido: SCRUM-82 Períodos y SCRUM-6 usuarios/roles). Es el primer eslabón del flujo de designaciones y desbloquea SCRUM-8 (circuito de aprobación).

Este change entrega un **prototipo frontend de alta fidelidad con datos mockeados, sin backend**. El objetivo es validar UX, flujo y reglas de negocio con el cliente antes de comprometer el backend. Todo punto de contacto con el backend queda aislado en un único seam (`features/designaciones/api/`) marcado con `// TODO(backend)`, para que el reemplazo posterior por la API real no toque hooks ni componentes.

## What Changes

- **Nueva pantalla "Mis pedidos"** (`/designaciones/mis-pedidos`): el Jefe de Cátedra ve los pedidos de su cátedra para el período abierto, con `StatusBadge` por estado y flag de prioritario. Acciones: Nuevo pedido, Editar (en `borrador` o `devuelto`-a-JC), Cancelar (en `borrador`), Enviar a revisión. Estados de página Loading / Empty / Error / Success. Precarga mock de los docentes del período anterior como "Sin novedad".
- **Nuevo "Form de pedido"** (`/designaciones/pedidos/nuevo` y `/designaciones/pedidos/:id/editar`): campos comunes (docente, antigüedad, cargo/dedicación actual read-only, materia asociada, novedad, flag "hace horas en otro Depto.") + **secciones condicionales por novedad**:
  - Alta → cargo/dedicación solicitados + adjuntos obligatorios CV + DNI frente + DNI dorso **[BR-002]**.
  - Baja → adjunto justificativo obligatorio **[BR-003]**.
  - Cambio de cargo o dedicación → cargo/dedicación solicitados + justificación obligatoria **[BR-004]**.
  - Validación inline; un docente = un pedido por período **[BR-001]**.
- **Modelo de dominio + máquina de estados (núcleo testeable)**: `EstadoPedido` y las transiciones que SCRUM-7 posee — `enviar` (`borrador` → `en_revision_coordinador`), `cancelar` (`borrador` → `cancelado`), `editar` (`borrador`/`devuelto` del propietario, sin cambio de estado) **[BR-008]**. Lógica pura en `maquinaEstados.ts` con guards, bajo **TDD estricto** (red-green). Las transiciones de revisión (aceptar/rechazar/devolver/priorizar) las agrega SCRUM-8.
- **Seam de datos mock**: capa `api/` async (Promise + latencia) sobre un store singleton persistido en `localStorage`, consumido vía **React Query** (primera feature que usa React Query de verdad). El flujo persiste entre cambios de rol y recargas.
- **Harness de tests (Fase 0, habilitante)**: bootstrap de Vitest + Testing Library + jsdom + user-event (hoy el frontend no tiene runner). Resuelve la deuda técnica del "runner frontend TBD".
- **Registro de reglas de negocio**: nuevo `docs/business-rules/designaciones.md` con BR-001..BR-008 (cita normativa pendiente de confirmación del cliente para BR-001..004; las de proceso quedan citadas como decisión de proceso), cada una con mapping a test **(invariante #11)**.
- **Design spec**: nuevo `docs/product/designs/proyecto-docente-design-spec.md` (Mis pedidos + form) **(invariante #12)**.
- **NO incluye backend**: sin controllers, services, EF, PostgreSQL ni endpoints reales. Sin notificaciones, SSO real, File Storage, ni cross-module Portal real (todo mock inline o diferido con `// TODO(backend)`).

## Capabilities

### New Capabilities

- `pedidos-designacion`: carga y gestión de pedidos de designación docente por el Jefe de Cátedra dentro de un período abierto — lista "Mis pedidos", form de alta/edición con secciones condicionales por novedad y validaciones (BR-001..004, BR-008), máquina de estados del lado del JC (`borrador` → `enviar`/`cancelar`/`editar`), y el seam de datos mock (store + `localStorage` + capa `api/` async + React Query) que persiste el flujo entre roles y recargas. Las transiciones de revisión (aprobación/rechazo/devolución/prioridad) quedan para la capability `aprobacion-pedidos-designacion` (SCRUM-8).

### Modified Capabilities

<!-- Ninguna. gestion-periodos (SCRUM-82) y los usuarios/roles (SCRUM-6) son cimiento ya construido y no cambian sus requisitos; este change los consume sin modificarlos. -->

## Impact

- **Módulo afectado**: Designaciones (solo frontend en este change). Sin cambios en backend, Contracts, ni en el grafo de dependencias (`docs/architecture/dependency-graph.md` no se toca: no hay edges nuevos ni cambios de API real).
- **Frontend** — `frontend/src/features/designaciones/`:
  - `api/`: nuevos `pedidosStore.ts`, `pedidosApi.ts` (seam, `// TODO(backend)`), `pedidosSeed.ts`, `maquinaEstados.ts`.
  - `components/`: nuevos `PedidoForm.tsx`, `TablaMisPedidos.tsx`, `EstadoPedidoBadge.tsx`.
  - `hooks/`: nuevos `usePedidos.ts`, `useAccionesPedido.ts` (React Query).
  - `pages/`: nuevas `MisPedidosPage.tsx`, `PedidoFormPage.tsx`.
  - `routes.tsx` y `types.ts`: extendidos. App shell: `nav.ts` (ítem "Mis pedidos" para JC) y `mockUsers.ts` (personas single-rol + Demo multi-rol).
- **Tooling**: `frontend/package.json` suma devDeps de testing + scripts `test`/`test:run`; nuevo `vitest.config.ts`. CI debería correr `test:run` además de lint + build.
- **Dependencias**: usa `@ars-docendi/ui` (pin `release/v1.0.2`) — exports verificados, cubren el form completo (Field, Input, Select, Radio, Textarea, FileUpload, InlineAlert, Button, StatusBadge, Table). `@tanstack/react-query` ya instalado.
- **Docs en el mismo PR (invariante #6 no aplica a schema/API porque todo es mock)**: se crean `docs/business-rules/designaciones.md` (invariante #11) y `docs/product/designs/proyecto-docente-design-spec.md` (invariante #12); se actualiza `docs/quality/tech-debt.md` (runner frontend resuelto).
- **Riesgo / rollback**: bajo. Es aditivo y frontend-only; no migra datos ni rompe APIs. Rollback = revertir el/los PR(s). Las citas normativas de BR-001..004 quedan como punto abierto a confirmar con el cliente (no bloquean el prototipo).
- **Encadenamiento**: primer change de dos. SCRUM-8 (`flujo-aprobacion-designaciones`) extiende la máquina de estados y la capability con el circuito de revisión.
