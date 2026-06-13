## Context

El handoff de Claude Design (`04e-pedido-form.html`) es un prototipo HTML/CSS/JS con tokens propios (`tokens.css`, `components.css`) y un `AppShell` de demo. El proyecto ya tiene su propia app-shell (`frontend/src/app/shell/`), su sistema de auth/routing, y una librería de componentes **`@ars-docendi/ui`** que expone exactamente los primitivos que el diseño usa: `Field`, `Input`, `Select`, `Textarea`, `FileUpload`, `InlineAlert`, `Button`, `Breadcrumbs`. La feature `usuarios` (recién mergeada) ya estableció el patrón a copiar: página + componentes + modales sobre `@ars-docendi/ui`, estado local con un store mock, sin backend.

El objetivo es **recrear visualmente** el form de pedido dentro de ese marco, no portar la estructura interna del prototipo. La instrucción del usuario es explícita: usar `@ars-docendi/ui` para todos los componentes y no rehacerlos a mano.

## Goals / Non-Goals

**Goals:**

- Implementar la pantalla crear/editar Pedido de Designación fiel al diseño `04e`, dentro de `frontend/src/features/designaciones/`.
- Usar los primitivos de `@ars-docendi/ui` para todos los controles (campos, selects, textarea, file upload, alerts, botones, breadcrumbs).
- Reproducir los 4 estados del diseño: default (renovación completo), alta-nueva (documentación obligatoria vacía), loading, error.
- Implementar la lógica condicional de "Alta nueva": documentación obligatoria + bloqueo del envío + feedback visual.
- Mantener el patrón de la feature `usuarios`: store mock local, sin HTTP real, archivos chicos (~300 líneas cap soft).

**Non-Goals:**

- No se implementa backend ni persistencia real (sin endpoints, sin React Query todavía).
- No se implementa el workflow de aprobación (eso es la pantalla de detalle `04f`, fuera de alcance).
- No se importan `tokens.css`/`components.css` del prototipo: el theming viene de `@ars-docendi/ui` (`theme.css` + `components.css` de la lib) ya cargado por la app.
- No se modifica `@ars-docendi/ui`: si falta un primitivo, se compone en la feature, no se agrega a la lib en este change.

## Decisions

**1. Primitivos de la lib vs. composiciones de pantalla.**
La lib provee los controles atómicos; el diseño tiene 3 composiciones específicas de pantalla que la lib NO expone: la grilla de tarjetas de tipo de pedido, el TOC de secciones, y el footer sticky. Decisión: usar `@ars-docendi/ui` para todo lo atómico y ensamblar las 3 composiciones en la feature (`components/`) sobre primitivos + CSS local de la feature.

- _Tarjetas de tipo_: se construyen como un radgroup accesible. Alternativa considerada: usar `Radio` de la lib directamente (descartada visualmente — el diseño quiere tarjetas con ícono + descripción + flag, no radios en lista; pero la semántica de selección única se respeta con `role="radiogroup"`).
- _TOC y footer_: markup propio + CSS de la feature; no hay equivalente en la lib.

**2. Documentación con `FileUpload`.**
El diseño tiene 3 slots fijos (CV, DNI frente, DNI dorso) + 1 slot opcional. Decisión: una instancia de `FileUpload` por slot fijo (single-file, `accept` específico: `application/pdf` para CV, `image/*` para DNI), más un `FileUpload` `multiple` para "otros documentos". El estado `error`/`title`/`hint` del componente cubre los estados filled/missing del diseño. Alternativa: un solo `FileUpload multiple` (descartada — se pierde el etiquetado por documento requerido que el diseño necesita para la validación de alta nueva).

**3. Estado del formulario: `useState` local, no React Query.**
Siguiendo `usuarios`, el form es estado local con un store mock (`mock/mockPedido.ts`) para semillas (renovación precargada) y tipos. No hay mutaciones a servidor. Esto deja el cableado de React Query/axios para un change posterior cuando exista el endpoint. Se aísla la lógica de validación en un hook `useValidacionPedido` para que el día que entre el backend solo cambie la fuente de datos.

**4. Validación de "Alta nueva" como regla de pantalla derivada.**
El gate de envío (`puedeEnviar`) se calcula desde el tipo + presencia de los 3 documentos obligatorios. El mensaje del footer y el tinte warning de la sección Documentación se derivan del mismo cálculo (una sola fuente de verdad). Se documenta como requisito testeable en la spec; cuando se conecte la normativa real se promueve a `BR-designaciones-NNN`.

**5. Estados loading/error como variantes de render.**
`loading` y `error` se modelan como un prop/estado de la página (`estado: "edit" | "loading" | "error"`) que elige qué render mostrar, igual que el prototipo tiene `LoadingForm`/`ErrorForm`. El skeleton usa la clase de skeleton de la lib si existe, o un placeholder local. El error usa `InlineAlert severity="danger"` de la lib.

**6. Ruteo.**
`designaciones/pedidos/nuevo` y `designaciones/pedidos/:id/editar` apuntan a la misma `PedidoFormPage` (crear vs editar lo distingue el `:id`). Se agregan como children de la ruta `designaciones` existente.

## Risks / Trade-offs

- **El diseño usa fuentes/tokens propios (Inter, JetBrains Mono, escalas de color del prototipo).** → Mitigación: usar los tokens de `@ars-docendi/ui`; si hay desvío visual respecto al mock, ajustar CSS local de la feature sin tocar la lib. La fidelidad pixel-perfect cede ante consistencia con el design system real ya implementado.
- **`FileUpload` de la lib puede no soportar el layout de 3 slots etiquetados en grilla.** → Mitigación: envolver cada `FileUpload` en un contenedor de la feature con el label del documento requerido; si el componente no encaja, se compone el slot con primitivos sin modificar la lib.
- **Sin backend, la validación es solo de UI.** → Mitigación: aislar la validación en un hook puro y testeable para reusarla cuando exista el servidor; documentar el gate en la spec.
- **Archivo de página puede crecer >300 líneas** por las 5 secciones. → Mitigación: una sub-componente por sección en `components/` (SeccionTipo, SeccionDocente, SeccionDesignacion, SeccionJustificacion, SeccionDocumentacion) + `FormToc` + `FooterPedido`.

## Migration Plan

Cambio puramente frontend y aditivo: nueva página + rutas en la feature `designaciones`, sin tocar backend ni el grafo de dependencias. Rollback = revertir el PR; no hay migración de datos ni cambios de contrato. El placeholder actual de `designaciones/IndexPage` se conserva como landing del módulo; la nueva pantalla se alcanza por las rutas de pedido.

## Open Questions

- ¿La entrada "Nuevo pedido" va en el sidebar (`nav.ts`) o solo como acción desde el index de Designaciones? (Propuesta: acción desde el index; no tocar `nav.ts` en este change.)
- ¿El contador de la justificación valida mínimo 20 también para tipos distintos de alta nueva? (Propuesta: sí, aplica a todos; es campo obligatorio en las 4 variantes.)
- Cuando entre el backend, ¿el autoguardado es real (debounce a endpoint) o se mantiene el indicador como mock? (Fuera de alcance de este change.)
