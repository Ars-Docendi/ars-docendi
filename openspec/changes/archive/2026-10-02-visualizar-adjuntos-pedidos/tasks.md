## 1. Contrato y acceso autenticado

- [x] 1.1 Agregar la operación frontend para obtener un adjunto de un pedido como contenido binario mediante `GET /api/designaciones/pedidos/{pedidoId}/adjuntos/{archivoId}`, usando el cliente Axios autenticado; verificar con una prueba unitaria que la ruta, el identificador y `responseType: "blob"` sean correctos.
- [x] 1.2 Conservar en el mapeo del pedido el estado del archivo cuando sea necesario y definir la condición de disponibilidad; verificar que un adjunto sin `archivoId` no pueda generar una solicitud de descarga.

## 2. Interfaz de visualización

- [x] 2.1 Convertir los adjuntos disponibles de `ResumenPedido` en controles accesibles con nombre que incluya tipo y archivo; verificar con pruebas de componente que cada control corresponde a un único adjunto y responde al teclado.
- [x] 2.2 Implementar la apertura del `Blob` en una pestaña nueva y la liberación de la URL temporal; verificar con pruebas que una respuesta exitosa abre el contenido y que los adjuntos legacy permanecen como metadata sin acción.
- [x] 2.3 Representar carga, error recuperable y bloqueo de la pestaña nueva sin ocultar el detalle del pedido; verificar que el operador puede reintentar y que no se informa una visualización exitosa cuando la API falla.

## 3. Verificación integrada

- [x] 3.1 Ejecutar las pruebas frontend relacionadas con Designaciones y archivos con `pnpm test:run` y confirmar que pasan las pruebas nuevas y las existentes.
- [x] 3.2 Ejecutar `pnpm lint` y `pnpm build` desde `frontend/`, confirmando que no aparecen errores de TypeScript, lint ni compilación.
- [x] 3.3 Ejecutar la prueba backend existente de descarga autorizada de Designaciones mediante el runner verificado del repositorio y confirmar que el endpoint continúa devolviendo el contenido sólo para pedidos dentro de ámbito.
- [x] 3.4 Ejecutar `git diff --check`, `openspec validate visualizar-adjuntos-pedidos --strict` y revisar el estado del cambio antes de solicitar aprobación.
