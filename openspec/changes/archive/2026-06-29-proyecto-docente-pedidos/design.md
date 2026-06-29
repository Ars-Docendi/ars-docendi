## Context

SCRUM-7 digitaliza la carga de pedidos de designación por el Jefe de Cátedra, primer eslabón del flujo de Designaciones. El cimiento ya existe en el frontend como mock: SCRUM-82 (Períodos) y SCRUM-6 (usuarios/roles) están construidos (changes OpenSpec archivados). Este change entrega un **prototipo frontend de alta fidelidad con datos mockeados, sin backend**, para validar UX y reglas de negocio con el cliente antes de comprometer la API real.

Estado actual del frontend (verificado): React 19 + TypeScript + Vite 8, `react-router-dom` v7 (`createBrowserRouter`), `@tanstack/react-query` v5 **instalado pero sin uso** (las páginas usan `useState`), `axios` singleton en `shared/api/client.ts` (sin uso por las páginas mock), `@ars-docendi/ui` pin `release/v1.0.2` (exports verificados: cubren el form completo; `StatusKind` calza 1:1 con el mapeo de estados). **No hay runner de tests configurado.** El feature slice `features/designaciones/` ya existe con el patrón `api/` + `components/` + `hooks/` + `pages/` + `routes.tsx` + `types.ts`, sin barrels, identificadores en español (invariante #13).

Restricción de proceso: invariante #5 (change apply-ready antes de codear), #7 (no fake UI), #11 (BR con cita + test), #12 (design spec), #13 (código en español). El plan maestro `docs/product/designs/proyecto-docente-frontend-plan.md` resuelve las decisiones de diseño de producto (grill §4) y no se re-litiga.

## Goals / Non-Goals

**Goals:**

- Prototipo navegable y demostrable del lado del Jefe de Cátedra: crear/editar/cancelar pedidos sobre un período abierto y enviarlos a revisión, con persistencia coherente entre cambios de rol y recargas.
- **Arquitectura forward-compatible**: toda lectura/escritura pasa por una capa `api/` async sobre un store mock; ese es el **único seam** que se reemplaza por axios + React Query real cuando llegue el backend. Fuera de `api/` no debe haber `// TODO(backend)`.
- **TDD estricto** sobre la lógica de dominio (máquina de estados + guards) y las validaciones de form (lógica pura), más tests de UI clave.
- Fidelidad visual alineada a `screens.pen` y a `@ars-docendi/ui`.
- Trazabilidad: cada criterio de aceptación referencia su epic (SCRUM-7) y su `BR-designaciones-NNN`.

**Non-Goals:**

- Backend de cualquier tipo (controllers, services, EF, PostgreSQL, endpoints reales). Solo mock.
- El circuito de revisión completo (Kanban, detalle role-aware, aceptar/rechazar/devolver/priorizar) → es SCRUM-8 (`flujo-aprobacion-designaciones`), change encadenado.
- Notificaciones (campana = placeholder), SSO real (Azure AD; se sigue con el selector de rol mock), cadena de hashes de integridad, concurrencia / bloqueo optimista, File Storage real, API Guaraní / Portal cross-module real (horas de investigación mockeadas inline).

## Decisions

### D1 — Capa de datos mock en tres archivos: store / api / máquina de estados

El seam del backend se materializa en `features/designaciones/api/` con responsabilidades separadas:

- **`pedidosStore.ts`** — singleton en memoria (`let pedidos: PedidoDesignacion[]`) hidratado desde `localStorage` (clave `adoc.mock.pedidos`) y persistido en cada escritura. Lectura/escritura **síncrona** interna. **No lo consumen los componentes directamente.**
- **`pedidosApi.ts`** — la API mock **async**: cada función devuelve `Promise` con latencia simulada (`await demora(250)`), opera sobre el store y **delega las transiciones a `maquinaEstados.ts`**. **Este es el seam**: cuando llegue el backend se reemplaza el cuerpo por `apiClient.get/post(...)` manteniendo la firma, y hooks/componentes no cambian. Cada función lleva su `// TODO(backend)` con el formato fijo del §7 del plan.
- **`maquinaEstados.ts`** — **lógica pura** (sin React, sin Promise): `aplicarAccion(pedido, accion)` valida guards y devuelve el nuevo pedido o lanza `ErrorDominioPedido`. Es el corazón del TDD estricto.

**Por qué**: aísla el cambio futuro a un solo lugar y permite testear el dominio sin red ni framework. **Alternativa descartada**: MSW (mockear a nivel HTTP). Se descartó porque hoy no hay HTTP — el dato es un módulo, mockear el módulo es más simple y directo; MSW agregaría una capa que igual hay que reescribir cuando exista el backend real.

### D2 — React Query como capa de consumo (primera feature que lo usa de verdad)

Los componentes consumen los datos vía hooks de React Query: `usePedidos.ts` (queries: lista del JC, por id) y `useAccionesPedido.ts` (mutations: crear, editar, enviar, cancelar — que invalidan `["pedidos"]` en `onSuccess`). El `QueryClientProvider` ya está en `main.tsx`.

**Por qué**: golden-principles exige "toda data por React Query; nada de `useEffect` + fetch manual". Esta feature **establece el patrón** para el resto del proyecto y nos da Loading/Empty/Error/Success gratis (hay que renderizarlos). **Alternativa descartada**: seguir con `useState` + llamadas directas (como las páginas de períodos actuales) — viola el principio y no escala al backend real.

### D3 — Máquina de estados como función pura, con el alcance del lado JC en este change

`EstadoPedido` y la tabla de transiciones del §6.5 del plan se implementan en `maquinaEstados.ts`. **Este change implementa las transiciones que SCRUM-7 posee**: `enviar` (`borrador` → `en_revision_coordinador`), `cancelar` (`borrador` → `cancelado`), `editar` (`borrador`/`devuelto` del propietario, sin cambio de estado), más los guards transversales aplicables (idempotencia terminal, propietario). La firma y la estructura de `aplicarAccion` se diseñan para que SCRUM-8 **agregue** las transiciones de revisión (aceptar/rechazar/devolver/reenviar/priorizar) sin reescribir las existentes.

**Por qué**: el dominio es la pieza de mayor valor testeable y la más estable frente al backend. Modelarla pura y extensible evita reescritura entre los dos changes encadenados. **Trade-off**: el tipo `EstadoPedido` y algunos campos (`etapaRetorno`, `propietarioActual`) se declaran ya completos en `types.ts` aunque SCRUM-7 no ejercite todas las ramas, para no romper el tipo en SCRUM-8.

### D4 — Componentes: lib primero, in-app solo lo que falta

Mapeo a `@ars-docendi/ui` (exports verificados en `release/v1.0.2`): el form usa `Field`, `Input`, `Select`, `Radio`, `Textarea`, `Button`; adjuntos `FileUpload` (stateless, el padre maneja la lista; mock = solo metadata); validación inline `InlineAlert` + `Field error`; "Mis pedidos" `Table` (namespace) + `StatusBadge` + `Button`. **Construido in-app**: `EstadoPedidoBadge.tsx` (wrapper fino sobre `StatusBadge` que mapea `EstadoPedido` → `StatusKind` según §6.6; `StatusKind` calza 1:1). El Kanban/Card del revisor NO se construye en este change (es SCRUM-8).

**Por qué**: no regresar de versión ni reinventar componentes existentes (decisión grill #6). **Gotcha**: `AuditVerb` de la lib está en inglés (`create|update|attach|approve|return|reject`) — es símbolo de librería, se usa tal cual; la `accion` del historial es español de dominio y se mapea al alimentar `AuditLog` (relevante recién en SCRUM-8).

### D5 — Harness de tests: Vitest + Testing Library + jsdom + user-event (Fase 0)

Bootstrap del runner inexistente: `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `@testing-library/user-event`, `jsdom`, `@vitest/coverage-v8`. Config `vitest.config.ts` con `environment: "jsdom"`, `globals: true`, `setupFiles` con jest-dom; scripts `test`/`test:run`. El `localStorage` se limpia entre tests para aislar el store singleton. Sin MSW (ver D1).

**Por qué**: el TDD estricto del dominio y las validaciones (decisión grill #7) requieren runner. Es un PR chico habilitante (Fase 0) y resuelve la deuda "runner frontend TBD" de `tech-debt.md`. **Red-green estricto** en `maquinaEstados.ts` (una falla primero por fila/guard de la tabla) y en el validador de form (BR-001..004); tests de UI (RTL/user-event) para `PedidoForm` (secciones condicionales, submit inválido bloqueado).

### D6 — Routing, nav y gating por rol

`routes.tsx` se extiende con `mis-pedidos` → `MisPedidosPage` y `pedidos/nuevo` + `pedidos/:id/editar` → `PedidoFormPage`, todas envueltas en `RequireRole` (componente existente) para el rol Jefe de Cátedra. `nav.ts` (`NAV_BY_ROLE`) agrega el ítem "Mis pedidos" solo para el JC, respetando invariante #7 (sin links muertos). El `ActorContexto` que consume `api/` se deriva de `useCurrentUser()` (rol activo) + el mock (carrera/ámbito). `mockUsers.ts` suma personas single-rol realistas + un usuario "Demo (todos los roles)" que usa el `RoleMenu` existente para recorrer la cadena sin re-loguear (útil para la defensa y para los tests de integración de SCRUM-8).

**Por qué**: gating por rol+etapa+ámbito es un design-principle ("roles visibles, permisos claros"); no mostrar acciones que el rol no puede ejecutar. La ruta de detalle (`pedidos/:id`) y `revision` se agregan en SCRUM-8.

### D7 — Un change por epic, encadenados

`proyecto-docente-pedidos` (SCRUM-7, este change) y `flujo-aprobacion-designaciones` (SCRUM-8) son changes separados y encadenados. La fundación de dominio (types, máquina de estados, store, api, hooks, harness) se construye acá porque SCRUM-7 va primero; SCRUM-8 la extiende.

**Por qué**: respeta el orden `Blocks` de Jira y mantiene los PRs reviewables (≤ ~400 líneas → slices encadenados si hace falta, skill `chained-pr`). **Alternativa descartada**: un solo change gigante SCRUM-7+8 — diff inmanejable para review y mezcla dos epics.

## Risks / Trade-offs

- **[El dominio se filtra fuera del seam]** → Regla dura: `// TODO(backend)` solo en `api/`; si aparece en un componente, la lógica se filtró. Lo verifica un criterio de aceptación global y el reviewer.
- **[El store singleton + `localStorage` ensucia el estado entre tests]** → `setupFiles` limpia `localStorage` y resetea el singleton antes de cada test; los tests de integración parten de seed conocido.
- **[Citas normativas de BR-001..004 sin confirmar]** → Se implementa la validación igual y se registra el BR con la cita marcada "pendiente con cliente"; el test queda en el lane `business` pendiente de cita. No bloquea el prototipo.
- **[Declarar el tipo `EstadoPedido` completo sin ejercitar todas las ramas en SCRUM-7]** → Aceptado a propósito (D3) para no romper el tipo en SCRUM-8; las ramas no usadas quedan cubiertas por sus tests recién en SCRUM-8.
- **[Discrepancia de versión de la lib: el `package.json` instalado reporta 1.0.1 pese al pin `v1.0.2`]** → No bloqueante; los exports cubren el 100% del form. Si en SCRUM-8 faltara algún componente del detalle (ApprovalTimeline/AuditLog ya verificados presentes), se evaluaría bumpear el pin.
- **[Primera feature con React Query real]** → Riesgo de patrón mal establecido que se copie. Mitigación: hooks chicos y explícitos, queryKeys namespaced (`["pedidos", ...]`), invalidación en `onSuccess`; queda como referencia para el resto.

## Migration Plan

No hay migración de datos ni de API (todo mock, aditivo y frontend-only). Despliegue = merge del/los PR(s) a `develop`. Rollback = revertir el/los PR(s); como el store vive en `localStorage` del navegador del usuario, un rollback de código no corrompe datos persistidos del backend (no existe). Orden de entrega sugerido (slices reviewables): Fase 0 (harness) → Fase 1 (fundaciones de dominio: types + máquina de estados + store + api + hooks + `EstadoPedidoBadge`, con su suite TDD) → Fase 2 (UI del JC: `MisPedidosPage` + `TablaMisPedidos` + `PedidoForm` + `PedidoFormPage` + rutas/nav/gating + mockUsers + validaciones con tests).

## Open Questions

- Citas normativas de BR-001..004 (estatuto / régimen docente UNLaM) — a confirmar con el cliente; no bloquea.
- Nomenclatura definitiva del estado post-Decanato (`en_lote` vs `Aprobado`) — afecta recién a SCRUM-8.
- ¿El CI debe correr `test:run` en el path filter de frontend desde ya? Recomendado sí, en el PR de Fase 0; confirmar con el equipo.
