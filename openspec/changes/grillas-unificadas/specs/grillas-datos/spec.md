## ADDED Requirements

### Requirement: Grillas sin paginación

Las grillas de datos (Mis pedidos, Revisión, Períodos, Usuarios y Docentes) SHALL mostrar todas las filas que cumplen los filtros activos, sin paginación. MUST NOT mostrar controles de paginación.

#### Scenario: Mis pedidos con más de nueve pedidos

- **GIVEN** un Jefe de Cátedra con 30 pedidos en el período
- **WHEN** abre Mis pedidos
- **THEN** la tabla muestra los 30 pedidos
- **AND** no hay botones "Anterior", "Siguiente" ni números de página

#### Scenario: Los filtros acotan la lista completa

- **GIVEN** una grilla con 150 filas
- **WHEN** el operador filtra por un encabezado
- **THEN** la tabla muestra todas las filas que cumplen el filtro

### Requirement: Scroll dentro de la tabla con encabezado fijo

Cada grilla SHALL ocupar el alto disponible de la pantalla y scrollear dentro de su contenedor, en vertical y en horizontal. El encabezado de la tabla MUST quedar visible mientras se scrollea en vertical. Ninguna columna MUST quedar cortada: si no entran en el ancho, la tabla SHALL permitir scrollear en horizontal hasta verlas.

#### Scenario: Encabezado visible al bajar

- **GIVEN** una grilla con más filas de las que entran en pantalla
- **WHEN** el operador scrollea hacia abajo dentro de la tabla
- **THEN** los encabezados, con sus filtros, siguen visibles arriba

#### Scenario: Columnas que no entran

- **GIVEN** una pantalla más angosta que el ancho total de las columnas de Usuarios
- **WHEN** el operador abre Usuarios
- **THEN** la tabla permite scrollear en horizontal y ninguna columna se ve cortada sin poder leerse

### Requirement: Click en la fila ejecuta la acción principal

Hacer click en una fila SHALL ejecutar su acción principal: abrir el detalle cuando la grilla lo tiene (Mis pedidos, Revisión), o abrir Editar cuando no (Períodos, Usuarios, Docentes). La fila MUST ser clickeable solo si el usuario puede ejecutar esa acción en esa fila, con la misma condición que muestra el botón correspondiente. El click MUST NOT dispararse cuando nace en un botón, link o control de la fila, ni cuando el usuario seleccionó texto. Una fila clickeable MUST poder enfocarse con el teclado y ejecutar su acción con Enter o Espacio, y MUST resaltarse al pasar el mouse. En las grillas con detalle no hay botón "Ver": la fila es la forma de abrirlo, y si "Ver" era su única acción, la grilla no muestra columna Acciones (Revisión). En las grillas sin detalle, el botón "Editar" se mantiene en Acciones.

#### Scenario: Revisión sin botón Ver

- **GIVEN** un Coordinador en Revisión
- **WHEN** mira la tabla
- **THEN** no hay botón "Ver" ni columna Acciones

#### Scenario: Abrir con el teclado

- **GIVEN** un usuario que navega con el teclado en Revisión
- **WHEN** enfoca una fila con Tab y presiona Enter
- **THEN** el sistema abre el detalle de ese pedido

#### Scenario: Revisión abre el detalle

- **GIVEN** un Coordinador en Revisión
- **WHEN** hace click en cualquier parte de la fila de un pedido
- **THEN** el sistema abre el detalle de ese pedido

#### Scenario: Períodos abre Editar

- **GIVEN** una Secretaría Académica en Períodos
- **WHEN** hace click en la fila de un período
- **THEN** el sistema abre el modal "Editar período" con sus datos

#### Scenario: Un botón de la fila no dispara el click de la fila

- **GIVEN** una fila de Mis pedidos de un borrador
- **WHEN** el operador hace click en "Eliminar"
- **THEN** se abre la confirmación de borrado y no se abre el detalle

#### Scenario: Seleccionar texto no abre nada

- **GIVEN** una fila de Docentes
- **WHEN** el operador arrastra para seleccionar el documento y suelta
- **THEN** no se abre ningún modal

#### Scenario: Sin permiso, la fila no es clickeable

- **GIVEN** un usuario que no puede editar docentes
- **WHEN** hace click en una fila de Docentes
- **THEN** no se abre nada y la fila no muestra cursor de mano

### Requirement: Acciones de fila con texto

Las acciones de las filas SHALL mostrarse como botones con texto ("Editar", "Desactivar", "Eliminar"). MUST NOT haber acciones representadas solo con un ícono. "Eliminar" SHALL mostrarse en color de peligro.

#### Scenario: Eliminar un borrador

- **GIVEN** un Jefe de Cátedra con un pedido en borrador en Mis pedidos
- **WHEN** mira la columna Acciones de esa fila
- **THEN** ve el botón con el texto "Eliminar", en rojo, y no una X
