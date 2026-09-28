## MODIFIED Requirements

### Requirement: Listar períodos de designación

El sistema SHALL mostrar una tabla con todos los períodos de designación registrados, incluyendo nombre, ventana de carga (desde/hasta), ventana de impacto (desde/hasta) y estado activo/inactivo. La columna Acciones SHALL mostrar las acciones de cada fila como botones visibles directamente (Editar y Eliminar), y MUST NOT agruparlas en un menú desplegable.

#### Scenario: Lista con períodos cargados

- **WHEN** el usuario navega a `/designaciones/periodos`
- **THEN** el sistema muestra una tabla con al menos un período y las columnas: Nombre, Carga desde, Carga hasta, Impacto desde, Impacto hasta, Activo, Acciones

#### Scenario: Impacto se muestra en formato mes/año

- **WHEN** la tabla renderiza las columnas de Impacto desde/hasta
- **THEN** el sistema SHALL mostrar únicamente mes y año (ej. "Agosto 2026"), truncando el día almacenado

#### Scenario: Estado visual del período activo

- **WHEN** un período tiene `activo: true`
- **THEN** la columna Activo SHALL mostrar el texto "Activo" (de solo lectura, sin control interactivo en la tabla)

#### Scenario: Estado visual del período inactivo

- **WHEN** un período tiene `activo: false`
- **THEN** la columna Activo SHALL mostrar el texto "Inactivo" (de solo lectura, sin control interactivo en la tabla)

#### Scenario: Acciones visibles en la fila

- **WHEN** la tabla renderiza una fila de período
- **THEN** la columna Acciones SHALL mostrar un botón "Editar" y un botón de eliminar con etiqueta accesible que identifique el período
- **AND** MUST NOT mostrar un botón de menú (kebab "⋮") para acceder a esas acciones

## ADDED Requirements

### Requirement: Campos de fecha con un único ícono de calendario

Los campos de fecha del modal de período (Carga desde, Carga hasta, Impacto desde, Impacto hasta) SHALL mostrar un único ícono de calendario, ubicado dentro del borde del campo. Hacer click sobre el ícono MUST abrir el selector de fecha.

#### Scenario: Un solo ícono dentro del campo

- **GIVEN** el modal "Nuevo período" o "Editar período" abierto
- **WHEN** se renderizan los campos de fecha
- **THEN** cada campo SHALL mostrar exactamente un ícono de calendario, dentro de los límites del input

#### Scenario: El ícono abre el selector

- **GIVEN** el modal de período abierto
- **WHEN** el usuario hace click sobre el ícono de calendario de un campo de fecha
- **THEN** el sistema SHALL abrir el selector de fecha de ese campo
