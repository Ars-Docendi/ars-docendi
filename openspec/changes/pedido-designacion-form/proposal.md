## Why

El Jefe de Cátedra hoy no tiene forma de cargar un Pedido de Designación dentro del sistema: el armado del proyecto docente (altas, renovaciones, cambios de cargo y bajas) se hace por fuera (mail, planillas). El diseño aprobado en Claude Design (handoff `04e-pedido-form.html`, wave 3) define la pantalla de **crear/editar** un pedido: un formulario de una sola página con TOC pegado a la izquierda, secciones 1–5 a la derecha y footer pegado al pie con la acción primaria. Esta pantalla es el punto de entrada del módulo Designaciones y la base sobre la que después se conecta el workflow de aprobación. Se implementa primero el front (estado local mock, sin backend) siguiendo el mismo enfoque que la feature `usuarios` ya mergeada.

## What Changes

- Nueva página `PedidoFormPage` en `frontend/src/features/designaciones/pages/` para crear/editar un Pedido de Designación, reemplazando el placeholder "Módulo en construcción".
- Layout de formulario de una sola página: TOC de secciones sticky a la izquierda (con estado por sección: hecho `✓` / error `!` / actual), columna de contenido con las 5 secciones, y footer sticky con el mensaje de validación a la izquierda y las acciones (Cancelar / Guardar borrador / Enviar a revisión) a la derecha.
- Sección **1 · Tipo de pedido**: grilla de 4 tarjetas seleccionables (Alta nueva, Renovación, Cambio de cargo, Baja). "Alta nueva" muestra el flag "requiere CV + DNI".
- Sección **2 · Datos del docente**: DNI, Nombre y apellido, Legajo, Email institucional, Teléfono. En "Alta nueva" Legajo/Email/Antigüedad quedan deshabilitados ("Se asigna al aprobar").
- Sección **3 · Designación solicitada**: Materia, Comisión, Cargo, Horas, Dedicación, Antigüedad. En "Cambio de cargo" se muestra el bloque comparativo Actual → Solicitado.
- Sección **4 · Justificación**: textarea con contador de caracteres (mín. 20 / máx. 1000) y hint de visibilidad para revisores.
- Sección **5 · Documentación**: 3 slots de carga (CV PDF, DNI frente, DNI dorso) + slot opcional de otros documentos. En "Alta nueva" la sección se tiñe de warning y los 3 slots son **obligatorios**: el botón "Enviar a revisión" queda deshabilitado hasta cargarlos.
- Banner condicional informativo cuando el tipo es "Alta nueva".
- Estados de la pantalla: **default** (editando, todo completo), **alta-nueva** (documentación obligatoria sin cargar), **loading** (skeletons que respetan la forma real), **error** (falló el autoguardado).
- Todos los controles se arman con componentes de **`@ars-docendi/ui`** (`Field`, `Input`, `Select`, `Textarea`, `FileUpload`, `InlineAlert`, `Button`, `Breadcrumbs`); las composiciones específicas de pantalla que la librería no provee (grilla de tarjetas de tipo, TOC de secciones, footer sticky) se ensamblan en la feature sobre esos primitivos + CSS local.
- Store mock local (`mockPedido.ts`) con el modelo del pedido y datos semilla, alineado al enfoque de `usuarios`.
- Nueva ruta `designaciones/pedidos/nuevo` (y `designaciones/pedidos/:id/editar`) registrada en `frontend/src/features/designaciones/routes.tsx`.

## Capabilities

### New Capabilities

- `crear-pedido-designacion`: Formulario de una sola página para crear un Pedido de Designación. Selección de tipo (4 tarjetas), datos del docente, designación solicitada, justificación con contador, y documentación. Footer sticky con acción primaria única y TOC de secciones con estado.
- `validar-documentacion-alta-nueva`: Regla de pantalla — cuando el tipo es "Alta nueva", la sección Documentación es obligatoria (CV PDF + DNI frente + DNI dorso) y el envío a revisión queda bloqueado hasta cargar los tres archivos, con feedback visual (banner, header/slots en warning, mensaje en el footer).
- `editar-pedido-designacion`: Edición de un pedido en estado borrador con datos precargados desde el store mock, incluyendo el bloque comparativo Actual → Solicitado para "Cambio de cargo".

### Modified Capabilities

_(ninguna — la feature `designaciones` solo tenía un placeholder; no hay requisitos previos que cambien)_

## Impact

- `frontend/src/features/designaciones/` — nueva página, componentes de las 5 secciones, store mock y CSS local de la feature. Se reemplaza el `IndexPage` placeholder o se agrega la página de pedido junto a él.
- `frontend/src/features/designaciones/routes.tsx` — nuevas rutas de crear/editar pedido.
- Posible entrada de navegación a "Nuevo pedido" desde el index de Designaciones (sin tocar `nav.ts` salvo que se decida agregar acceso directo en el sidebar).
- **Sin cambios de backend**: no hay llamadas HTTP reales; todo es estado local mock (mismo patrón que `usuarios`). El módulo `Modules.Designaciones` no se toca; el grafo de dependencias no cambia.
- Dependencia de UI sobre `@ars-docendi/ui` (ya instalada). La regla de obligatoriedad documental de "Alta nueva" deberá registrarse como `BR-designaciones-NNN` cuando se conecte la normativa real; en esta iteración mock se documenta como regla de pantalla en la spec.
