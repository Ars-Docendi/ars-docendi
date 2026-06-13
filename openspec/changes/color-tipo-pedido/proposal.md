## Why

Hoy las cuatro tarjetas del selector de "Tipo de pedido" (sección 1 del Pedido de Designación) usan un único color de selección (`--color-accent`). Las acciones tienen consecuencias muy distintas —un alta incorpora un docente, una baja lo cierra— pero visualmente son indistinguibles al seleccionarse. Un color semántico por tipo refuerza la intención de la acción y reduce el riesgo de elegir el movimiento equivocado.

## What Changes

- Cada tarjeta del radiogroup de tipo de pedido adopta, al seleccionarse, un tono semántico propio en vez del `--color-accent` único:
  - **Alta nueva** → `success` (verde): incorporación / creación.
  - **Renovación** → `info` (azul): continuidad neutral.
  - **Cambio de cargo** → `warning` (ámbar): modificación que amerita revisión.
  - **Baja** → `danger` (rojo): cierre / eliminación.
- El tono se modela como dato del catálogo de tipos (`TIPOS_PEDIDO`) y se expone como atributo en la tarjeta, manteniendo el render data-driven.
- Se reusa la paleta semántica existente del design system (`--success/info/warning/danger-{100,500,700}`); no se introducen colores nuevos.
- Sin cambios funcionales: la selección, validación y semántica del radiogroup no se alteran. Solo cambia el estado visual seleccionado.

## Capabilities

### New Capabilities

- `seleccion-tipo-pedido`: comportamiento visual del selector de tipo de pedido, incluyendo el color semántico por tipo en el estado seleccionado y la preservación de accesibilidad del radiogroup.

### Modified Capabilities

<!-- Ninguna. La capability del formulario (crear-pedido-designacion) aún vive en su change sin archivar a openspec/specs/; este cambio es aditivo y acotado al estado visual del selector. -->

## Impact

- **Frontend** (módulo Designaciones), solo presentación:
  - `frontend/src/features/designaciones/mock/mockPedido.ts` — agregar campo `tono` a cada entrada de `TIPOS_PEDIDO`.
  - `frontend/src/features/designaciones/components/SeccionTipo.tsx` — exponer el tono como `data-tono` en la tarjeta.
  - `frontend/src/features/designaciones/pedido-form.css` — reglas de tono para `.pedido-tipo-card.selected[data-tono="…"]`.
- Sin impacto en backend, API, Contracts ni base de datos. No toca el grafo de dependencias.
- Sin normativa institucional involucrada (no aplica BR-\*).
- **Rollback**: cambio aislado y reversible; revertir el commit restaura el color único previo sin efectos colaterales.
