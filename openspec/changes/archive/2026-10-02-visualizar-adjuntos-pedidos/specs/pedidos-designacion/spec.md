## ADDED Requirements

### Requirement: Visualización autorizada de adjuntos del pedido

El detalle de un pedido de designación autorizado SHALL ofrecer una acción accesible para abrir cada adjunto que tenga un `archivoId` disponible. Al activar la acción, el cliente MUST solicitar el contenido mediante la ruta autorizada del pedido y abrirlo en una pestaña nueva con el nombre y MIME devueltos por el backend, sin exponer URLs permanentes ni datos internos del almacenamiento. Un adjunto legacy sin `archivoId` SHALL conservar su tipo y nombre como metadata, pero MUST NOT ofrecer una acción de visualización.

#### Scenario: Abrir un adjunto disponible

- **GIVEN** un actor autorizado consulta un pedido que tiene un PDF o una imagen disponible con `archivoId`
- **WHEN** activa la acción del adjunto
- **THEN** el contenido se solicita mediante el endpoint autorizado del pedido y se abre en una pestaña nueva para su visualización

#### Scenario: Adjuntos legacy sólo como metadata

- **GIVEN** un pedido contiene un adjunto legacy sin `archivoId`
- **WHEN** se muestra la sección "Documentación adjunta"
- **THEN** se conserva el tipo y el nombre visibles, pero no se ofrece una acción que intente descargarlo o visualizarlo

#### Scenario: Fallo o archivo no disponible

- **GIVEN** el actor ve un adjunto con `archivoId`, pero la API responde error o el archivo dejó de estar disponible
- **WHEN** intenta abrirlo
- **THEN** no se presenta contenido como si la operación hubiera sido exitosa, el detalle permanece visible y se muestra un error accesible con posibilidad de reintentar

#### Scenario: Operación accesible por adjunto

- **GIVEN** la sección contiene varios adjuntos disponibles
- **WHEN** el operador enfoca una acción y la activa mediante teclado
- **THEN** sólo se solicita y abre el archivo correspondiente, y el control comunica de forma accesible el tipo y nombre del adjunto
