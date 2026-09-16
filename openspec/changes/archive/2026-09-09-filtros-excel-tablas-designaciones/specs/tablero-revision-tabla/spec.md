## MODIFIED Requirements

### Requirement: Filtro de pedidos por nombre o legajo del docente

El sistema SHALL ofrecer en la superficie de revisión (`/designaciones/revision`) filtros de columna para Docente, Legajo y Tipo, asociados a sus encabezados visibles. El filtro de Prioridad SHALL permanecer como filtro general opcional porque la tabla no tiene una columna independiente de Prioridad. La comparación de Docente y Legajo MUST ser por contenido, sin distinguir mayúsculas ni acentos. El filtro de Tipo SHALL permitir seleccionar una novedad; los filtros de columnas distintas SHALL combinarse mediante AND. Los criterios generales del tablero que no representan columnas visibles SHALL conservarse fuera de los encabezados: Período, Carrera, Prioridad y Sin movimiento.

#### Scenario: Filtrar por Nombre acota las filas visibles

- **GIVEN** la tabla de revisión contiene pedidos de varios docentes
- **WHEN** el revisor escribe parte del nombre en el filtro del encabezado "Docente"
- **THEN** sólo quedan visibles los pedidos cuyo docente coincide sin distinguir mayúsculas ni acentos

#### Scenario: Filtrar por Tipo (siempre visible, junto a Nombre) acota las filas visibles

- **GIVEN** la tabla contiene pedidos de Alta, Baja y Cambio
- **WHEN** el revisor selecciona "Alta" en el menú del encabezado "Tipo"
- **THEN** sólo quedan visibles los pedidos de tipo Alta en todas las pestañas aplicables

#### Scenario: Agregar el filtro opcional Legajo acota las filas visibles

- **GIVEN** existen pedidos con y sin legajo
- **WHEN** el revisor escribe un legajo en el filtro del encabezado "Legajo"
- **THEN** sólo quedan visibles los pedidos cuyo docente tiene un legajo coincidente, y los pedidos sin legajo no aparecen

#### Scenario: Un pedido de Alta sin legajo no aparece al filtrar por legajo

- **GIVEN** un pedido de Alta cuyo docente todavía no tiene legajo asignado
- **WHEN** el revisor escribe un valor en el filtro del encabezado "Legajo"
- **THEN** ese pedido no aparece en ninguna pestaña
- **AND WHEN** el filtro de Legajo se limpia o queda vacío
- **THEN** el pedido vuelve a aparecer sujeto a los demás filtros

#### Scenario: Prioridad es opcional, igual que Legajo (Tipo no — es fijo)

- **GIVEN** el filtro general de Prioridad no está aplicado
- **WHEN** el revisor agrega Prioridad y selecciona "Sólo prioritarios"
- **THEN** el resultado se acota sin que aparezca Prioridad como una columna ficticia
- **AND** el filtro de Tipo continúa disponible en el encabezado Tipo

#### Scenario: Los filtros activos se combinan entre sí

- **GIVEN** hay un filtro de Tipo activo y texto en Docente
- **WHEN** ambos criterios están aplicados
- **THEN** sólo quedan visibles los pedidos que cumplen ambas condiciones

#### Scenario: Conservar los filtros generales sin columna visible

- **GIVEN** el revisor tiene filtros activos de Período, Carrera, Prioridad o Sin movimiento
- **WHEN** agrega un filtro desde un encabezado de la tabla
- **THEN** los criterios generales se conservan y se combinan con el nuevo filtro mediante AND

#### Scenario: Los filtros acotan los contadores de pestañas

- **GIVEN** existen pedidos distribuidos en varias pestañas
- **WHEN** el revisor aplica un filtro de encabezado
- **THEN** las filas visibles y los contadores de todas las pestañas se calculan sobre las coincidencias restantes

#### Scenario: Limpiar un filtro de encabezado

- **GIVEN** el filtro del encabezado "Estado" está activo junto con otros criterios
- **WHEN** el revisor activa "Limpiar filtro" en Estado
- **THEN** se elimina sólo el criterio de Estado y los demás filtros continúan aplicados

#### Scenario: Operación accesible sin activar el orden

- **WHEN** el revisor abre con teclado el filtro de un encabezado ordenable
- **THEN** se muestra el menú del filtro sin disparar el ordenamiento de esa columna

### Requirement: Filtro por Estado en revisión

El tablero SHALL ofrecer el filtro Estado en el menú del encabezado Estado, con Todos y los estados presentes en revisión: en revisión Coordinador, Secretaría y Decanato, Devuelto, En lote, Rechazado y Cancelado. El filtro MUST combinarse con los demás filtros antes de calcular filas y contadores de pestañas. El estado SHALL ser independiente del área propietaria y de la prioridad. No SHALL existir un segundo filtro general duplicado para Estado.

#### Scenario: Filtrar devueltos

- **GIVEN** hay pedidos devueltos a distintas áreas y pedidos en revisión
- **WHEN** el revisor selecciona "Devuelto" en el menú de Estado
- **THEN** sólo se muestran los pedidos devueltos dentro de los restantes filtros y cada pestaña cuenta sus coincidencias

#### Scenario: Restablecer y consultar sin coincidencias

- **GIVEN** Estado está filtrado y existen otros criterios activos
- **WHEN** el revisor selecciona un Estado sin coincidencias
- **THEN** se muestra el estado vacío sin romper las pestañas
- **AND WHEN** el revisor limpia el filtro Estado
- **THEN** vuelven a mostrarse los estados restantes sujetos a los demás filtros conservados

## ADDED Requirements

### Requirement: Filtros por encabezado en la tabla de revisión

La tabla de revisión SHALL ofrecer filtros por encabezado para Docente, Legajo, Tipo, Inicio, Últ. actualización y Estado. Cuando la pestaña activa sea "Todos", el encabezado Área SHALL ofrecer también un filtro categórico; en las demás pestañas Área no SHALL mostrarse ni filtrarse porque es constante. Acciones MUST NOT ofrecer filtro. Los filtros textuales SHALL admitir coincidencia parcial sin distinguir mayúsculas ni acentos; las fechas SHALL compararse usando su valor temporal y las opciones múltiples SHALL combinarse con OR dentro de una columna.

#### Scenario: Filtrar una fecha desde su columna

- **GIVEN** existen pedidos con diferentes fechas de inicio o actualización
- **WHEN** el revisor busca una fecha en el menú de "Inicio" o "Últ. actualización"
- **THEN** la tabla muestra sólo los pedidos cuyo valor visible coincide y la comparación no depende del formato textual para ordenar

#### Scenario: Filtrar Área sólo en Todos

- **WHEN** el revisor selecciona "Todos"
- **THEN** el encabezado Área ofrece sus opciones disponibles y el filtro acota los pedidos
- **AND WHEN** el revisor cambia a una pestaña de etapa
- **THEN** el encabezado Área no ofrece filtro porque esa columna no está visible

#### Scenario: Cerrar un menú conserva el criterio

- **GIVEN** un filtro de encabezado está aplicado
- **WHEN** el revisor cierra el menú con Escape o clic fuera
- **THEN** el criterio sigue afectando las filas y el encabezado conserva su indicador de filtro activo
