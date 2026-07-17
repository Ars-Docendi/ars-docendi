## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: Switcher entre vista Tabla y Tablero

**Reason**: la vista Tablero (Kanban) se elimina — el cliente pidió simplificar la superficie de
Revisión a una sola vista. Sin una segunda vista, un switcher no tiene sentido.

**Migration**: `/designaciones/revision` renderiza directamente la Tabla, sin selector. No hay acción
de usuario a migrar (la Tabla ya era una de las dos opciones existentes, ahora es la única).
