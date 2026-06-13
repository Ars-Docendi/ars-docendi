## ADDED Requirements

### Requirement: Edición de un pedido en borrador con datos precargados

El sistema SHALL permitir abrir un Pedido de Designación existente en estado borrador con todos sus campos precargados desde la fuente de datos (en esta iteración, un store mock local). El encabezado de la pantalla SHALL identificar el pedido (número y estado borrador) y la edición SHALL reutilizar el mismo formulario que la creación.

#### Scenario: Abrir un pedido existente para editar

- **WHEN** el usuario abre la ruta de edición de un pedido existente
- **THEN** el formulario se muestra con los campos de docente, designación, justificación y documentación precargados con los datos del pedido

#### Scenario: Identificación del pedido en edición

- **WHEN** se edita un pedido en borrador
- **THEN** el encabezado muestra el número del pedido y el estado "borrador"

### Requirement: Estados de carga y error de la pantalla

El sistema SHALL representar tres situaciones de la pantalla además de la edición normal: un estado de carga inicial con skeletons que respetan la forma del formulario, y un estado de error cuando falla el autoguardado, mostrando una alerta con opciones de reintento y conservando el borrador localmente.

#### Scenario: Estado de carga inicial

- **WHEN** la pantalla está cargando los datos del pedido
- **THEN** se muestran skeletons con la forma del TOC y de las secciones del formulario, sin reflow al llegar los datos

#### Scenario: Error de autoguardado

- **WHEN** el autoguardado del borrador falla
- **THEN** se muestra una alerta de error (severidad danger) indicando que los cambios no se guardaron, con acción de reintentar, y las acciones del footer quedan deshabilitadas
