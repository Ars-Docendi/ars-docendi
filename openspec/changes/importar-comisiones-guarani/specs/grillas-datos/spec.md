## MODIFIED Requirements

### Requirement: Grillas sin paginación

Las grillas de datos (Mis pedidos, Revisión, Períodos, Usuarios, Docentes y Comisiones) SHALL mostrar todas las filas que cumplen los filtros activos, sin paginación. MUST NOT mostrar controles de paginación.

#### Scenario: Mis pedidos con más de nueve pedidos

- **GIVEN** un Jefe de Cátedra con 30 pedidos en el período
- **WHEN** abre Mis pedidos
- **THEN** la tabla muestra los 30 pedidos
- **AND** no hay botones "Anterior", "Siguiente" ni números de página

#### Scenario: Los filtros acotan la lista completa

- **GIVEN** una grilla con 150 filas
- **WHEN** el operador filtra por un encabezado
- **THEN** la tabla muestra todas las filas que cumplen el filtro

#### Scenario: Comisiones de un período completo

- **GIVEN** 400 comisiones importadas para el período seleccionado
- **WHEN** el actor abre Comisiones
- **THEN** la tabla muestra las 400 comisiones sin controles de paginación
