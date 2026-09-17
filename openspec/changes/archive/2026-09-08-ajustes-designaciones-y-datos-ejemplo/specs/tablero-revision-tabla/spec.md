## ADDED Requirements

### Requirement: Filtro por Estado en revisión

El tablero SHALL ofrecer el filtro Estado con Todos y los estados presentes en revisión: en revisión Coordinador, Secretaría y Decanato, Devuelto, En lote, Rechazado y Cancelado. MUST combinarse con los demás filtros antes de calcular filas y contadores de pestañas. El estado SHALL ser independiente del área propietaria y de la prioridad.

#### Scenario: Filtrar devueltos

- **GIVEN** pedidos devueltos a distintas áreas y pedidos en revisión
- **WHEN** se elige Devuelto
- **THEN** MUST mostrarse sólo los devueltos dentro de los restantes filtros y cada pestaña MUST contar sus coincidencias

#### Scenario: Restablecer y consultar sin coincidencias

- **GIVEN** un filtro Estado sin coincidencias
- **WHEN** se consulta y luego se vuelve a Todos
- **THEN** MUST mostrarse primero el estado vacío y luego los resultados de los demás filtros conservados

### Requirement: Hover de pestañas con subrayado persistente

Las pestañas del tablero SHALL usar un verde más claro que el primario durante hover. La pestaña activa MUST conservar su subrayado inferior, y el foco por teclado MUST permanecer visible.

#### Scenario: Hover sobre pestaña activa

- **GIVEN** Finalizados como pestaña activa
- **WHEN** el puntero entra y sale de la pestaña
- **THEN** el subrayado MUST permanecer visible y el hover MUST usar verde claro

#### Scenario: Navegación por teclado

- **GIVEN** el tablero abierto
- **WHEN** se recorre la barra de pestañas mediante teclado
- **THEN** MUST distinguirse el foco y la selección sin depender del hover
