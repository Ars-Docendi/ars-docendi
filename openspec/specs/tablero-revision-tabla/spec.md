# tablero-revision-tabla

## Purpose

Vista Tabla del tablero de revisión de pedidos de designación (`/designaciones/revision`): una tabla plana, alternativa a la vista Tablero (Kanban), que lista los mismos pedidos del ámbito del actor con columnas Docente, Asignatura, Novedad, Estado y Prioritario. Cubre la columna Estado que combina estado y avance en el circuito, la columna Prioritario por ícono, y el switcher para alternar entre Tabla y Tablero sobre la misma superficie.

## Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer una **vista Tabla** del tablero de revisión (`/designaciones/revision`) que liste los mismos pedidos del ámbito del actor que la vista Tablero (Kanban), en una **tabla plana** (sin divisores de grupo) ordenada por estado: En revisión → Aceptados → Devueltos → Rechazados. La tabla MUST tener las columnas **Docente**, **Asignatura**, **Novedad**, **Estado** y **Prioritario**. La vista Tabla MUST respetar los mismos filtros activos que el Tablero (ámbito, vista, tipo, prioritario). La vista MUST representar los estados Loading, Empty, Error y Success.

#### Scenario: La Tabla lista los mismos pedidos que el Tablero

- **GIVEN** un revisor con pedidos en su ámbito y unos filtros activos
- **WHEN** cambia a la vista Tabla
- **THEN** ve los mismos pedidos que en el Tablero, sujetos a los mismos filtros, dispuestos como filas ordenadas por estado (En revisión, Aceptados, Devueltos, Rechazados)

#### Scenario: Tabla sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos que cumplan los filtros activos
- **WHEN** abre la vista Tabla
- **THEN** ve el estado vacío sin filas, sin romper la navegación

### Requirement: Columna Estado que combina estado y avance

En la vista Tabla, la columna **Estado** MUST mostrar en una sola celda el estado del pedido junto con su avance en el circuito: para un pedido en revisión, un mini-stepper de 4 pasos con el paso actual resaltado y la etiqueta `En {etapa} · {paso}/4`; para `en_lote`, el stepper completo (4/4) con la etiqueta "Aceptado"; para `devuelto`, un indicador "Devuelto"; para `rechazado`, un indicador "Rechazado". El color del indicador MUST corresponder al estado.

#### Scenario: Pedido en revisión muestra etapa y avance

- **GIVEN** un pedido en `en_revision_secretaria`
- **WHEN** se renderiza su fila en la Tabla
- **THEN** la columna Estado muestra el mini-stepper con 2 de 4 pasos y la etiqueta "En Secretaría · 2/4"

#### Scenario: Pedido aceptado muestra avance completo

- **GIVEN** un pedido en `en_lote`
- **WHEN** se renderiza su fila
- **THEN** la columna Estado muestra el stepper completo (4/4) con la etiqueta "Aceptado"

#### Scenario: Pedido terminal muestra solo su estado

- **GIVEN** un pedido `devuelto` y un pedido `rechazado`
- **WHEN** se renderizan sus filas
- **THEN** la columna Estado muestra "Devuelto" y "Rechazado" respectivamente, sin mini-stepper

### Requirement: Columna Prioritario por ícono

En la vista Tabla, la columna **Prioritario** MUST mostrar un ícono de bandera únicamente en los pedidos marcados como prioritarios, y permanecer vacía en los demás. No MUST mostrar texto adicional en esa columna.

#### Scenario: Pedido prioritario muestra la bandera

- **GIVEN** un pedido marcado como prioritario
- **WHEN** se renderiza su fila
- **THEN** la columna Prioritario muestra el ícono de bandera

#### Scenario: Pedido no prioritario deja la columna vacía

- **GIVEN** un pedido no prioritario
- **WHEN** se renderiza su fila
- **THEN** la columna Prioritario queda vacía (sin ícono ni texto)

### Requirement: Switcher entre vista Tabla y Tablero

El sistema SHALL ofrecer un control (switcher) para alternar entre la vista **Tabla** y la vista **Tablero** (Kanban) sobre la misma superficie `/designaciones/revision`. La vista **Tablero** MUST ser la vista por default al entrar. La selección de vista MUST persistir mientras el usuario permanece en la superficie de revisión.

#### Scenario: El tablero abre por default en la vista Tablero

- **GIVEN** un revisor que abre `/designaciones/revision`
- **WHEN** la pantalla carga
- **THEN** ve la vista Tablero (Kanban) con el switcher en "Tablero"

#### Scenario: Cambiar a la vista Tabla

- **GIVEN** un revisor en la vista Tablero
- **WHEN** elige "Tabla" en el switcher
- **THEN** la superficie muestra la vista Tabla con los mismos pedidos y filtros, y el switcher queda en "Tabla"
