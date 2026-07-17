## Context

`/designaciones/revision` hoy ofrece dos vistas intercambiables vía `SwitchVista`: **Tablero**
(Kanban, `TableroRevision` → `ColumnaKanban` → `PedidoCard`, opción D — 4 columnas por estado de
avance) y **Tabla** (`TablaRevision`, filas planas ordenadas por estado). Ambas leen los mismos
`pedidos` filtrados por `aplicarFiltros` (`filtrosTablero.ts`) y comparten `tableroRevisionModelo.ts`
(`construirColumnas`, `avancePedido`, `detallePedido`, etc.). El detalle (`DetallePedidoPage.tsx` +
`PanelAccionesRevision.tsx`) ofrece Aceptar/Rechazar/Devolver/Marcar prioritario, todas vía
`ModalConfirmacionAccion`, y no tiene botón de vuelta a Revisión salvo en los estados de error.

El pedido del cliente (ver proposal) es: sacar el Kanban y quedarse solo con la Tabla; poder
despriorizar; poder volver del detalle; y que el estado no "se pierda" visualmente en el detalle.

## Goals / Non-Goals

**Goals:**

- Dejar `/designaciones/revision` con una sola vista (Tabla), sin switcher.
- Eliminar el código exclusivo del Kanban sin dejar nada muerto ni fake UI.
- Agregar "Quitar prioritario" como acción simétrica a "Marcar prioritario", con el mismo patrón de
  confirmación por modal que las demás acciones, pero sin justificativo obligatorio.
- Agregar un botón "Volver" persistente en el detalle.
- Reforzar visualmente el badge de estado en el detalle (tamaño/contraste), sin reposicionarlo.
- Preservar el motivo de rechazo destacado que hoy vive en `PedidoCard`, moviéndolo al detalle.
- Corregir el drift de spec preexistente en `aprobacion-pedidos-designacion` (columnas por rol vs. por
  estado de avance) ya que hay que tocar ese requirement de todos modos.

**Non-Goals:**

- **No** se implementa la jerarquía de cargos (tema C): "Quitar prioritario" queda disponible para
  cualquier actor con visibilidad/turno sobre el pedido, igual que "Marcar prioritario" hoy — sin
  restricción de "solo un cargo superior puede despriorizar a otro". Esa restricción (BR P1) es el
  tema C, con su propia BR y umbral (`>` vs `≥`) a definir.
- **No** se toca "Simplificar la información" del detalle — es un juicio de diseño abierto que el doc
  de exploración deja sin resolver; no hay un criterio verificable para este change.
- **No** se tocan Historial (F), período abierto real (G) ni Datos Docente.
- **No** hay cambios de backend/API real.

## Decisions

### D-1: Eliminar el Kanban por completo, no ocultarlo tras un flag

Se borran `TableroRevision.tsx`, `ColumnaKanban.tsx`, `PedidoCard.tsx`, `SwitchVista.tsx` y sus tests,
en vez de dejarlos sin usar o detrás de un feature flag. Ningún otro componente los importa (verificado
por grep antes de este change). `tableroRevisionModelo.ts` **se conserva**: `construirColumnas`,
`avancePedido`, `detallePedido`, `situacionPedido`, etc. siguen siendo consumidos por `TablaRevision`
y por el detalle — no es código exclusivo del Kanban, es el modelo de presentación compartido.

**Alternativa descartada**: dejar el Kanban en el código pero sacarlo de la navegación. Se descartó
por el invariante "no fake UI" — código muerto sin ruta que lo use es peor que borrarlo, y ya está en
git history si hace falta recuperarlo.

### D-2: "Quitar prioritario" es una transición nueva en la máquina de estados, sin justificativo

`maquinaEstados.ts` gana `despriorizar` (mismo shape que `priorizar`: no cambia `estado`, solo el flag
— acá `prioritario = false`). A diferencia de `priorizar` (BR-designaciones-017, comentario
obligatorio), `despriorizar` **no** exige comentario: bajar la urgencia de un pedido es una acción de
menor riesgo que subirla (no hay nada que justificar ante otros revisores), y pedirlo agregaría fricción
sin beneficio claro. Mismo guard de ámbito/turno que las demás acciones de revisión — ningún actor
fuera de su etapa/ámbito puede despriorizar, igual que no puede priorizar.

**UI**: el botón "Quitar prioritario" en `PanelAccionesRevision` solo se muestra cuando
`pedido.prioritario === true` (y "Marcar prioritario" solo cuando es `false`) — nunca conviven los dos
botones a la vez.

### D-3: El botón Volver navega a `/designaciones/revision` siempre, no al `history.back()` del navegador

Un link fijo a la ruta de Revisión (no `navigate(-1)`) para que el comportamiento sea predecible sin
importar desde dónde se llegó al detalle (deep link, notificación futura, etc.) — mismo patrón que ya
usan los otros "Volver a X" del feature (`PedidoFormPage.tsx`).

### D-4: El motivo de rechazo destacado se muda a `ResumenPedido`, no se duplica en la Tabla

La Tabla (`TablaRevision`) es un listado de filas planas sin espacio para una cita larga; el detalle
(`ResumenPedido`) es donde el revisor ya lee el resto de los datos del pedido, así que es el lugar
natural para la cita destacada del motivo de rechazo — reutiliza el mismo dato
(`motivoRechazo(pedido)` de `tableroRevisionModelo.ts`) que ya usaba `PedidoCard`, solo cambia dónde
se renderiza.

