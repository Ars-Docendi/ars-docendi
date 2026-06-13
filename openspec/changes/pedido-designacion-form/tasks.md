## 1. Andamiaje de la feature

- [x] 1.1 Crear `frontend/src/features/designaciones/mock/mockPedido.ts` con el tipo `PedidoMock` (tipo, datos del docente, designación, justificación, documentos), el enum de `TipoPedido` (`alta-nueva` | `renovacion` | `cambio` | `baja`) y datos semilla (un pedido de renovación completo para el estado default).
- [x] 1.2 Definir tipos auxiliares en `mockPedido.ts`: `DocumentoRequerido`, estado de cada slot, y helpers puros (`puedeEnviar`, `documentosFaltantes`).
- [x] 1.3 Crear `frontend/src/features/designaciones/pedido-form.css` con el CSS local de las composiciones que no provee la lib (grilla de tarjetas de tipo, TOC sticky, footer sticky, banner condicional, grilla de slots de documentación).

## 2. Composiciones de pantalla (sobre primitivos de @ars-docendi/ui)

- [x] 2.1 Implementar `components/FormToc.tsx`: TOC de las 5 secciones con estado por sección (actual / `✓` / `!`).
- [x] 2.2 Implementar `components/SeccionTipo.tsx`: grilla de 4 tarjetas seleccionables con `role="radiogroup"`, ícono, descripción y flag "requiere CV + DNI" en Alta nueva.
- [x] 2.3 Implementar `components/SeccionDocente.tsx` con `Field` + `Input` de la lib (DNI, Nombre y apellido, Legajo, Email, Teléfono); deshabilitar Legajo/Email cuando el tipo es Alta nueva.
- [x] 2.4 Implementar `components/SeccionDesignacion.tsx` con `Field` + `Select`/`Input` (Materia, Comisión, Cargo, Horas, Dedicación, Antigüedad) y el bloque comparativo "Actual → Solicitado" condicional a Cambio de cargo.
- [x] 2.5 Implementar `components/SeccionJustificacion.tsx` con `Textarea` de la lib + contador de caracteres (mín. 20 / máx. 1000).
- [x] 2.6 Implementar `components/SeccionDocumentacion.tsx` con un `FileUpload` por documento obligatorio (CV PDF, DNI frente, DNI dorso) + `FileUpload multiple` opcional; tinte warning y slots faltantes cuando el tipo es Alta nueva.
- [x] 2.6.1 Validar tamaño máximo (5 MB, constante `TAMANO_MAX_BYTES` + helper puro `excedeTamanoMaximo`) en los 4 slots de Documentación: rechazar el archivo excedido, marcar el slot en error con mensaje y derivar el hint "máx. 5 MB" de la constante.
- [x] 2.7 Implementar `components/FooterPedido.tsx`: mensaje de validación a la izquierda + acciones (Cancelar / Guardar borrador / Enviar a revisión) con `Button` de la lib, "Enviar a revisión" como única primaria.

## 3. Página y lógica de validación

- [x] 3.1 Implementar `hooks/useValidacionPedido.ts`: hook puro que deriva `puedeEnviar`, `documentosFaltantes` y el estado por sección del TOC a partir del estado del formulario.
- [x] 3.2 Implementar `pages/PedidoFormPage.tsx`: compone Breadcrumbs + encabezado (número de pedido + estado borrador) + `FormToc` + las 5 secciones + `FooterPedido`, con `useState` local y semilla del store mock.
- [x] 3.3 Cablear la lógica condicional de Alta nueva: banner (`InlineAlert`), documentación obligatoria, bloqueo de "Enviar a revisión" y mensaje del footer derivados de `useValidacionPedido`.
- [x] 3.4 Implementar los estados `loading` (skeletons con la forma del form) y `error` (alerta `InlineAlert severity="danger"` con reintento y footer deshabilitado).

## 4. Ruteo

- [x] 4.1 Agregar las rutas `pedidos/nuevo` y `pedidos/:id/editar` (ambas a `PedidoFormPage`) como children de la ruta `designaciones` en `frontend/src/features/designaciones/routes.tsx`.
- [x] 4.2 Agregar una acción "Nuevo pedido" desde el index de Designaciones que navegue a `pedidos/nuevo`.

## 5. Verificación

- [x] 5.1 Verificar build + typecheck del frontend (`pnpm --filter frontend build`) y lint sin errores.
- [x] 5.2 Verificar manualmente los 4 estados del diseño (default renovación, alta-nueva sin documentos, loading, error) y que el gate de envío de Alta nueva funcione.
- [x] 5.3 Confirmar que todos los controles usan componentes de `@ars-docendi/ui` y que no se reimplementó ningún primitivo ni se importaron los `tokens.css`/`components.css` del prototipo.
