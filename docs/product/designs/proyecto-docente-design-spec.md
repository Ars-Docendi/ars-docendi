---
status: draft # draft | review | approved
owner: ""
feature: "openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md"
last_updated: 2026-06-20
---

# Design spec: Proyecto docente — carga de pedidos (SCRUM-7)

## Resumen

Se diseña la experiencia del **Jefe de Cátedra** para cargar y gestionar los pedidos de designación de su cátedra dentro del período abierto: una lista "Mis pedidos" y un formulario de alta/edición con secciones que cambian según la novedad (Sin novedad / Alta / Baja / Cambio de cargo o dedicación). Es un prototipo de alta fidelidad **frontend-only con datos mockeados**; el circuito de revisión (Coordinador → Secretaría → Decanato) es SCRUM-8.

## Roles que ven esta surface

- [x] Jefe de Cátedra
- [ ] Coordinador de Carrera
- [ ] Secretaría Académica
- [ ] Decanato
- [ ] Administrativos
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

## Referencias

- [`docs/product/design-principles.md`](../design-principles.md)
- Plan maestro: [`docs/product/designs/proyecto-docente-frontend-plan.md`](./proyecto-docente-frontend-plan.md)
- Spec funcional: [`openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md`](../../../openspec/changes/proyecto-docente-pedidos/specs/pedidos-designacion/spec.md)
- Business rules: [`docs/business-rules/designaciones.md`](../../business-rules/designaciones.md)

## Open questions de diseño

- Nomenclatura definitiva del estado post-Decanato (`En lote` vs `Aprobado`) — afecta el badge a partir de SCRUM-8.
- ¿La precarga del período anterior debe traer también los adjuntos previos o solo los datos del docente? (a confirmar con el cliente).
