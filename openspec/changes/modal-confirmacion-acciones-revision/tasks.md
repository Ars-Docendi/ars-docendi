## 1. Diseño y design spec

- [x] 1.1 Agregar el frame `Designaciones - Devolver (modal)` a `docs/product/designs/screens.pen` con paleta warning, justificativo obligatorio y botones Cancelar / "Devolver a Borrador" (hecho durante el planning; verificar que quedó persistido).
- [x] 1.2 Actualizar `docs/product/designs/proyecto-docente-design-spec.md` documentando el modal de confirmación por acción (Aceptar/Rechazar/Devolver/Priorizar) y el flujo de confirmación + edición del comentario.

## 2. Componente `ModalConfirmacionAccion` (presentación) — TDD

- [x] 2.1 Escribir `components/ModalConfirmacionAccion.test.tsx` (red): abre con la config de cada acción; confirmar deshabilitado sin justificativo para rechazar/devolver/priorizar [BR-designaciones-005]/[BR-designaciones-017]; aceptar permite confirmar con comentario vacío; textarea pre-cargado editable; Cancelar dispara `onCerrar` sin confirmar.
- [x] 2.2 Crear `components/ModalConfirmacionAccion.tsx` reusando `Modal` de `@ars-docendi/ui`, con el mapa de config por acción (ícono, paleta info/warning, título, subtítulo, texto de aviso, label de confirmar, obligatoriedad, placeholder) matcheando `screens.pen`. Props: `accion`, `pedido`, `comentarioInicial`, `enviando`, `onConfirmar(comentario)`, `onCerrar`.
- [x] 2.3 Estado interno del comentario inicializado desde `comentarioInicial`; validación de obligatoriedad (bloquea confirmar + mensaje) según la acción; `key`/reset al cambiar de acción.
- [x] 2.4 Estilos del modal (CSS de la feature) acordes a las cajas info/warning y a los botones por acción; verificar contraste y match con el diseño.

## 3. Cableado en el detalle

- [x] 3.1 Modificar `pages/DetallePedidoPage.tsx`: estado `accionPendiente: AccionRevision | null` + comentario; abrir `ModalConfirmacionAccion` según la acción; al confirmar disparar la mutation correspondiente de `useAccionesPedido` con el comentario del modal; al cancelar limpiar el estado.
- [x] 3.2 Simplificar `components/PanelAccionesRevision.tsx`: los botones pasan a llamar `onSolicitarAccion(accion)` (en vez de mutar/validar inline), trasladando el comentario tipeado; remover la validación de obligatoriedad inline (se mueve al modal); conservar el textarea como entrada rápida.
- [x] 3.3 Verificar que el guard de etapa/rol y `permiteAceptar` ([BR-designaciones-015]) siguen ocultando/deshabilitando las acciones no permitidas antes de abrir el modal.

## 4. Tests del flujo

- [x] 4.1 Actualizar `pages/flujoAprobacion.test.tsx`: la confirmación/validación ocurre en el modal — aceptar/rechazar/devolver abren modal y se ejecutan al confirmar; devolver/rechazar sin justificativo quedan bloqueados dentro del modal; Cancelar no muta.
- [x] 4.2 Verificar que `MisPedidosPage.test.tsx` y `TableroRevision.test.tsx` no rompen (el reenvío del JC y el tablero no cambian).

## 5. QA, validación y cierre

- [x] 5.1 `pnpm --filter frontend lint` + `pnpm --filter frontend build` verdes.
- [x] 5.2 Suite de tests de la feature verde (`pnpm --filter frontend test` o vitest del feature).
- [x] 5.3 Spot-check manual por rol (Coordinador / Secretaría / Decanato / Administración): cada acción abre su modal, valida y confirma como en `screens.pen` (verificado por screenshot headless de los 4 modales).
- [x] 5.4 `pnpm exec openspec validate --strict modal-confirmacion-acciones-revision` verde.
- [x] 5.5 Correr `/evaluate` contra spec + grading-criteria y actualizar el scorecard.
