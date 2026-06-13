## Context

El selector de tipo de pedido (`SeccionTipo.tsx`) renderiza las cuatro opciones como un `radiogroup` de tarjetas. Hoy el estado seleccionado (`.pedido-tipo-card.selected`) aplica un único tono `--color-accent` (fondo `--accent-100`, borde `--accent-500`, ícono y título `--accent-700`) para los cuatro tipos. El render ya es data-driven sobre `TIPOS_PEDIDO` (en `mockPedido.ts`). El form usa una paleta semántica del design system (`--success/info/warning/danger-{100,500,700}`) que ya aparece en banners, flags y estados del footer.

## Goals / Non-Goals

**Goals:**

- Dar a cada tipo un tono de selección semántico propio, reusando la paleta existente.
- Mantener el render data-driven: el tono se declara en el catálogo, no se hardcodea en el componente.
- Preservar la accesibilidad del radiogroup (color como refuerzo, no como único indicador).

**Non-Goals:**

- No se cambia la lógica de selección, validación ni la semántica del formulario.
- No se introducen colores ni tokens nuevos al design system.
- No se altera el estilo de las tarjetas no seleccionadas ni el de hover/focus.

## Decisions

### Decisión 1: Modelar el tono como dato del catálogo (`tono` en `TIPOS_PEDIDO`)

Agregar un campo `tono: TonoPedido` a cada entrada de `TIPOS_PEDIDO`, con `type TonoPedido = "success" | "info" | "warning" | "danger"`. El componente lee `t.tono` y lo emite en la tarjeta.

- **Por qué**: mantiene el render data-driven y deja el mapeo en un solo lugar. Cuando el catálogo venga del backend, el tono viaja con el tipo sin tocar el componente.
- **Alternativa descartada**: un `Record<TipoPedido, Tono>` o un `switch` en el componente — duplica la fuente de verdad del catálogo y acopla presentación a los ids.

### Decisión 2: Selección de tono vía atributo `data-tono` + CSS

El componente expone `data-tono={t.tono}` en el `<button>` de la tarjeta. El CSS agrega reglas `.pedido-tipo-card.selected[data-tono="success|info|warning|danger"]` que sobreescriben fondo/borde/ícono/título con la familia de tokens correspondiente.

- **Por qué**: mantiene el color en la capa CSS (donde ya vive el estado `.selected`), sin estilos inline ni lógica de color en TS. Es la extensión natural de la regla `.selected` actual.
- **Alternativa descartada**: estilos inline con `style={{...}}` calculados en JS — rompe la separación actual y complica el override de fondo/borde/ícono/título.

### Decisión 3: Mapeo semántico de tonos

`alta-nueva → success`, `renovacion → info`, `cambio → warning`, `baja → danger`. El criterio: el color comunica la consecuencia de la acción (crear / continuar / modificar / cerrar).

## Risks / Trade-offs

- **Contraste/legibilidad de cada tono en el estado seleccionado** → los cuatro tonos usan la misma estructura (100/500/700) que ya está validada para `accent`; al ser tokens del mismo sistema, el contraste se mantiene. Verificación visual manual en el spot-check.
- **El color como único indicador (a11y)** → mitigado: el borde y `aria-checked` siguen comunicando la selección; el tono solo refuerza. Sin daltonismo-dependencia.
- **Posible confusión `warning` (ámbar) con el flag "requiere CV + DNI"** → el flag de documentación solo aparece en `alta-nueva` (tono `success`), no en `cambio`; no coexisten en la misma tarjeta.

## Migration Plan

Cambio puramente de presentación en frontend, sin migración de datos ni de API. Deploy junto al resto del frontend. Rollback: revertir el commit restaura el color único previo, sin efectos colaterales.

## Open Questions

Ninguna.
