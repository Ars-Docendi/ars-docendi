# tablero-revision-tabla

## Purpose

Vista Tabla del tablero de revisión de pedidos de designación (`/designaciones/revision`): una tabla plana, única vista de la superficie, que lista los pedidos del ámbito del actor con columnas Docente, Asignatura, Novedad, Estado y Prioritario. Cubre la columna Estado que combina estado y avance en el circuito, y la columna Prioritario por ícono.

## Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer la superficie de revisión de pedidos (`/designaciones/revision`) como una
**tabla plana** (sin divisores de grupo) que lista los pedidos del ámbito del actor [BR-designaciones-009],
ordenada por estado: En revisión → Aceptados → Devueltos → Rechazados. La tabla MUST tener las
columnas **Docente**, **Asignatura**, **Novedad**, **Estado** y **Prioritario**. La vista MUST
respetar los filtros activos (vista `mis-pendientes`/`completa`, tipo de novedad, prioridad) y MUST
representar explícitamente los estados Loading, Empty, Error y Success. Esta es la **única** vista de
la superficie — no existe una vista alternativa.

#### Scenario: La Tabla lista los pedidos del ámbito, ordenados por estado

- **GIVEN** un revisor con pedidos en su ámbito y unos filtros activos
- **WHEN** abre `/designaciones/revision`
- **THEN** ve sus pedidos sujetos a los filtros activos, dispuestos como filas ordenadas por estado
  (En revisión, Aceptados, Devueltos, Rechazados)

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
