## MODIFIED Requirements

### Requirement: Grillas sin paginación

Las grillas de datos (Mis pedidos, Revisión, Períodos, Usuarios y Docentes) SHALL mostrar todas las filas que cumplen los filtros activos, sin paginación. MUST NOT mostrar controles de paginación. Excepción: por debajo de 560 px la lista de tarjetas pagina de a 20 (ver `listas-movil`); la tabla de escritorio no cambia.

#### Scenario: Mis pedidos con más de nueve pedidos

- **GIVEN** un Jefe de Cátedra con 30 pedidos en el período
- **WHEN** abre Mis pedidos en escritorio
- **THEN** la tabla muestra los 30 pedidos
- **AND** no hay botones "Anterior", "Siguiente" ni números de página

#### Scenario: Los filtros acotan la lista completa

- **GIVEN** una grilla con 150 filas
- **WHEN** el operador filtra por un encabezado
- **THEN** la tabla muestra todas las filas que cumplen el filtro

### Requirement: Scroll dentro de la tabla con encabezado fijo

Cada grilla SHALL ocupar el alto disponible de la pantalla y scrollear dentro de su contenedor, en vertical y en horizontal. El encabezado de la tabla MUST quedar visible mientras se scrollea en vertical. Ninguna columna MUST quedar cortada: si no entran en el ancho, la tabla SHALL permitir scrollear en horizontal hasta verlas. Por debajo de 560 px la grilla se reemplaza por tarjetas y, donde se conserve una tabla, scrollea la página y no la tabla en vertical (ver `listas-movil`).

#### Scenario: Encabezado visible al bajar

- **GIVEN** una grilla con más filas de las que entran en pantalla
- **WHEN** el operador scrollea hacia abajo dentro de la tabla
- **THEN** los encabezados, con sus filtros, siguen visibles arriba

#### Scenario: Columnas que no entran

- **GIVEN** una pantalla más angosta que el ancho total de las columnas de Usuarios
- **WHEN** el operador abre Usuarios
- **THEN** la tabla permite scrollear en horizontal y ninguna columna se ve cortada sin poder leerse
