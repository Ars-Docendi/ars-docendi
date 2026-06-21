---
status: draft # draft | review | approved
owner: ""
feature: "openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md"
last_updated: 2026-06-20
---

# Design spec: Proyecto docente — pedidos y flujo de aprobación (SCRUM-7 + SCRUM-8)

## Resumen

Se diseña la experiencia del **Jefe de Cátedra** para cargar y gestionar los pedidos de designación de su cátedra dentro del período abierto: una lista "Mis pedidos" y un formulario de alta/edición con secciones que cambian según la novedad (Sin novedad / Alta / Baja / Cambio de cargo o dedicación) — **SCRUM-7**. Y se diseña el **circuito de aprobación** (Coordinador → Secretaría → Decanato, con Administración como revisor sin aprobación): un tablero de revisión tipo Kanban y un detalle role-aware con cadena de aprobación e historial — **SCRUM-8**. Es un prototipo de alta fidelidad **frontend-only con datos mockeados**.

## Roles que ven esta surface

- [x] Jefe de Cátedra (carga + reenvío de devueltos)
- [x] Coordinador de Carrera (revisión de su carrera)
- [x] Secretaría Académica (revisión depto-wide)
- [x] Decanato (revisión depto-wide, etapa final)
- [x] Administrativos (revisión sin aprobación: rechaza/devuelve)
- [ ] Docente

## Flujo principal

1. El Jefe de Cátedra entra a **Mis pedidos** (`/designaciones/mis-pedidos`) desde el ítem de navegación. Ve los pedidos del período abierto, con la precarga de los docentes del período anterior como "Sin novedad".
2. Crea uno nuevo con **Nuevo pedido** (`/designaciones/pedidos/nuevo`) o **Editar** sobre un borrador / pedido devuelto.
3. En el **formulario** completa los datos del docente y elige la novedad; el form muestra las secciones correspondientes (cargo/dedicación solicitados, adjuntos, justificación) y valida inline.
4. **Guarda el borrador**. Vuelve a Mis pedidos y el pedido aparece en estado `borrador`.
5. Desde Mis pedidos, **Enviar** pasa el borrador a `en_revision_coordinador` (inicio de la cadena de SCRUM-8). **Cancelar** (con confirmación) lo lleva a `cancelado`.
6. Tras enviar, el pedido queda de solo lectura para el JC (salvo que sea devuelto).

## Layout / IA

- **Mis pedidos**: `Breadcrumbs` + `PageHeader` (con acción "Nuevo pedido") + tabla. Cada fila: docente (nombre + DNI), materia asociada, novedad, `EstadoPedidoBadge` (+ badge "Prioritario"), y acciones contextuales por estado (Editar / Enviar / Cancelar). Modal de confirmación para cancelar.
- **Formulario de pedido**: `Breadcrumbs` + `PageHeader` + `form` en una sola columna (máx. 720px) con `fieldset`s: Datos del docente, Novedad, Solicitud (Alta/Cambio), Justificación (Cambio), Documentación obligatoria (Alta/Baja). Botonera: Cancelar (secundario) + Guardar borrador (primario).
- Mockups de referencia en `docs/product/designs/screens.pen` (frames de pedido-form Alta/Editar·Cambio y Mis pedidos).

## Estados a diseñar

| Estado            | Descripción                                                                      | Cuándo se muestra                                  |
| ----------------- | -------------------------------------------------------------------------------- | -------------------------------------------------- |
| Loading           | "Cargando tus pedidos…" en la lista; "Cargando el pedido…" en edición            | Carga inicial / refetch de la query                |
| Empty             | `InlineAlert` informativo invitando a crear el primer pedido con "Nuevo pedido"  | El JC no tiene pedidos en el período abierto       |
| Error             | `InlineAlert` de error (carga de lista, carga de pedido, o fallo de una acción)  | Falla la query o la mutation                       |
| Success           | Tabla de pedidos / formulario operativo                                          | Estado normal con datos                            |
| Awaiting approval | El pedido enviado queda read-only para el JC; el badge muestra "En revisión · …" | Tras Enviar, hasta que el circuito (SCRUM-8) actúe |

## Decisiones de diseño

