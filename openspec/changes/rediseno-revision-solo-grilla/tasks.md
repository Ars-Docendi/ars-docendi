## 1. Dominio — despriorizar

- [x] 1.1 En `api/maquinaEstados.ts`: agregar `AccionPedido` variante `{ tipo: "despriorizar" }`; nueva función que fija `prioritario = false` sin cambiar `estado`, sin exigir comentario, con el mismo guard de ámbito/turno que `priorizar`.
- [x] 1.2 En `api/pedidosApi.ts`: agregar `despriorizarPedido(id, actor)` (mismo patrón que `priorizarPedido`, sin parámetro `comentario` obligatorio).
- [x] 1.3 Tests en `api/maquinaEstados.test.ts`: `despriorizar` no cambia estado, no exige comentario, respeta guard de ámbito/turno, es idempotente-terminal (denegado en estados terminales).

## 2. Modal de confirmación — nueva acción

- [x] 2.1 En `components/ModalConfirmacionAccion.tsx`: agregar `"despriorizar"` a `AccionRevision`; título "Quitar prioridad", subtítulo/aviso informativo (severidad info, no warning); el campo de comentario NO es obligatorio para confirmar (a diferencia de rechazar/devolver/priorizar).
- [x] 2.2 Test en `ModalConfirmacionAccion.test.tsx`: el botón de confirmar de "despriorizar" está habilitado con el comentario vacío; confirmar dispara `onConfirmar` con el texto (posiblemente vacío).

## 3. Panel de acciones y detalle

- [x] 3.1 En `components/PanelAccionesRevision.tsx`: mostrar "Quitar prioritario" (variant ghost, como "Marcar prioritario") cuando `pedido.prioritario === true`, y "Marcar prioritario" cuando es `false` — nunca ambos.
- [x] 3.2 En `hooks/useAccionesPedido.ts`: agregar `useDespriorizarPedido` (mismo patrón que `usePriorizarPedido`, sin comentario obligatorio en la firma del mutate).
- [x] 3.3 En `pages/DetallePedidoPage.tsx`: wiring de `onDespriorizar`; agregar botón/link "Volver" persistente (siempre visible, no solo en los estados de error) que navega a `/designaciones/revision` — reusar el patrón `<a href={RUTA_REVISION}>` ya existente en los fallbacks, promovido a visible siempre (p. ej. en el `PageHeader` o antes de la cadena de aprobación).
- [x] 3.4 Reforzar visualmente `components/EstadoPedidoBadge.tsx` (tamaño/contraste vía CSS) sin cambiar su posición en el header del detalle.

## 4. Motivo de rechazo destacado en el detalle

- [x] 4.1 En `components/ResumenPedido.tsx`: cuando `pedido.estado === "rechazado"`, mostrar el motivo de rechazo (`motivoRechazo(pedido)` de `tableroRevisionModelo.ts`) destacado/citado, igual tratamiento visual al que tenía `PedidoCard`.

## 5. Sacar el Kanban

- [x] 5.1 En `pages/TableroRevisionPage.tsx`: quitar `SwitchVista`, el estado `vistaActiva` y el tipo `VistaActiva`; renderizar siempre `TablaRevision`.
- [x] 5.2 Eliminar `components/TableroRevision.tsx`, `components/ColumnaKanban.tsx`, `components/PedidoCard.tsx`, `components/SwitchVista.tsx` y sus `.test.tsx` (confirmado sin otros consumidores).
- [x] 5.3 Confirmar que `components/tableroRevisionModelo.ts` (+ test) NO se toca — sigue siendo consumido por `TablaRevision` y el detalle. (Se retiraron además `detallePedido`/`situacionPedido`, exclusivos de la card del Kanban ya eliminada, sin otros consumidores.)
- [x] 5.4 Limpiar CSS huérfano de Kanban/switcher si quedara alguno tras borrar los componentes (`revision.css` u otros).

## 6. Mockup (`screens.pen`)

- [x] 6.1 Eliminar (o mover a una sección de archivo) los frames Kanban: `q6OrQB`, `kWSjh`, `Z0S9T`.
- [x] 6.2 En `ebl4U`: quitar el `viewSwitch` (`jJTMl`, con `segTabla`/`segTablero`) del `filterCluster`, dejando solo los chips de filtro.
- [x] 6.3 En `hcCfk`: agregar botón "Volver" (junto a los breadcrumbs), agregar la acción "Quitar prioritario" en el panel de acciones (reemplazando "Priorizar novedad", el pedido de ejemplo ya es prioritario), y reforzar visualmente el badge de estado (tamaño/contraste).
- [x] 6.4 Verificar `snapshot_layout` sin problemas en los frames tocados.

## 7. Tests y cierre

- [x] 7.1 Actualizar/crear tests de `PanelAccionesRevision` (vía `pages/flujoAprobacion.test.tsx`, integración real) para cubrir marcar → quitar prioritario en un mismo flujo, rechazo con motivo destacado, y el botón Volver.
- [x] 7.2 Correr `pnpm --filter frontend lint` + suite de tests del frontend completa (127/127); confirmar que no queda ninguna referencia a `TableroRevision`/`ColumnaKanban`/`PedidoCard`/`SwitchVista`/`VistaActiva` en el código (grep, sin resultados).
- [x] 7.3 Actualizar `docs/product/designs/rediseno-designaciones-exploracion.md`: marcar el tema E como implementado (no solo parcial) y enlazar este change.
- [x] 7.4 Actualizar `docs/product/designs/proyecto-docente-design-spec.md`: reescribir las secciones de Tablero/switcher para describir la Tabla como única vista, sumar Volver/Quitar prioritario/badge reforzado/motivo destacado.
- [x] 7.5 Verificación funcional: cubierta por los tests de integración de `flujoAprobacion.test.tsx` (stack real hooks→api mock→store→maquinaEstados) — marcar/quitar prioritario, rechazo con motivo destacado, navegación del botón Volver. No se pudo hacer una pasada manual en navegador real: este entorno no tiene una herramienta de automatización de browser disponible.