### D-5: Corrección del drift de spec en `aprobacion-pedidos-designacion`

El requirement "Tablero de revisión filtrado por ámbito" describía columnas por rol ("Pendiente (mi
etapa)", singular por actor) que no coinciden con el código real (columnas por estado de avance,
iguales para todos — opción D, ya implementada desde antes de este change). Como este change ya
reescribe ese requirement (Kanban → Tabla), se aprovecha para alinear el texto a lo que el código
realmente hace, en vez de arrastrar la spec vieja a la nueva superficie.

## Risks / Trade-offs

- **[Riesgo] Perder funcionalidad real del Kanban que la Tabla no cubre** → revisado: la Tabla ya
  tiene Docente/Asignatura/Novedad/Estado(+avance)/Prioritario — la única pieza que faltaba (motivo de
  rechazo destacado) se preserva en el detalle (D-4). Ámbito y filtros (`mis-pendientes`/`completa`,
  tipo, prioridad) son transversales a ambas vistas hoy, así que la Tabla los sigue teniendo sin
  cambios.
- **[Riesgo] `despriorizar` sin justificativo se presta a idas y vueltas sin registro** → mitigado
  parcialmente: la transición igual queda registrada como evento en el historial (`AuditLog`), solo
  que sin comentario obligatorio — el "quién y cuándo" queda trazado aunque el "por qué" sea opcional.
- **[Trade-off] Tocar `aprobacion-pedidos-designacion` para corregir el drift de columnas** además del
  alcance mínimo de este change → se acepta porque el requirement que describe el Kanban de todos
  modos hay que reescribirlo (Kanban se elimina), así que el costo marginal de alinearlo al código real
  es bajo y evita dejar la spec con una inexactitud que ya existía.

## Migration Plan

1. Backend/dominio primero: `maquinaEstados.ts` (+ test) — transición `despriorizar`.
2. `ModalConfirmacionAccion.tsx` (+ test): nuevo tipo de acción `despriorizar` (título/subtítulo/aviso,
   sin campo de justificativo obligatorio).
3. `PanelAccionesRevision.tsx`: botón condicional "Quitar prioritario" / "Marcar prioritario".
4. `DetallePedidoPage.tsx`: wiring de `onDespriorizar` + botón "Volver" + refuerzo visual del badge.
5. `ResumenPedido.tsx` (+ test si aplica): motivo de rechazo destacado.
6. `TableroRevisionPage.tsx`: quitar switcher/vistaActiva, renderizar solo `TablaRevision`.
7. Eliminar `TableroRevision.tsx`, `ColumnaKanban.tsx`, `PedidoCard.tsx`, `SwitchVista.tsx` + tests.
8. Mockup (`screens.pen`): eliminar frames Kanban, quitar `viewSwitch` de `ebl4U`, actualizar `hcCfk`.
9. Specs: aplicar los deltas a `tablero-revision-tabla` y `aprobacion-pedidos-designacion`.

**Rollback**: revertir el PR restaura el Kanban, el switcher y el estado previo del detalle — sin
migraciones de datos (prototipo mock).

## Open Questions

- Ninguna bloqueante. "Simplificar la información" del detalle queda fuera de este change (juicio de
  diseño sin criterio verificable, ver Non-Goals).

## Corrección post-implementación (`mis-pedidos-simplificado`)

El botón Volver se diseñó originalmente como un link fijo a `/designaciones/revision` (D-3) asumiendo
que el único origen posible del detalle era la Tabla de revisión. Al agregar en `mis-pedidos-simplificado`
la navegación fila-clickeable de "Mis pedidos" hacia el mismo detalle, ese supuesto dejó de ser válido:
un Jefe de Cátedra que llega desde "Mis pedidos" y hace click en Volver terminaba en Revisión (una
pantalla a la que ni siquiera tiene acceso como rol no-revisor), no en "Mis pedidos". Se corrigió a
`navigate(-1)` (vuelve a la pantalla anterior real del historial), revirtiendo D-3. Ver el requirement
actualizado en `specs/aprobacion-pedidos-designacion/spec.md` de este mismo change.

## Corrección post-implementación #2: falta el botón Editar en el detalle (`mis-pedidos-simplificado`)

El detalle original (D-4 del proyecto de pedidos original, read-only salvo Volver para no-revisores)
nunca ofreció una forma de editar un borrador/devuelto propio desde ahí — solo desde la fila de "Mis
pedidos". Al agregar Eliminar al detalle (ronda 2 de `mis-pedidos-simplificado`) el header ya tenía
Volver + Eliminar pero seguía faltando Editar, un gap real: un JC que entra al detalle de su propio
borrador no tenía forma de corregirlo sin volver a "Mis pedidos" primero. Se agregó un botón **Editar**
(`variant="secondary"`, ícono `IconoSquarePen`, entre Volver y Eliminar) gateado por el mismo
`puedeEditarPedido` que ya usa la fila de "Mis pedidos" — visible en `borrador` y en `devuelto` del
propietario, a diferencia de Eliminar que solo aplica a `borrador`. Ver el requirement actualizado en
`specs/aprobacion-pedidos-designacion/spec.md` de este mismo change.
