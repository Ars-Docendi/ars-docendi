## Why

Hoy, en el detalle de pedido, las acciones de revisión (Aceptar, Rechazar, Devolver, Priorizar) se disparan directo desde el panel inline `PanelAccionesRevision`: un click ejecuta la mutación sin un paso de confirmación. Para acciones de alto impacto —sobre todo Rechazar (terminal) y Devolver (vuelve al Jefe de Cátedra)— esto es riesgoso: no hay confirmación explícita, no se comunica claramente el efecto de la acción ni a quién se notifica, y la validación del justificativo obligatorio ocurre de forma poco visible (inline). El diseño en `screens.pen` ya define un modal de confirmación por acción (frames `modalAprobar`, `modalRechazar`, `modalDevolver`, `modalPriorizar`); falta llevarlo al frontend.

## What Changes

- Al ejecutar **Aceptar / Rechazar / Devolver / Priorizar** desde `PanelAccionesRevision`, en lugar de mutar de inmediato, se abre un **modal de confirmación** propio de la acción que matchea `screens.pen`.
- Cada modal muestra: ícono+título de la acción, subtítulo de etapa/efecto, una **caja de aviso** (info verde para Aceptar; warning ámbar para Rechazar/Devolver/Priorizar) describiendo el efecto y a quién se notifica, y un textarea de comentario/justificativo.
- El comentario tipeado en el panel inline **se traslada al modal pre-cargado y se puede editar** ahí; lo confirmado en el modal es lo que se envía.
- La **validación del justificativo obligatorio** (Rechazar/Devolver/Priorizar, [BR-designaciones-005]) pasa a ocurrir **dentro del modal** (botón de confirmar bloqueado + error visible), reemplazando la validación inline actual.
- El modal ofrece **Cancelar** (cierra sin efecto) y **Confirmar** con label por acción: "Aprobar y enviar", "Rechazar novedad", "Devolver a Borrador", "Guardar prioridad".
- Se reintroduce un componente de presentación genérico `ModalConfirmacionAccion` (config por acción) reusando `Modal` de `@ars-docendi/ui`. El `PanelAccionesRevision` queda como disparador y conserva su textarea como entrada rápida.
- **Diseño**: se agrega el frame `Designaciones - Devolver (modal)` a `screens.pen` (no existía) y se actualiza el design spec.

No cambian las reglas de dominio ni la máquina de estados: las transiciones, los guards de etapa/rol y la obligatoriedad del justificativo ya están definidos en `aprobacion-pedidos-designacion`. Este change solo agrega la **capa de confirmación en la UI**.

## Capabilities

### New Capabilities

<!-- Ninguna capability nueva: es una mejora de UX sobre el flujo de aprobación existente. -->

### Modified Capabilities

- `aprobacion-pedidos-designacion`: se **agrega** un requisito de UX — las acciones de revisión (aceptar/rechazar/devolver/priorizar) requieren confirmación explícita vía modal, con edición del comentario y validación del justificativo obligatorio dentro del modal. Delta de tipo `ADDED Requirements` (no se modifican las reglas de dominio existentes).

> Nota: la capability `aprobacion-pedidos-designacion` vive en el change `flujo-aprobacion-designaciones` (Complete, aún sin archivar). Este delta se stackea sobre ese; al archivar, ambos mergean en `openspec/specs/aprobacion-pedidos-designacion/`.

## Impact

- **Frontend** (`frontend/src/features/designaciones/`, único surface; prototipo frontend-only mock):
  - Nuevo: `components/ModalConfirmacionAccion.tsx` + `ModalConfirmacionAccion.test.tsx`.
  - Modificado: `components/PanelAccionesRevision.tsx` (pasa a disparar el modal; mueve la validación obligatoria al modal), `pages/DetallePedidoPage.tsx` (cableado del estado de acción pendiente).
  - Tests: `pages/flujoAprobacion.test.tsx` (la confirmación/validación ahora ocurre en el modal, no inline).
  - Reusa `Modal` de `@ars-docendi/ui` (ya disponible, v1.0.2). Sin nuevas dependencias.
- **Sin impacto backend / Contracts / API / base de datos**: no se tocan módulos .NET ni endpoints. No cambia el grafo de dependencias.
- **Reglas de negocio**: respeta [BR-designaciones-005] (justificativo obligatorio en rechazar/devolver) y [BR-designaciones-017] (justificativo en priorizar). No introduce ni modifica BR-\*; solo cambia el **punto de validación en la UI**.
- **Diseño/UX**: `docs/product/designs/screens.pen` (frame Devolver agregado) + `docs/product/designs/proyecto-docente-design-spec.md` (invariante #12).
- **Rollback**: bajo riesgo (UI mock, sin estado persistido server-side). Revertir = volver a la ejecución directa desde el panel inline; el componente del modal es aditivo y aislado.
