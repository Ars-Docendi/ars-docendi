## 1. Catálogo: tono como dato

- [x] 1.1 En `mockPedido.ts`, definir `export type TonoPedido = "success" | "info" | "warning" | "danger"`.
- [x] 1.2 Agregar el campo `tono: TonoPedido` al tipo del catálogo `TIPOS_PEDIDO` y completar cada entrada: `alta-nueva → "success"`, `renovacion → "info"`, `cambio → "warning"`, `baja → "danger"`.

## 2. Componente: exponer el tono

- [x] 2.1 En `SeccionTipo.tsx`, emitir `data-tono={t.tono}` en el `<button>` de cada tarjeta, sin tocar `role="radio"` ni `aria-checked`.

## 3. Estilos: tono semántico en estado seleccionado

- [x] 3.1 En `pedido-form.css`, agregar reglas `.pedido-tipo-card.selected[data-tono="success|info|warning|danger"]` que sobreescriban fondo (escala 100), borde (escala 500) e ícono+título (escala 700) con la familia de tokens correspondiente.
- [x] 3.2 Mantener intacto el estilo de tarjeta no seleccionada en reposo y el focus-visible.
- [x] 3.3 Agregar hover por tono sobre tarjetas no seleccionadas (`:not(.selected)[data-tono="…"]:hover`): borde escala 500 + fondo a media intensidad (`color-mix` del `-100`), sin pisar el estado seleccionado.

## 4. Verificación

- [x] 4.1 `pnpm --filter frontend lint` y `pnpm --filter frontend build` en verde.
- [ ] 4.2 Spot-check visual: seleccionar cada tipo y confirmar el tono (verde/azul/ámbar/rojo) en fondo, borde, ícono y título; confirmar contraste legible.
- [ ] 4.3 Spot-check a11y: navegación por teclado y `aria-checked` intactos; la selección se percibe sin depender del color.
- [x] 4.4 `pnpm exec openspec validate --strict color-tipo-pedido` en verde.
