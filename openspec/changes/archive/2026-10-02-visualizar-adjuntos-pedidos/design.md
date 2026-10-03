## Context

El problema y el alcance están definidos en `proposal.md`. Actualmente `ResumenPedido` muestra los adjuntos como texto, mientras que `PedidosController` ya expone `GET /api/designaciones/pedidos/{id}/adjuntos/{archivoId}` y `ServicioPedidosApi` valida el ámbito del actor y la pertenencia del archivo al pedido antes de devolver el stream.

El endpoint transversal de Storage también tiene una ruta de descarga, pero valida únicamente la propiedad directa del archivo. No es adecuado para revisores que pueden consultar un pedido sin ser sus propietarios. El frontend dispone de un único cliente Axios con los interceptores de autenticación de desarrollo.

## Goals / Non-Goals

**Goals:**

- Reutilizar la autorización por pedido, rol y ámbito ya implementada en Designaciones.
- Abrir PDFs e imágenes disponibles en una pestaña nueva sin publicar URLs del almacenamiento.
- Mantener la separación entre componentes presentacionales y acceso HTTP de la feature.
- Diferenciar adjuntos disponibles de metadata legacy y representar errores sin ocultar el detalle.
- Cubrir la interacción con pruebas automatizadas y conservar archivos pequeños, cercanos al límite de 300 líneas.

**Non-Goals:**

- No agregar endpoints, tablas, migraciones ni dependencias externas.
- No cambiar el flujo de carga, confirmación, antivirus o asociación de archivos.
- No convertir URI legacy en archivos descargables.
- No implementar un visor PDF propio ni una galería embebida; la pestaña nueva usará la capacidad nativa del navegador.
- No modificar la autorización backend existente salvo que una prueba revele una regresión.

## Decisions

### 1. Usar el endpoint de Designaciones

La API frontend agregará una operación específica para obtener el adjunto de un pedido como contenido binario mediante `GET /api/designaciones/pedidos/{pedidoId}/adjuntos/{archivoId}`. No se usará `/api/archivos/{archivoId}/descarga`, porque esa ruta está restringida al propietario directo y no modela el acceso de un revisor al pedido.

**Alternativa descartada:** construir la URL del objeto o usar una URL permanente de SeaweedFS. Expondría detalles internos y rompería el aislamiento definido por `almacenamiento-adjuntos`.

### 2. Solicitar el archivo con el cliente autenticado

La operación usará el cliente Axios compartido con respuesta binaria. Así se conservan los encabezados y cookies de autenticación existentes, tanto en desarrollo como en los ambientes desplegados. La API de dominio devolverá un `Blob` y la UI no recibirá claves de objeto ni URLs del proveedor.

**Alternativa descartada:** un enlace HTML directo al endpoint. Puede omitir los encabezados agregados por el interceptor de desarrollo y no permite representar de forma consistente carga, error ni reintento.

### 3. Abrir una pestaña nueva con el contenido recibido

La interacción del detalle será un control por adjunto. La acción solicitará el contenido, creará una URL temporal del `Blob` y la abrirá en una nueva pestaña para que el navegador renderice el PDF o la imagen. El ciclo de vida de la URL temporal deberá liberarla cuando ya no sea necesaria.

El componente de detalle recibirá callbacks y estado de la operación; la lógica HTTP permanecerá en la API de la feature. Cada adjunto tendrá un control independiente, con etiqueta accesible que incluya tipo y nombre.

**Alternativa descartada:** modal con visor propio. Aumentaría la superficie de UI, duplicaría capacidades del navegador y no es necesaria para el requerimiento actual.

### 4. Distinguir archivos disponibles y legacy

El mapeo frontend conservará la información de disponibilidad del DTO o, como mínimo, tratará como legacy todo adjunto sin `archivoId`. Sólo los adjuntos con identificador y estado disponible tendrán acción. Los legacy seguirán mostrándose sin provocar solicitudes de red.

**Alternativa descartada:** inferir disponibilidad desde el nombre o la URI histórica. Esa información no prueba que exista un objeto descargable y contradice la política de no inventar archivos legacy.

### 5. Errores y pestañas bloqueadas

La UI mantendrá la vista del pedido si falla la solicitud, mostrará un mensaje accesible y permitirá reintentar el mismo archivo. Si el navegador bloquea la apertura de una pestaña nueva, se informará explícitamente en lugar de presentar la operación como completada.

## Risks / Trade-offs

- **[Risk]** El navegador puede bloquear la apertura de una pestaña iniciada después de una operación asíncrona → **Mitigation:** ejecutar la acción desde un control explícito, detectar el resultado de la apertura y mostrar una instrucción de reintento si el navegador la bloquea.
- **[Risk]** Una URL temporal de `Blob` puede permanecer viva demasiado tiempo → **Mitigation:** revocarla después de entregar el contenido a la nueva pestaña, con un ciclo de vida acotado y documentado en el helper.
- **[Risk]** Un archivo puede dejar de estar disponible entre la carga del detalle y el clic → **Mitigation:** tratar cualquier respuesta no exitosa como error recuperable y no alterar el pedido mostrado.
- **[Risk]** Un cambio futuro del contrato puede omitir `archivoId` o el estado → **Mitigation:** pruebas de mapeo y condición explícita que impida intentar descargar adjuntos sin identificador.

## Migration Plan

1. Incorporar el helper de descarga y las pruebas frontend.
2. Adaptar el modelo/mapeo de adjuntos si se requiere conservar el estado devuelto por la API.
3. Agregar la acción accesible y los estados de carga/error en el detalle.
4. Ejecutar pruebas frontend, lint y build; verificar que la cobertura backend existente de descarga siga verde.
5. Desplegar sólo el frontend. El rollback consiste en revertir esos cambios frontend; no requiere cambios de base ni limpieza de objetos.

## Open Questions

No quedan decisiones bloqueantes: la interacción solicitada es abrir el contenido en una pestaña nueva y el endpoint de Designaciones existente es la frontera autorizada.
