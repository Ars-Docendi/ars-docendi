## Why

En el detalle de `/designaciones/pedidos/:id`, la sección **Documentación adjunta** sólo muestra el tipo y el nombre de cada archivo. Aunque el backend ya expone una descarga autorizada y los pedidos conservan `archivoId`, los operadores no pueden inspeccionar los PDFs o imágenes necesarios para revisar el trámite.

## What Changes

- Hacer que cada adjunto disponible del detalle pueda abrirse en una pestaña nueva.
- Solicitar el contenido mediante el endpoint autorizado de Designaciones, conservando la autenticación del cliente.
- Mostrar una acción accesible por archivo, con estados de carga y error recuperable.
- Mantener los adjuntos legacy sin `archivoId` como metadata visible, sin ofrecer una descarga inexistente.
- Agregar cobertura frontend para la consulta del archivo, la apertura del contenido y los casos legacy o fallidos.
- No modificar el almacenamiento privado, las asociaciones persistidas ni el contrato HTTP existente del backend.

## Capabilities

### New Capabilities

- Ninguna. La funcionalidad pertenece al ciclo de vida y detalle existente de los pedidos de designación.

### Modified Capabilities

- `pedidos-designacion`: el detalle autorizado de un pedido debe permitir visualizar sus adjuntos disponibles y conservar los adjuntos legacy como metadata no descargable.

## Impact

- **Frontend:** `features/designaciones`, especialmente el resumen del pedido, el mapeo del DTO y la API de pedidos.
- **Backend:** se reutiliza `GET /api/designaciones/pedidos/{pedidoId}/adjuntos/{archivoId}`; no se requiere una nueva ruta ni una migración.
- **Seguridad:** las solicitudes pasan por el cliente autenticado y nunca exponen bucket, clave, URI interna ni URL permanente del almacenamiento.
- **Pruebas:** se incorporan pruebas unitarias de la API frontend y de la interacción accesible del detalle; se conserva la cobertura backend existente de autorización y streaming.
- **Rollback:** revertir el frontend elimina la acción de visualización sin afectar los archivos ni las asociaciones existentes.
