## Why

El cliente pidió simplificar la pantalla de Revisión de pedidos: hoy conviven dos vistas (Kanban
"Tablero" y "Tabla") con un switcher, cuando la Tabla ya cubre toda la información que necesita el
revisor. Además, el detalle de un pedido no tiene forma de "despriorizar" uno marcado por error, ni
un botón para volver a la lista de Revisión — hay que usar el botón atrás del navegador. Y el badge
de estado en el detalle "se pierde" visualmente en la fila del título (reclamo directo del profesor).
Este change baja a código el tema E del rediseño (ver
`docs/product/designs/rediseno-designaciones-exploracion.md`).

## What Changes

- **Eliminar la vista Tablero (Kanban) y su switcher**: `/designaciones/revision` pasa a mostrar
  únicamente la vista Tabla, sin selector. **BREAKING** para el capability `tablero-revision-tabla`
  (deja de ser "una alternativa al Tablero" para ser la única superficie) y para
  `aprobacion-pedidos-designacion` (el tablero deja de describirse como Kanban).
- **Quitar prioritario**: nueva acción en el detalle de un pedido prioritario, simétrica a "Marcar
  prioritario" pero sin justificativo obligatorio (bajar la urgencia es de menor riesgo que subirla).
  Pasa por el mismo patrón de confirmación en modal que el resto de las acciones de revisión.
- **Botón Volver**: en el detalle de un pedido (`/designaciones/pedidos/:id`), un link/botón
  persistente de vuelta a `/designaciones/revision` — hoy solo existe como fallback en los estados de
  error, no en la vista normal.
- **Peso visual del badge de estado**: en el detalle, el `EstadoPedidoBadge` se refuerza visualmente
  (tamaño/contraste) para que no se pierda en la fila del título — mismo componente, ajuste de estilo,
  sin cambiar su posición (invariante que ya cumplía).
- **Motivo de rechazo destacado, reubicado**: el Kanban (`PedidoCard`) citaba el motivo de un pedido
  rechazado en su card; al eliminar el Kanban, esa cita se muda al detalle (`ResumenPedido`), que es
  donde el revisor la puede ver de cualquier forma. El distintivo de estado "Rechazado" ya lo cubre
  `EstadoPedidoBadge` sin cambios.
- **Código a eliminar** (sin reemplazo, sin otros consumidores): `TableroRevision.tsx`,
  `ColumnaKanban.tsx`, `PedidoCard.tsx`, `SwitchVista.tsx` + sus tests; el estado `vistaActiva` y el
  tipo `VistaActiva` en `TableroRevisionPage.tsx`.
- **De paso, corrige un drift de spec preexistente** (no introducido por este change, pero que hay que
  tocar igual): `aprobacion-pedidos-designacion` describía el tablero con columnas "Pendiente (mi
  etapa) / Aprobado / Rechazado / Devuelto" (por rol), pero el código real implementa "opción D"
  (columnas por estado de avance: En revisión / Aceptados / Devueltos / Rechazados, iguales para todo
  actor). Se corrige la spec para que documente lo que el código ya hace.

## Capabilities

### New Capabilities

_(ninguna)_

### Modified Capabilities

- `tablero-revision-tabla`: dejan de existir dos vistas — la Tabla pasa a ser la única superficie de
  Revisión (se quita el requirement del switcher; se reescribe "Vista Tabla del tablero de revisión"
  para no compararse contra un Tablero que ya no existe, y para asumir directamente el filtrado por
  ámbito).
- `aprobacion-pedidos-designacion`: el requirement del tablero Kanban se reescribe para describir la
  superficie real (Tabla, columnas por estado de avance — corrige el drift de spec mencionado arriba);
  se agrega "Quitar prioritario"; el detalle suma la acción "Quitar prioritario", el botón "Volver" y
  el motivo de rechazo destacado; el requirement de confirmación por modal incluye la nueva acción.

## Impact

- **Frontend** (`frontend/src/features/designaciones/`):
  - `pages/TableroRevisionPage.tsx`: quitar `SwitchVista`/`vistaActiva`, renderizar solo `TablaRevision`.
  - Eliminar: `components/TableroRevision.tsx`, `components/ColumnaKanban.tsx`,
    `components/PedidoCard.tsx`, `components/SwitchVista.tsx` + sus `.test.tsx`.
  - `components/ModalConfirmacionAccion.tsx` + `pages/DetallePedidoPage.tsx` +
    `components/PanelAccionesRevision.tsx`: nueva acción `despriorizar`.
  - `api/maquinaEstados.ts`: nueva transición `despriorizar` (mismo patrón que `priorizar`, sin exigir
    comentario).
  - `components/ResumenPedido.tsx`: motivo de rechazo destacado cuando `estado === "rechazado"`.
  - `components/EstadoPedidoBadge.tsx` + CSS: refuerzo visual (no de posición).
  - Botón "Volver" persistente en `DetallePedidoPage.tsx`.
- **Mockup** (`docs/product/designs/screens.pen`): eliminar frames `q6OrQB`, `kWSjh`, `Z0S9T` (Kanban,
  3 variantes); quitar `viewSwitch` (`jJTMl`) del header de `ebl4U`; en `hcCfk`, agregar botón Volver,
  acción "Quitar prioritario" y reforzar el badge de estado.
- **Specs**: `openspec/specs/tablero-revision-tabla/spec.md` y
  `openspec/specs/aprobacion-pedidos-designacion/spec.md` (deltas, ver arriba).
- **Sin impacto en backend**: sigue siendo store mock + `localStorage`; no hay módulo `.NET` de
  Designaciones todavía.
- **Rollback**: cambio acotado a un feature branch del monorepo frontend, sin migraciones de datos;
  revertir el PR restaura el Kanban y el switcher sin efectos secundarios.
