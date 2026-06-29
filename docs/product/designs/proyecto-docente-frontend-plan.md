# Plan de implementación — Prototipo frontend "Proyecto Docente" (SCRUM-7 + SCRUM-8)

> **Qué es esto.** Un plan maestro auto-contenido para construir un **prototipo de alta fidelidad, SOLO frontend con datos mockeados**, de la carga de pedidos de designación (SCRUM-7) y su flujo de aprobación (SCRUM-8). El backend se define e implementa después; cada punto de contacto con el backend queda marcado con `// TODO(backend)` en el seam de la capa `api/`.
>
> **Cómo usarlo.** Alimentá este archivo a un contexto nuevo de Claude Code y arrancá por la sección [§14 Cómo arrancar](#14-cómo-arrancar-en-el-contexto-nuevo). El plan ya tiene resueltas todas las decisiones de diseño (ver [§4](#4-registro-de-decisiones-grill)).

| Campo                             | Valor                                                                     |
| --------------------------------- | ------------------------------------------------------------------------- |
| **Módulo**                        | Designaciones                                                             |
| **Epics Jira**                    | SCRUM-7 (Generación del proyecto docente) · SCRUM-8 (Flujo de aprobación) |
| **Dependencias (ya construidas)** | SCRUM-6 (ABM usuarios/roles) · SCRUM-82 (ABM Períodos)                    |
| **Tipo**                          | Prototipo frontend de alta fidelidad — datos mockeados, sin backend       |
| **Release**                       | R1                                                                        |
| **Fecha del plan**                | 2026-06-20                                                                |
| **Estado**                        | Listo para `/opsx:propose` + `/add-feature`                               |

---

## 1. Contexto y hallazgos clave

### 1.1 Orden de implementación (de las "actividades vinculadas" en Jira)

Los tres tickets son **Epics** en estado _Idea_, sin subtareas. La cadena de `Blocks` define el orden:

```
SCRUM-6 (ABM usuarios/roles)  →  SCRUM-82 (ABM Períodos)  →  SCRUM-7 (Pedidos / Proyecto Docente)  →  SCRUM-8 (Flujo de aprobación)  →  SCRUM-81 (Manejo de Lotes)
        [ya construido]              [ya construido]              ◄── ESTE PLAN ──►                                                    [fuera de alcance]
```

- **SCRUM-7** — "Carga y gestión de pedidos de designación docente por parte del Jefe de Cátedra. Incluye datos del docente, materia, horas, tipo de novedad y adjunto de documentación obligatoria." _Bloqueado por_ SCRUM-82. _Bloquea_ SCRUM-8.
- **SCRUM-8** — "Circuito de aprobación de pedidos: Coordinador de Carrera → Secretaría Académica → Decanato. Incluye acciones de aprobar, rechazar y devolver con comentario, y notificaciones al Jefe de Cátedra." _Bloqueado por_ SCRUM-7. _Bloquea_ SCRUM-81.

### 1.2 Estado actual del repositorio (verificado)

- **SCRUM-82 (Períodos) y SCRUM-6 (usuarios) YA están implementados** como mock en el front (changes OpenSpec archivados). Sirven de **cimiento**: un pedido se crea dentro de un **período abierto**, y los roles/personas ya existen. No se reimplementan.
- **Frontend**: React 19 + TypeScript + Vite 8, `react-router-dom` v7 (`createBrowserRouter`), `@tanstack/react-query` v5 **instalado pero sin uso todavía** (las páginas usan `useState`), `axios` singleton en `src/shared/api/client.ts` (sin uso por las páginas mock).
- **Librería de UI**: `@ars-docendi/ui`, consumida desde `github:Ars-Docendi/ui-lib#release/v1.0.2`. El `../ui-lib` local (v1.0.1) es la **referencia** (tiene Storybook con 62 stories). Cubre casi todo lo que necesitamos (ver [§6.7](#67-mapping-a-componentes-de-ars-docendiui)).
- **Testing**: **NO hay runner configurado** (ni Vitest, ni RTL, ni jsdom, ni script `test`). Bootstrapearlo es la **Fase 0** ([§8](#8-plan-tdd)).
- **Patrón de feature slice** (de `features/designaciones/` ya existente): `api/` + `components/` + `hooks/` + `pages/` + `routes.tsx` + `types.ts`. Sin barrels `index.ts`. Identificadores y comentarios en **español** (invariante #13).
- **Diseños**: `docs/product/designs/screens.pen` ya contiene los mockups de pedido-form (Alta/Editar), tablero de revisión y el flujo de revisión ("Modelo B": Aceptar / Rechazar terminal / Devolver / Marcar prioritario / En lote). Este plan implementa esos diseños.

### 1.3 Gates del proyecto que aplican

- **Invariante #5** — feature nueva ⇒ change OpenSpec apply-ready ANTES de tocar código. Lo mintea el contexto nuevo con `/opsx:propose` (ver [§13](#13-obligaciones-de-proceso)).
- **Invariante #7** — No fake UI: cada botón/ruta hace algo visible (mutar el store mock cuenta).
- **Invariante #11** — Toda BR que venga de normativa se registra como `BR-designaciones-NNN` con cita + mapping a test ([§9](#9-business-rules-a-registrar)).
- **Invariante #12** — Cambios de UX ⇒ actualizar `docs/product/designs/proyecto-docente-design-spec.md`.
- **Invariante #13** — Código en español (identificadores, comentarios, docstrings). Símbolos de framework/lib tal como los define la librería.

---

## 2. Objetivos y no-objetivos

### 2.1 Objetivos

1. Un **prototipo navegable y demostrable de punta a punta** del Proyecto Docente: el Jefe de Cátedra crea pedidos sobre un período abierto, los envía, y la cadena Coordinador → Secretaría → Decanato los acepta / rechaza / devuelve, con persistencia coherente entre cambios de rol y recargas.
2. **Fidelidad visual** alineada a `screens.pen` y al design system `@ars-docendi/ui`.
3. **Arquitectura forward-compatible**: toda lectura/escritura pasa por una capa `api/` async (Promise) sobre un store mock; ese es el **único seam** que se reemplaza por axios + React Query cuando llegue el backend.
4. **TDD estricto** sobre el dominio (máquina de estados + guards + validaciones) y tests de UI clave + integración del happy-path.
5. **Trazabilidad a Jira y a las BR**: cada criterio de aceptación referencia su epic y su `BR-designaciones-NNN`.

### 2.2 No-objetivos (fuera de alcance — diferido con `// TODO(backend)` o a tickets posteriores)

- **Backend de cualquier tipo** (controllers, services, EF, PostgreSQL, endpoints reales). Solo mock.
- **SCRUM-81 / Manejo de lotes, exportación a Excel, `confirmar-novedades`, rectificativas, docentes especiales** (posteriores, dependen de SCRUM-8).
- **Notificaciones** (in-app o email): la campana del TopBar queda como **placeholder**. Se anota como diferido. _(Decisión del grill — ver §4.)_
- **SSO real (Azure AD)**: se sigue con el selector de rol / MockLogin existente (R1).
- **Cadena de hashes de integridad (RNF-7)**, concurrencia / bloqueo optimista (P-09), File Storage real.
- **API Guaraní / Portal cross-module real**: las "horas de investigación" y datos del docente se mockean inline.

---

## 3. Alcance funcional por epic

### 3.1 SCRUM-7 — Generación del proyecto docente (Jefe de Cátedra)

**Granularidad** _(decisión del grill)_: **pedido individual por docente**, dentro de un contenedor "Mis pedidos" por período abierto. Con **precarga** mock de los docentes del período anterior como "Sin novedad".

Pantallas:

1. **Mis pedidos** (`/designaciones/mis-pedidos`) — lista de los pedidos del JC para el período abierto, con `StatusBadge` por estado, flag de prioritario, y acciones: **Nuevo pedido**, **Editar** (en `borrador` o `devuelto`-a-JC), **Cancelar** (en `borrador`), **Enviar a revisión** (pasa los borradores a la cadena). Estados de página: Loading / Empty (sin pedidos) / Error / Success.
2. **Form de pedido** (`/designaciones/pedidos/nuevo` y `/designaciones/pedidos/:id/editar`) — campos del pedido con **secciones condicionales por novedad**:
   - Comunes: docente (DNI, nombre), **antigüedad**, cargo actual, dedicación actual (read-only, mock), **materia asociada**, novedad (Radio: Sin novedad / Alta / Baja / Cambio de cargo o dedicación), flag "hace más horas en otro Departamento".
   - **Alta** → cargo y dedicación solicitados + adjuntos obligatorios **CV + foto DNI frente + foto DNI dorso** (`FileUpload`) [BR-002].
   - **Baja** → adjunto justificativo obligatorio [BR-003].
   - **Cambio de cargo o dedicación** → cargo/dedicación solicitados + **justificación** obligatoria [BR-004].
   - Validación inline (`InlineAlert` + `Field error`). Un docente = un pedido por período [BR-001].

### 3.2 SCRUM-8 — Flujo de aprobación (Coordinador / Secretaría / Decanato / Administración)

**Superficie de triage** _(decisión del grill)_: **Kanban de revisión sin drag**.

Pantallas:

3. **Tablero de revisión** (`/designaciones/revision`) — Kanban con columnas **Pendiente (mi etapa)** / **Aprobado** / **Rechazado** / **Devuelto**, filtrado por **ámbito** del rol [BR-009]. Cada `PedidoCard` muestra docente + cátedra + novedad + `StatusBadge` + flag prioritario. Click en la card → detalle. _(Card y columna se construyen in-app — la lib no trae Kanban.)_
4. **Detalle del pedido** (`/designaciones/pedidos/:id`) — vista role-aware:
   - Datos completos del docente y del pedido (`DataList`), incluida materia asociada y horas de investigación (mock).
   - **Cadena de aprobación** (`ApprovalTimeline`) + **historial** de eventos (`AuditLog`).
   - Para el **revisor de la etapa actual** dentro de su ámbito: botones **Aceptar** (primary), **Rechazar** (destructive), **Devolver** (warning), **Marcar prioritario** (ghost). Cada uno abre `ModalAccionRevision` (Modal + Textarea) con la regla de comentario/justificativo [BR-005].
   - Para el JC y otros: solo lectura + timeline.

**Extras del modelo incluidos** _(decisión del grill — todos)_:

- **Reenvío tras devolución** [BR-014]: el devuelto vuelve editable al actor anterior; al reenviar retoma la etapa del que devolvió.
- **Marcar prioritario** [BR-017]: cualquier actor, con justificativo, sin cambiar estado.
- **Cancelar en borrador** (JC).
- **Administración como revisor sin aprobar** [BR-015]: ve el depto, puede Rechazar/Devolver/editar, NO Aceptar.

---

## 4. Registro de decisiones (grill)

| #   | Decisión           | Elegido                                                                                                       | Implicancia                                                       |
| --- | ------------------ | ------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| 1   | Alcance            | **Foco SCRUM-7 + SCRUM-8**; períodos/usuarios como base; lotes/Excel/rectificativas/docentes especiales fuera | Achica el trabajo a las 4 pantallas nuevas                        |
| 2   | Entregable         | **Plan maestro markdown** (este archivo); el contexto nuevo mintea OpenSpec con `/opsx:propose`               | Respeta invariante #5 sin pre-generar multi-archivo               |
| 3   | Carga del JC       | **Pedido individual + contenedor "Mis pedidos"** + precarga                                                   | Define las pantallas de SCRUM-7                                   |
| 4   | Datos mock         | **Capa `api/` async (Promise + latencia) + React Query + store singleton + `localStorage`**                   | El flujo persiste entre roles/recargas; seam de backend en `api/` |
| 5   | Triage del revisor | **Kanban sin drag** (columnas, click → detalle)                                                               | Hay que construir Card + columna in-app                           |
| 6   | Librería           | **Mantener pin `v1.0.2`**; `../ui-lib` = referencia/Storybook; verificar componentes; faltantes in-app        | No regresa de versión; build limpio                               |
| 7   | TDD                | **Estricto en el core + UI clave** (Vitest + RTL + jsdom + user-event, sin MSW)                               | Fase 0 = bootstrap del harness                                    |
| 8   | Personas mock      | **Single-rol realistas + 1 usuario "Demo" multi-rol**                                                         | Recorre la cadena sin re-loguear (RoleMenu existente)             |
| 9   | Notificaciones     | **Fuera de alcance** (campana placeholder, diferido)                                                          | Menos superficie; `// TODO(backend)` anotado                      |
| 10  | Extras del flujo   | **Todos**: reenvío, prioritario, cancelar, Administración revisor                                             | Modelo completo y testeable                                       |
| 11  | Ubicación del plan | `docs/product/designs/proyecto-docente-frontend-plan.md`                                                      | Junto a `screens.pen`                                             |

---

## 5. Guidelines y convenciones (no negociables)

1. **Español en el código** (invariante #13): identificadores, props, tipos, comentarios y docstrings en español de dominio. Excepción: símbolos de framework/lib (`useState`, `useQuery`, `useMutation`, `Outlet`, nombres de componentes de `@ars-docendi/ui`). Ej.: `usePedidos`, `aceptarPedido`, `EstadoPedido`, `TableroRevision`, `ModalAccionRevision`.
2. **No fake UI** (invariante #7): cada acción muta el store mock y se refleja en la UI. Nada de lorem ipsum, TODO visible al usuario ni botones muertos.
3. **Estados explícitos** (design-principles): Loading / Empty / Error / Success en cada pantalla con datos. React Query nos los da; hay que renderizarlos.
4. **Features aisladas** (golden-principles): nada de cross-imports entre features; lo común a `src/shared/`. Toda data por React Query; nada de `useEffect` + fetch manual.
5. **Archivos chicos**: ~300 líneas como cap soft. Componentes presentacionales separados de los contenedores.
6. **Roles visibles, permisos claros** (design-principles): no mostrar acciones que el rol no puede ejecutar (gating por etapa + ámbito).
7. **Convención `// TODO(backend)`** (ver [§7](#7-convención-todobackend)).

---

## 6. Arquitectura técnica

### 6.1 Estructura de carpetas (extiende `features/designaciones/`)

```
frontend/src/features/designaciones/
├── api/
│   ├── periodosMock.ts            (ya existe)
│   ├── pedidosStore.ts            ◄ NUEVO — store singleton en memoria + persistencia localStorage
│   ├── pedidosApi.ts              ◄ NUEVO — funciones async (SEAM backend). Aquí van los // TODO(backend)
│   ├── pedidosSeed.ts             ◄ NUEVO — datos iniciales (docentes precargados, pedidos de ejemplo por estado)
│   └── maquinaEstados.ts          ◄ NUEVO — transiciones + guards (lógica pura, sin React) ← TDD estricto
├── components/
│   ├── (existentes de períodos…)
│   ├── PedidoForm.tsx             ◄ NUEVO — form crear/editar, secciones condicionales
│   ├── TablaMisPedidos.tsx        ◄ NUEVO — lista del JC
│   ├── TableroRevision.tsx        ◄ NUEVO — Kanban (columnas)
│   ├── ColumnaKanban.tsx          ◄ NUEVO — columna in-app
│   ├── PedidoCard.tsx             ◄ NUEVO — card del Kanban
│   ├── ModalAccionRevision.tsx    ◄ NUEVO — Modal + Textarea (aceptar/rechazar/devolver/priorizar)
│   └── EstadoPedidoBadge.tsx      ◄ NUEVO — wrapper sobre StatusBadge mapeando EstadoPedido → kind
├── hooks/
│   ├── usePedidos.ts              ◄ NUEVO — React Query: queries (lista por ámbito, por id)
│   └── useAccionesPedido.ts       ◄ NUEVO — React Query: mutations (crear, enviar, aceptar, …)
├── pages/
│   ├── IndexPage.tsx              (ya existe — placeholder)
│   ├── PeriodosPage.tsx           (ya existe)
│   ├── MisPedidosPage.tsx         ◄ NUEVO (SCRUM-7)
│   ├── PedidoFormPage.tsx         ◄ NUEVO (SCRUM-7)
│   ├── TableroRevisionPage.tsx    ◄ NUEVO (SCRUM-8)
│   └── DetallePedidoPage.tsx      ◄ NUEVO (SCRUM-8)
├── routes.tsx                     (extender)
└── types.ts                       (extender)
```

> Componentes genéricos reutilizables (ej. un `Card` base, si hace falta) van a `src/shared/ui/`. Todo lo específico de designaciones queda en el feature.

### 6.2 Capa de datos mock (el seam del backend)

Tres archivos, responsabilidades separadas:

- **`pedidosStore.ts`** — un **singleton** en memoria (`let pedidos: PedidoDesignacion[]`) hidratado desde `localStorage` (clave `adoc.mock.pedidos`) y persistido en cada escritura. Expone lectura/escritura **síncrona** interna. _No lo consumen los componentes directamente._
- **`pedidosApi.ts`** — la **API mock async**. Cada función devuelve `Promise` con latencia simulada (`await demora(250)`), opera sobre el store, y **delega las transiciones de estado a `maquinaEstados.ts`**. **Este es el seam**: cuando llegue el backend, se reemplaza el cuerpo por llamadas `apiClient.get/post(...)` y los componentes/hooks no cambian. Cada función lleva su `// TODO(backend)`.
- **`maquinaEstados.ts`** — **lógica pura** (sin React, sin Promise): dada `(pedido, acción, actor)` valida guards y devuelve el pedido resultante o lanza un error de dominio. Es el corazón del TDD estricto.

```ts
// pedidosApi.ts (forma del seam)
// TODO(backend): reemplazar por GET /api/designaciones/pedidos?ambito=... (SCRUM-8).
//   Hoy lee del store mock en localStorage; mantener la misma firma para no tocar hooks/componentes.
export async function listarPedidosPorAmbito(actor: ActorContexto): Promise<PedidoDesignacion[]> {
  await demora(250);
  return pedidosStore.leerTodos().filter((p) => visibleEnAmbito(p, actor));
}

// TODO(backend): reemplazar por POST /api/designaciones/pedidos/:id/aceptar (SCRUM-8).
export async function aceptarPedido(
  id: string,
  actor: ActorContexto,
  comentario?: string,
): Promise<PedidoDesignacion> {
  await demora(250);
  const actual = pedidosStore.buscar(id);
  const siguiente = aplicarAccion(actual, { tipo: "aceptar", actor, comentario }); // ← maquinaEstados.ts
  pedidosStore.guardar(siguiente);
  return siguiente;
}
```

### 6.3 Consumo con React Query (hooks)

```ts
// usePedidos.ts
export function usePedidosPorAmbito(actor: ActorContexto) {
  return useQuery({
    queryKey: ["pedidos", "ambito", actor.rol, actor.ambito],
    queryFn: () => listarPedidosPorAmbito(actor),
  });
}
export function usePedido(id: string) {
  return useQuery({ queryKey: ["pedidos", id], queryFn: () => obtenerPedido(id) });
}

// useAccionesPedido.ts — mutations que invalidan las queries afectadas
export function useAceptarPedido() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, actor, comentario }: ParamsAceptar) => aceptarPedido(id, actor, comentario),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["pedidos"] }),
  });
}
```

> El `QueryClientProvider` ya está en `main.tsx`. Esta es la primera feature que usa React Query de verdad — establece el patrón que pide golden-principles.

### 6.4 Modelo de datos (extiende `types.ts`)

```ts
export type Rol =
  | "Jefe de Cátedra"
  | "Coordinador"
  | "Secretaría"
  | "Decanato"
  | "Administración"
  | "Docente";

export type Novedad = "Sin novedad" | "Alta" | "Baja" | "Cambio de cargo o dedicación";
export type Cargo = "Titular" | "Adjunto" | "JTP" | "Ayudante";
export type Dedicacion =
  | "Categoría 1"
  | "Categoría 2"
  | "Categoría 3"
  | "Categoría 4"
  | "Categoría 5"
  | "Categoría 6";

export type EstadoPedido =
  | "borrador"
  | "en_revision_coordinador"
  | "en_revision_secretaria"
  | "en_revision_decanato"
  | "devuelto" // sub-estado: el pedido volvió a un actor anterior para corrección
  | "en_lote" // terminal-para-el-prototipo (flujo real: → Universitaria → Aprobado)
  | "rechazado" // terminal
  | "cancelado"; // terminal

export interface Adjunto {
  id: string;
  nombre: string; // solo nombre/tipo en el mock
  tipo: "cv" | "dni_frente" | "dni_dorso" | "justificativo";
  // TODO(backend): subir a File Storage y guardar URL (RNF-4). Hoy es solo metadata mock.
}

export interface EventoHistorial {
  id: string;
  accion:
    | "crear"
    | "enviar"
    | "aceptar"
    | "rechazar"
    | "devolver"
    | "reenviar"
    | "editar"
    | "cancelar"
    | "priorizar";
  porRol: Rol;
  porNombre: string;
  etapa: EstadoPedido;
  comentario?: string; // justificativo (rechazo) / comentario (devolución) / motivo (prioridad)
  fecha: string; // ISO
}

export interface PedidoDesignacion {
  id: string;
  periodoId: string; // FK al período (SCRUM-82)
  catedra: string;
  carrera: string; // para el ámbito del Coordinador
  docente: { dni: string; nombre: string; antiguedad: number };
  materiaAsociada: string;
  cargoActual: Cargo | null;
  dedicacionActual: Dedicacion | null;
  novedad: Novedad;
  cargoSolicitado?: Cargo;
  dedicacionSolicitada?: Dedicacion;
  justificacion?: string;
  haceHorasOtroDepto: boolean;
  horasInvestigacion?: number; // mock (cross-module Portal en el real)
  adjuntos: Adjunto[];
  estado: EstadoPedido;
  prioritario: boolean;
  // cuando estado === "devuelto":
  etapaRetorno?: EstadoPedido; // a qué etapa de revisión vuelve al reenviar
  propietarioActual?: Rol; // quién debe corregir (JC / Coordinador / Secretaría)
  historial: EventoHistorial[];
}

// Contexto del actor que ejecuta una acción (rol + ámbito), derivado de useCurrentUser + mock.
export interface ActorContexto {
  rol: Rol;
  nombre: string;
  carrera?: string; // ámbito del Coordinador
  // depto implícito para Secretaría/Decanato/Administración
}
```

### 6.5 Máquina de estados y guards (núcleo del TDD)

Tabla de transiciones. `maquinaEstados.ts` la implementa como función pura `aplicarAccion(pedido, accion): PedidoDesignacion` que valida y devuelve el nuevo pedido, o lanza `ErrorDominioPedido`.

| Acción      | Estado de origen                              | Rol autorizado (+ ámbito)            | Resultado                                                                                         | Regla          |
| ----------- | --------------------------------------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------- | -------------- |
| `enviar`    | `borrador`                                    | JC (dueño de la cátedra)             | → `en_revision_coordinador`                                                                       | BR-008         |
| `cancelar`  | `borrador`                                    | JC                                   | → `cancelado` (terminal)                                                                          |                |
| `editar`    | `borrador` o `devuelto` (propietario = actor) | propietario                          | sin cambio de estado                                                                              | BR-008         |
| `aceptar`   | `en_revision_coordinador`                     | Coordinador (su carrera)             | → `en_revision_secretaria`                                                                        |                |
| `aceptar`   | `en_revision_secretaria`                      | Secretaría (depto)                   | → `en_revision_decanato`                                                                          |                |
| `aceptar`   | `en_revision_decanato`                        | Decanato (depto)                     | → `en_lote` (terminal-prototipo)                                                                  |                |
| `aceptar`   | cualquiera                                    | **Administración**                   | **DENEGADO**                                                                                      | **BR-015**     |
| `rechazar`  | `en_revision_*`                               | revisor de la etapa o Administración | → `rechazado` (terminal); **justificativo obligatorio**                                           | BR-005, BR-011 |
| `devolver`  | `en_revision_coordinador`                     | Coordinador / Administración         | → `devuelto` (propietario=JC, etapaRetorno=`en_revision_coordinador`); **comentario obligatorio** | BR-005, BR-014 |
| `devolver`  | `en_revision_secretaria`                      | Secretaría / Administración          | → `devuelto` (propietario=Coordinador, etapaRetorno=`en_revision_secretaria`)                     | BR-014         |
| `devolver`  | `en_revision_decanato`                        | Decanato / Administración            | → `devuelto` (propietario=Secretaría, etapaRetorno=`en_revision_decanato`)                        | BR-014         |
| `reenviar`  | `devuelto` (propietario = actor)              | propietario                          | → `etapaRetorno`                                                                                  | BR-014         |
| `priorizar` | cualquiera no-terminal                        | cualquier actor (en ámbito)          | `prioritario=true`, sin cambio de estado; **justificativo obligatorio**                           | BR-017         |
| cualquiera  | `rechazado` / `cancelado` / `en_lote`         | —                                    | **DENEGADO** (idempotencia terminal)                                                              |                |

**Guards transversales:**

- **Etapa**: solo el revisor de la etapa actual puede aceptar/rechazar/devolver [BR-013].
- **Ámbito**: Coordinador solo su carrera; Secretaría / Decanato / Administración todo el depto [BR-009].
- **Administración** nunca acepta [BR-015].

### 6.6 Mapping `EstadoPedido` → `StatusBadge kind`

`@ars-docendi/ui` `StatusBadge` ya trae los kinds exactos:

| EstadoPedido                                            | `StatusBadge kind`          |
| ------------------------------------------------------- | --------------------------- |
| `borrador`                                              | `pendiente`                 |
| `en_revision_coordinador` / `_secretaria` / `_decanato` | `revision`                  |
| `devuelto`                                              | `devuelto`                  |
| `en_lote`                                               | `aprobado`                  |
| `rechazado`                                             | `rechazado`                 |
| `cancelado`                                             | `cancelado`                 |
| (flag `prioritario`)                                    | `prioritario` (badge extra) |

### 6.7 Mapping a componentes de `@ars-docendi/ui`

> Verificar al inicio que `release/v1.0.2` exporta los componentes "nuevos" (ApprovalTimeline, AuditLog, FileUpload, Drawer, Tabs, Radio, Textarea, Toast, Pagination). El `../ui-lib` local (v1.0.1) los tiene. Si falta alguno en v1.0.2 → bumpear el pin.

| Necesidad                | Componente de la lib                                                         | Notas                                                                |
| ------------------------ | ---------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| Form de pedido           | `Field`, `Input`, `Select`, `Radio`, `Textarea`, `DatePicker`, `Button`      | Radio para novedad; Field envuelve cada control con label/hint/error |
| Adjuntos                 | `FileUpload`                                                                 | Stateless: el padre maneja la lista. Mock (solo metadata)            |
| Validación inline        | `InlineAlert` (severities) + `Field error`                                   |                                                                      |
| Mis pedidos              | `Table` (namespace) o cards + `StatusBadge` + `Button`                       |                                                                      |
| Kanban                   | **in-app** `ColumnaKanban` + `PedidoCard` (+ `StatusBadge`)                  | La lib NO trae Kanban/Card                                           |
| Detalle                  | `DataList`, `ApprovalTimeline`, `AuditLog`, `StatusBadge`, `Tabs` (opcional) | Tabs: Solicitud / Historial / Documentos                             |
| Acciones de revisión     | `Modal` + `Textarea` + `Button` (primary/destructive/warning/ghost)          | `ModalAccionRevision` reusa Modal                                    |
| Breadcrumbs / paginación | `Breadcrumbs`, `Pagination`                                                  |                                                                      |
| Usuario / rol            | `RoleBadge`, `RoleMenu`                                                      | Ya están en el TopBar                                                |

**A construir in-app** (no están en la lib): `ColumnaKanban`, `PedidoCard`, (y si hace falta) un `Card` base en `shared/ui/`. Tooltip y date-range **no** se necesitan para 7/8.

### 6.8 Routing, nav y gating

- **`routes.tsx`** (extender el `RouteObject` de designaciones) — agregar:
  - `mis-pedidos` → `MisPedidosPage` (gate rol JC)
  - `pedidos/nuevo` y `pedidos/:id/editar` → `PedidoFormPage` (gate rol JC)
  - `pedidos/:id` → `DetallePedidoPage` (cualquier rol con visibilidad por ámbito; acciones gated por etapa)
  - `revision` → `TableroRevisionPage` (gate roles Coordinador / Secretaría / Decanato / Administración)
- **`RequireRole`** (componente existente) envuelve los grupos de rutas por rol.
- **`nav.ts`** (`NAV_BY_ROLE`) — agregar los ítems por rol respetando invariante #7 (sin links muertos): "Mis pedidos" para JC; "Revisión" para los revisores; "Períodos" sigue para Secretaría.

### 6.9 Personas mock (extiende `mockUsers.ts`)

_(Decisión del grill: single-rol realistas + 1 demo multi-rol.)_

- Mantener/asegurar una persona por rol: JC, Coordinador, Secretaría, Decanato, Administración, Docente (con `carrera` para el Coordinador, para probar el ámbito [BR-009]).
- Agregar **`Demo (todos los roles)`**: un usuario con `roles: [todos]` que usa el `RoleMenu` ya existente para saltar JC → Coordinador → Secretaría → Decanato sin re-loguear. Ideal para la defensa.
- El `ActorContexto` que consume la `api/` se deriva de `useCurrentUser()` (rol activo) + el mock (carrera/ámbito).

---

## 7. Convención `// TODO(backend)`

Todo punto que hoy es mock y mañana es backend lleva un comentario con **formato fijo**, concentrado en la capa `api/` (el seam):

```ts
// TODO(backend): <qué endpoint/comportamiento real lo reemplaza> — <ticket>.
//   Mock actual: <qué hace hoy>. Mantener la firma para no tocar hooks/componentes.
```

Ejemplos:

- `// TODO(backend): GET /api/designaciones/pedidos?ambito=... con autorización por rol+ámbito (RNF-1) — SCRUM-8.`
- `// TODO(backend): persistir adjuntos en File Storage y guardar URL (RNF-4) — SCRUM-7.`
- `// TODO(backend): emitir notificación in-app al cambiar de etapa (SCRUM-8 / capability notificaciones) — diferido.`
- `// TODO(backend): horas de investigación vía Portal.Contracts (cross-module) — hoy mock inline.`

Regla: **fuera de `api/` no debería haber `// TODO(backend)`** — si aparece uno en un componente, es señal de que la lógica se filtró fuera del seam.

---

## 8. Plan TDD

**Stack** _(decisión del grill)_: Vitest + `@testing-library/react` + `@testing-library/jest-dom` + `@testing-library/user-event` + jsdom. **Sin MSW** (el dato es un módulo `api/` mock, no HTTP; se mockea el módulo).

### Fase 0 — Bootstrap del harness (prerequisito de todo)

1. `pnpm --filter frontend add -D vitest @testing-library/react @testing-library/jest-dom @testing-library/user-event jsdom @vitest/coverage-v8`
2. `vitest.config.ts` (o `test` en `vite.config.ts`): `environment: "jsdom"`, `setupFiles` con `@testing-library/jest-dom`, `globals: true`.
3. Script `"test": "vitest"` y `"test:run": "vitest run"` en `frontend/package.json`.
4. Un test trivial verde (smoke del setup) para confirmar el harness.
5. Registrar en `docs/quality/tech-debt.md` que TD del "runner frontend TBD" queda **resuelto**.

### Qué se testea, y con qué rigor

**Red-green ESTRICTO (lógica de dominio pura — `maquinaEstados.ts`):** un test que falla primero por cada fila/guard de la tabla [§6.5]. Casos mínimos:

- `aceptaCoordinadorAvanzaASecretaria`, `aceptaSecretariaAvanzaADecanato`, `aceptaDecanatoVaAEnLote`
- `administracionNoPuedeAceptar` [BR-015]
- `rechazoSinJustificativoFalla` / `rechazoEsTerminal` [BR-005, BR-011]
- `devolucionSinComentarioFalla` / `devolucionRetrocedeUnNivel` / `reenvioRetomaEtapaDelRevisor` [BR-005, BR-014]
- `rolEtapaIncorrectaDenegado` [BR-013]
- `coordinadorFueraDeCarreraDenegado` [BR-009]
- `prioritarioExigeJustificativo` / `prioridadNoCambiaEstado` [BR-017]
- `accionSobrePedidoTerminalDenegada`
- `cancelarSoloEnBorrador`

**Red-green ESTRICTO (validación de form — lógica pura de `PedidoForm`/validador):**

- `altaExigeCvYDniFrenteYDorso` [BR-002]
- `bajaExigeJustificativo` [BR-003]
- `cambioExigeJustificacion` [BR-004]
- `unPedidoPorDocentePorPeriodo` [BR-001]

**Tests de UI clave (render + interacción con RTL/user-event):**

- `PedidoForm`: muestra/oculta secciones según novedad; bloquea submit inválido.
- `ModalAccionRevision`: exige comentario en rechazo/devolución; dispara la mutation.
- `PedidoCard` / `TableroRevision`: estado y prioridad correctos; solo muestra columnas/acciones del ámbito y la etapa.

**Integración (1–2, RTL sobre el store mock):**

- Happy-path de la cadena: JC crea → envía; Coordinador acepta → Secretaría acepta → Decanato acepta → `en_lote`. Verifica que el pedido viaja entre vistas (cambiando el `ActorContexto`).
- Camino de devolución: Coordinador devuelve → JC corrige y reenvía → vuelve a `en_revision_coordinador`.

> El `localStorage` se limpia entre tests (setup) para aislar el store singleton.

---

## 9. Business Rules a registrar

Crear `docs/business-rules/designaciones.md` (no existe aún) desde el template y registrar las BR con su cita y su mapping a test (invariante #11). Las marcadas _"cita pendiente"_ requieren confirmación normativa del cliente (estatuto/régimen docente UNLaM) — implementar la validación igual, marcar el test como `business` pendiente de cita.

| BR                   | Statement                                                 | Fuente              | Test (mapping)                                                 |
| -------------------- | --------------------------------------------------------- | ------------------- | -------------------------------------------------------------- |
| BR-designaciones-001 | Un pedido por docente por período, sobre una sola materia | _cita pendiente_    | `unPedidoPorDocentePorPeriodo`                                 |
| BR-designaciones-002 | Alta exige CV + DNI frente + DNI dorso                    | _cita pendiente_    | `altaExigeCvYDniFrenteYDorso`                                  |
| BR-designaciones-003 | Baja exige justificativo adjunto                          | _cita pendiente_    | `bajaExigeJustificativo`                                       |
| BR-designaciones-004 | Cambio de cargo/dedicación exige justificación            | _cita pendiente_    | `cambioExigeJustificacion`                                     |
| BR-designaciones-005 | Rechazo exige justificativo; devolución exige comentario  | decisión de proceso | `rechazoSinJustificativoFalla`, `devolucionSinComentarioFalla` |
| BR-designaciones-008 | Tras enviar, el JC no edita salvo devolución              | decisión de proceso | (guard `editar`)                                               |
| BR-designaciones-009 | Acción/visibilidad acotada al ámbito del rol              | decisión de proceso | `coordinadorFueraDeCarreraDenegado`                            |
| BR-designaciones-011 | El rechazo es terminal                                    | decisión de proceso | `rechazoEsTerminal`                                            |
| BR-designaciones-013 | Solo el revisor de la etapa actual puede actuar           | decisión de proceso | `rolEtapaIncorrectaDenegado`                                   |
| BR-designaciones-014 | Devolución retrocede un nivel; reenvío retoma la etapa    | decisión de proceso | `devolucionRetrocedeUnNivel`, `reenvioRetomaEtapaDelRevisor`   |
| BR-designaciones-015 | Administración revisa pero no aprueba                     | decisión de proceso | `administracionNoPuedeAceptar`                                 |
| BR-designaciones-017 | Cualquier actor marca prioritario con justificativo       | decisión de proceso | `prioritarioExigeJustificativo`                                |

---

## 10. Criterios de aceptación

### Globales

- [ ] El harness de tests corre (`pnpm --filter frontend test:run` verde) y hay tests para todas las BR testeables de [§9].
- [ ] `pnpm --filter frontend lint` y `pnpm --filter frontend build` verdes.
- [ ] No hay `// TODO(backend)` fuera de `features/designaciones/api/`.
- [ ] Todo identificador/comentario nuevo en español (invariante #13).
- [ ] El flujo persiste entre cambios de rol y recargas (store + localStorage).
- [ ] Cada pantalla con datos implementa Loading / Empty / Error / Success.
- [ ] `docs/product/designs/proyecto-docente-design-spec.md` creado (invariante #12) y `docs/business-rules/designaciones.md` con las BR (invariante #11).

### SCRUM-7

- [ ] El JC ve "Mis pedidos" del período abierto, con precarga de docentes del período anterior ("Sin novedad").
- [ ] Puede crear un pedido con secciones condicionales por novedad; la validación bloquea submit inválido (BR-001..004).
- [ ] Puede editar/cancelar en borrador y enviar a revisión (los pedidos pasan a `en_revision_coordinador`).
- [ ] Tras enviar, el pedido queda read-only para el JC salvo devolución (BR-008).

### SCRUM-8

- [ ] Cada revisor ve en su Kanban solo los pedidos de su ámbito (BR-009), en la columna correcta por estado.
- [ ] Aceptar avanza la etapa; el último (Decanato) lleva a `en_lote`.
- [ ] Rechazar (con justificativo) es terminal (BR-005, BR-011); devolver (con comentario) retrocede un nivel y permite reenvío que retoma la etapa (BR-014).
- [ ] Solo el revisor de la etapa actual puede actuar (BR-013); Administración puede rechazar/devolver pero no aceptar (BR-015).
- [ ] Cualquier actor puede marcar prioritario con justificativo, sin cambiar el estado (BR-017).
- [ ] El detalle muestra `ApprovalTimeline` + `AuditLog` con el historial completo.

---

## 11. Orden de implementación (fases / work-units para PRs reviewables)

> Mantener cada PR ≤ ~400 líneas (skill `chained-pr` / `work-unit-commits`). Sugerencia de slices:

- **Fase 0 — Harness de tests** ([§8](#8-plan-tdd)). PR chico, habilitante.
- **Fase 1 — Fundaciones de dominio (SCRUM-7/8 compartido)**: `types.ts`, `maquinaEstados.ts` (con su suite TDD estricta), `pedidosStore.ts`, `pedidosApi.ts` (seam + `// TODO(backend)`), `pedidosSeed.ts`, hooks React Query, `EstadoPedidoBadge`. **Sin UI de pantalla todavía.** Acá vive el grueso del valor testeable.
- **Fase 2 — SCRUM-7 (JC)**: `MisPedidosPage`, `TablaMisPedidos`, `PedidoForm` + `PedidoFormPage`, rutas + nav + gating JC, validaciones con sus tests. mockUsers (personas + demo).
- **Fase 3 — SCRUM-8 (revisores)**: `TableroRevision` + `ColumnaKanban` + `PedidoCard`, `DetallePedidoPage` (DataList + ApprovalTimeline + AuditLog), `ModalAccionRevision`, rutas + nav + gating de revisores, tests de UI + integración del happy-path y de la devolución.
- **Fase 4 — Cierre**: design-spec md (invariante #12), business-rules md (invariante #11), `/evaluate`, scorecard.

---

## 12. Skills a usar (respuesta a "¿qué skill uso?")

1. **`/opsx:propose`** — primero. Mintea el/los change(s) OpenSpec apply-ready (proposal + design + specs + tasks). Recomendado **un change por epic**: `proyecto-docente-pedidos` (SCRUM-7) y `flujo-aprobacion-designaciones` (SCRUM-8), encadenados. Sin esto, `/add-feature` se frena en su hard gate.
2. **`/add-feature`** — orquestador de cada feature ya con el change apply-ready. Corre architecture check, delega la ejecución de tasks a `/opsx:apply`, hace security pass, llama a `/evaluate` y abre el PR. (Su QA de frontend exige lint + build; sumamos el `test:run` por el TDD.)
3. **`/opsx:apply`** — ejecuta las tasks (invocado por `/add-feature`).
4. **`react-features-guide`** — se **auto-activa** al tocar `frontend/src/`; aporta las convenciones (features aisladas, React Query, axios singleton, routing).
5. **`/add-tests`** (lane business) — para formalizar el mapping BR ↔ test una vez que el runner está configurado.
6. **`/pencil-design`** (opcional) — si se quiere iterar `screens.pen` (`--in docs/product/designs/screens.pen`) para nuevas pantallas antes/durante el build.
7. **`/evaluate`** — cierre, score en `docs/quality/scorecard.md`.

---

## 13. Obligaciones de proceso

- **Invariante #5**: crear el/los change(s) con `/opsx:propose` y dejarlos apply-ready ANTES de codear. (Este plan es el insumo.)
- **Invariante #11**: crear `docs/business-rules/designaciones.md` y registrar las BR de [§9] con cita (las normativas quedan _"cita pendiente con cliente"_).
- **Invariante #12**: crear `docs/product/designs/proyecto-docente-design-spec.md` (Mis pedidos, form, Kanban de revisión, detalle/timeline) desde `_design-spec-template.md`.
- **Invariante #6**: como NO hay cambios de schema/API reales (todo mock), no aplica el update de `data-model.md`/`api-contracts.md` en este PR — pero el plan documenta el modelo de datos objetivo para cuando llegue el backend.
- **PRs encadenados** si el diff supera ~400 líneas (ver fases en [§11](#11-orden-de-implementación-fases--work-units-para-prs-reviewables)).

---

## 14. Cómo arrancar en el contexto nuevo

Prompt sugerido para pegar en una sesión nueva:

> Leé `docs/product/designs/proyecto-docente-frontend-plan.md` completo. Vamos a implementar el prototipo frontend de SCRUM-7 + SCRUM-8 siguiendo ese plan, **solo frontend con datos mockeados, TDD**. Empezá creando el change OpenSpec con `/opsx:propose` para SCRUM-7 (`proyecto-docente-pedidos`) según el alcance y el modelo de datos del plan; cuando esté apply-ready, seguí con `/add-feature`. Respetá las decisiones del §4, la arquitectura del §6 (capa `api/` async + React Query + store singleton + localStorage), el plan TDD del §8 (Fase 0: bootstrap Vitest+RTL+jsdom) y la convención `// TODO(backend)` del §7. Código en español (invariante #13).

### Puntos abiertos a confirmar con el cliente (no bloquean el prototipo)

- Citas normativas de BR-001..004 (estatuto docente UNLaM) — invariante #11.
- Nomenclatura definitiva de estados (`En lote` vs `Aprobado` post-Decanato).
- Concurrencia / bloqueo optimista cuando el pedido cambia de estado con un revisor mirándolo (P-09) — diferido al backend.

---

_Fin del plan. Todo lo necesario para arrancar la implementación está acá; no hace falta volver a explorar el repo salvo para verificar exports de `@ars-docendi/ui` v1.0.2 al inicio._
