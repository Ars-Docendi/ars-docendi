## ADDED Requirements

### Requirement: Prioridad junto al nombre del docente

En la Tabla de revisión, un pedido prioritario SHALL mostrar un chip "Prioritario" en color de atención (ámbar), en la misma línea del nombre del docente. La celda Estado MUST mostrar solo el estado del pedido. El chip MUST NOT ensanchar la columna Docente: en las filas prioritarias, el nombre cede ese espacio y se recorta con "…" si no entra. Los pedidos no prioritarios MUST NOT mostrar chip.

#### Scenario: Pedido prioritario

- **GIVEN** un pedido prioritario en revisión
- **WHEN** se renderiza su fila
- **THEN** la celda Docente muestra el nombre y el chip "Prioritario"
- **AND** la celda Estado muestra solo el estado, sin "Prioritario"

#### Scenario: Pedido no prioritario

- **GIVEN** un pedido no prioritario
- **WHEN** se renderiza su fila
- **THEN** la celda Docente muestra solo el nombre

## REMOVED Requirements

### Requirement: Columna Prioritario por ícono

**Reason**: La tabla ya no tiene una columna Prioritario con bandera. Se probaron el chip en Estado, una franja roja con texto bajo el nombre y separadores de grupo; July eligió un chip ámbar junto al nombre (atención, no error; sin ensanchar columnas ni cambiar el alto de fila).
**Migration**: Ver "Prioridad junto al nombre del docente".
