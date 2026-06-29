## Context

El detalle de pedido (`DetallePedidoPage`) muestra el panel de revisión `PanelAccionesRevision`, que hoy dispara las acciones (aceptar/rechazar/devolver/priorizar) directo contra las mutations de React Query (`useAccionesPedido`), con un único textarea compartido y validación inline del justificativo obligatorio ([BR-designaciones-005]/[BR-designaciones-017]).

El diseño (`screens.pen`) define un modal de confirmación por acción (`modalAprobar`, `modalRechazar`, `modalDevolver`, `modalPriorizar`). Una iteración previa tenía un `ModalAccionRevision` que se eliminó al pasar al panel inline; este change reintroduce el modal **como capa de confirmación encima del panel inline**, no como reemplazo del panel. Es un prototipo frontend-only (mock con store en memoria/localStorage; sin backend). Existe `Modal` reusable en `@ars-docendi/ui` (v1.0.2).

## Goals / Non-Goals

**Goals:**

- Confirmación explícita por acción antes de mutar, matcheando `screens.pen` 1:1.
- Comentario editable en el modal, pre-cargado desde el textarea del panel inline.
- Mover la validación del justificativo obligatorio al modal, conservando [BR-designaciones-005] y [BR-designaciones-017].
- Componente de presentación genérico, testeable y por debajo del cap de ~300 líneas.
- Sin cambios de dominio: reglas, máquina de estados y guards de etapa/rol intactos.

**Non-Goals:**

- No se toca backend, Contracts, API ni base de datos.
- No se modifican las transiciones ni los guards (`aplicarAccion`/`maquinaEstados`).
- No se agrega confirmación a acciones fuera del panel de revisión (p. ej. "Reenviar" del Jefe de Cátedra en _Mis Pedidos_).
- No se cambian las firmas públicas de los hooks `useAccionesPedido`.

## Decisions

### Decisión 1: Componente de presentación genérico `ModalConfirmacionAccion`

Un solo componente parametrizado por la acción (`aceptar | rechazar | devolver | priorizar`), con un mapa de configuración por acción (ícono, paleta info/warning, título, subtítulo, texto de la caja de aviso, label de confirmar, obligatoriedad del justificativo, placeholder). Reusa `Modal` de `@ars-docendi/ui`.

- **Por qué:** las cuatro variantes comparten estructura (header con ícono+título+subtítulo, body con caja de aviso + textarea, footer Cancelar/Confirmar). Un mapa de config evita cuatro componentes casi idénticos y mantiene el archivo chico.
- **Alternativa descartada:** cuatro componentes separados → duplicación, más superficie de test, drift visual entre variantes.
- **Alternativa descartada:** revivir tal cual el viejo `ModalAccionRevision` → su contenido no matchea el `screens.pen` actual (cajas de aviso info/warning, subtítulos de etapa, labels nuevos).

### Decisión 2: Estado de "acción pendiente de confirmar" en `DetallePedidoPage`

`DetallePedidoPage` mantiene un estado `accionPendiente: AccionRevision | null`. El `PanelAccionesRevision` deja de mutar directo: sus botones llaman `onSolicitarAccion(accion)` con el comentario actual del panel. El page abre el modal correspondiente; al confirmar, recién ahí dispara la mutation de `useAccionesPedido` con el comentario final del modal; al cancelar, limpia `accionPendiente`.

- **Por qué:** mantiene el page como contenedor (dueño de las mutations y del routing de estado) y deja al panel y al modal como presentación (patrón container/presentational ya usado en la feature).
- **Alternativa descartada:** que `PanelAccionesRevision` sea dueño del modal y de las mutations → mezcla presentación con data-fetching y duplica la lógica de qué mutation corresponde a cada acción (que ya vive en el page).

### Decisión 3: La validación obligatoria se mueve al modal

El panel inline ya no bloquea: el botón de acción abre el modal aun con el textarea vacío, trasladando lo tipeado. La validación ([BR-designaciones-005]/[BR-designaciones-017]) ocurre en el modal: el botón de confirmar queda deshabilitado + mensaje "obligatorio" mientras el justificativo (rechazar/devolver/priorizar) esté vacío. Aceptar permite comentario vacío.

- **Por qué:** evita doble validación y doble fuente de verdad del comentario; el modal es el punto único de confirmación, coherente con el diseño (la obligatoriedad se muestra en el modal).
- **Impacto en tests:** `flujoAprobacion.test.tsx` deja de esperar el bloqueo inline y pasa a verificar el bloqueo dentro del modal (abrir modal → confirmar deshabilitado sin justificativo → tipear → confirmar).

### Decisión 4: Carry-over bidireccional simple del comentario

El comentario vive en el page (o se pasa del panel al modal al abrir). Al abrir el modal se inicializa con el texto del panel; el valor del modal es el que se envía. No se re-sincroniza al panel al cancelar (cancelar descarta la edición del modal; el panel conserva lo último tipeado en él).

- **Por qué:** comportamiento predecible y mínimo; el usuario edita en el modal y confirma, o cancela y vuelve al panel sin sorpresas.

## Risks / Trade-offs

- **[Doble textarea (panel + modal) puede confundir]** → Mitigación: el modal es claramente un paso de confirmación (overlay + aviso de efecto); el textarea del panel queda como entrada rápida y su contenido se ve reflejado en el modal. Si en QA resulta redundante, se evalúa ocultar/colapsar el textarea del panel en una iteración siguiente (fuera de alcance).
- **[El delta spec se stackea sobre `flujo-aprobacion-designaciones` no archivado]** → Mitigación: usar `ADDED Requirements` (no depende del base en `openspec/specs/`); al archivar, archivar primero flujo-aprobacion o validar el merge. Documentado en el proposal.
- **[Regresión en los tests del flujo]** → Mitigación: actualizar `flujoAprobacion.test.tsx` y agregar `ModalConfirmacionAccion.test.tsx` (red-green sobre la validación movida al modal) antes de cerrar.
- **[Drift con `@ars-docendi/ui` Modal]** → Mitigación: usar las props públicas del `Modal` (open/onOpenChange/title/footer) sin asumir internals; el contenido del body/footer lo provee la feature.

## Migration Plan

1. Agregar el frame `Designaciones - Devolver (modal)` a `screens.pen` (hecho) + actualizar el design spec.
2. Crear `ModalConfirmacionAccion` (presentación) con su config por acción + tests.
3. Cablear `DetallePedidoPage` (estado `accionPendiente`) y simplificar `PanelAccionesRevision` (disparar en vez de mutar).
4. Actualizar `flujoAprobacion.test.tsx` a la validación en el modal.
5. QA por rol + lint + build + `openspec validate --strict`.

**Rollback:** bajo riesgo (UI mock). Revertir el cableado del page y los botones del panel a la mutación directa; el componente del modal es aditivo y aislado.

## Open Questions

- Ninguna bloqueante. Pendiente menor de UX para iteración futura: si el textarea del panel inline debería colapsarse cuando el modal pasa a ser el punto de edición principal (hoy se conserva, fuera de alcance).