- **Pedido individual por docente** dentro de un contenedor "Mis pedidos" por período (decisión del grill §4).
- **Secciones condicionales por novedad**: el form solo muestra lo aplicable, evitando ruido (Alta/Cambio piden cargo+dedicación; Alta/Baja piden adjuntos; Cambio pide justificación).
- **Validación inline bloqueante**: el submit inválido no se envía; el error aparece en el campo (`Field error`) o como `InlineAlert` (adjuntos). Las reglas mapean a BR-001..004.
- **Acciones gated por estado** (invariante #7): Editar solo en borrador/devuelto-propietario; Enviar y Cancelar solo en borrador. Nada de botones muertos.
- **Adjuntos mock**: `FileUpload` registra solo el nombre del archivo (metadata); la persistencia real en File Storage es backend (`// TODO(backend)`).
- **Cancelar con confirmación**: por ser una acción terminal, pide confirmación en un `Modal`.

## Anti-patterns a evitar (específicos de esta feature)

- Mostrar el botón "Enviar"/"Editar" en pedidos que no lo admiten por su estado (rompe invariante #7 y confunde el flujo).
- Dejar que el form envíe datos inválidos al store (la validación debe bloquear antes del submit).
- Filtrar lógica de dominio (transiciones, guards) dentro de los componentes: vive en `maquinaEstados.ts` / `pedidoValidacion.ts`; los `// TODO(backend)` solo en `api/`.
- Simular adjuntos como si se subieran a un servidor: dejar claro (hint) que es metadata mock.

## Circuito de aprobación (SCRUM-8)

### Flujo del revisor

1. Un revisor (Coordinador / Secretaría / Decanato / Administración) entra a **Revisión** (`/designaciones/revision`) desde el ítem de navegación (visible solo para esos roles).
2. Ve un **tablero Kanban (sin drag)** con los pedidos de **su ámbito** [BR-009]: el Coordinador solo los de su carrera; Secretaría/Decanato/Administración, todo el departamento.
3. Hace click en una **card** → **detalle del pedido** (`/designaciones/pedidos/:id`).
4. Si es el revisor de la etapa actual, actúa: **Aceptar** (avanza la cadena), **Rechazar** (terminal), **Devolver** (retrocede un nivel) o **Marcar prioritario** — cada acción abre un **modal** con la regla de comentario [BR-005].
5. Al **Aceptar**, el pedido avanza: Coordinador → Secretaría → Decanato → **En lote** (terminal-prototipo). Administración nunca acepta [BR-015].
6. Al **Devolver**, el pedido vuelve al actor anterior (Jefe de Cátedra / Coordinador / Secretaría) como `devuelto`; el propietario lo corrige y **reenvía**, retomando la etapa que lo devolvió [BR-014].

### Layout / IA — Tablero de revisión

- `Breadcrumbs` + `PageHeader` ("Revisión de pedidos", con el conteo del ámbito) + **grilla de 4 columnas**: **Pendiente (mi etapa)** / **Aprobado** / **Rechazado** / **Devuelto**.
- Cada columna (`ColumnaKanban`, in-app) muestra su título, la cuenta y la lista de `PedidoCard`. La columna vacía muestra un texto tenue ("Sin pedidos en tu etapa", etc.).
- Cada **`PedidoCard`** (in-app, es un `button`) muestra: docente (nombre), cátedra · carrera, novedad y `EstadoPedidoBadge` (+ badge "Prioritario"). Click → detalle.
- La columna "Pendiente (mi etapa)" se calcula con el predicado de dominio `puedeRevisar(pedido, actor)` (revisor de la etapa en su ámbito, o Administración): un pedido en una etapa que no es la del actor no aparece en su tablero.

### Layout / IA — Detalle del pedido (role-aware)

- `Breadcrumbs` + `PageHeader` (título "Pedido de {docente}", con el `EstadoPedidoBadge` como meta y, para el revisor de la etapa, la **botonera de acciones** a la derecha).
- **`Tabs`** (Solicitud / Historial / Documentos):
  - **Solicitud**: `DataList` con los datos del docente y del pedido (incl. materia asociada, horas de investigación mock, cargo/dedicación solicitados, justificación) + **cadena de aprobación** (`ApprovalTimeline`).
  - **Historial**: `AuditLog` con cada evento (actor, verbo, fecha, comentario).
  - **Documentos**: lista de adjuntos (CV / DNI frente / DNI dorso / Justificativo) o estado vacío.
- **Cadena de aprobación** (`ApprovalTimeline`): tres pasos (Coordinador de Carrera → Secretaría Académica → Decanato). El estado de cada paso (`done` / `current` / `pending` / `returned` / `rejected`) se deriva del estado y el historial del pedido (`derivarTimeline`).
- **Acciones** (solo para el revisor de la etapa en su ámbito): **Aceptar** (primary; oculto para Administración [BR-015]), **Rechazar** (destructive), **Devolver** (warning), **Marcar prioritario** (ghost). Cada una abre `ModalAccionRevision` (Modal + Textarea). El JC y los demás roles ven el detalle **de solo lectura** + timeline.
- **`ModalAccionRevision`**: el comentario es **obligatorio** en Rechazar / Devolver / Marcar prioritario [BR-005] y opcional en Aceptar; el modal bloquea el confirmar vacío y muestra el error inline. El dominio sigue siendo la autoridad (la mutation revalida).

### Gotcha de mapeo (lib en inglés ↔ dominio en español)

`@ars-docendi/ui` usa enums en inglés (`AuditVerb`: create/update/attach/approve/return/reject; `TimelineStatus`: done/current/pending/returned/rejected) — son símbolos de la lib (invariante #13, excepción de framework). La `accion` del historial va en español y se mapea español→`AuditVerb` (con etiqueta legible en español) al alimentar `AuditLog`; la cadena de etapas se mapea a `TimelineStep[]` al alimentar `ApprovalTimeline`. Esos adapters (`detalleAdapters.ts`) son funciones puras **de presentación**, no de dominio.

### Role-switching (recorrer la cadena en la demo)

El usuario **"Demo (todos los roles)"** usa el `RoleMenu` existente del TopBar para cambiar de rol sin re-loguear. El rol activo se persiste en la sesión mock y `useCurrentUser` lo observa de forma reactiva, así el `ActorContexto` que consume la capa `api/` cambia de forma coherente y el mismo pedido es visible/accionable en la etapa que corresponde a cada rol.

### Estados a diseñar — superficies de revisión

| Estado  | Tablero de revisión                         | Detalle del pedido                                              |
| ------- | ------------------------------------------- | --------------------------------------------------------------- |
| Loading | "Cargando los pedidos de tu ámbito…"        | "Cargando el pedido…"                                           |
| Empty   | `InlineAlert` "No hay pedidos para revisar" | (sin estado vacío propio; usa Error si el id no existe)         |
| Error   | `InlineAlert` de error de carga             | `InlineAlert` "No se encontró el pedido" / "fuera de tu ámbito" |
| Success | Kanban con las 4 columnas                   | DataList + ApprovalTimeline + AuditLog (+ acciones si revisor)  |

### Decisiones de diseño — SCRUM-8

- **Kanban sin drag** (decisión del grill §4): el avance es una acción con regla (comentario obligatorio en rechazo/devolución), no un movimiento libre de columna; un drag implicaría transiciones sin justificativo.
- **Autoridad en el dominio, affordance en la UI**: los botones se muestran con predicados derivados de la máquina de estados (`puedeRevisar` / `puedeAceptar`); la autoridad real la imponen los guards (etapa [BR-013] + ámbito [BR-009] + Administración-no-aprueba [BR-015]).
- **Reenvío del JC desde "Mis pedidos"**: un pedido `devuelto` al Jefe de Cátedra ofrece "Reenviar" en la tabla de Mis pedidos (además de Editar), cerrando el lazo de corrección.

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Plan maestro: [`docs/product/designs/proyecto-docente-frontend-plan.md`](./proyecto-docente-frontend-plan.md)
- Spec funcional (SCRUM-7): [`openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md`](../../../openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md)
- Spec funcional (SCRUM-8): [`openspec/changes/flujo-aprobacion-designaciones/specs/aprobacion-pedidos-designacion/spec.md`](../../../openspec/changes/flujo-aprobacion-designaciones/specs/aprobacion-pedidos-designacion/spec.md)
- Business rules: [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md)

## Open questions de diseño

- Nomenclatura definitiva del estado post-Decanato (`En lote` vs `Aprobado`) — afecta el badge a partir de SCRUM-8.
- ¿La precarga del período anterior debe traer también los adjuntos previos o solo los datos del docente? (a confirmar con el cliente).
