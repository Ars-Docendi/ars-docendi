# tablero-revision-tabla

## Purpose

Vista Tabla del tablero de revisión de pedidos de designación (`/designaciones/revision`): una tabla plana, única vista de la superficie, que lista los pedidos del ámbito del actor con pestañas por área del circuito. Cubre las columnas, la columna Estado que combina estado y avance en el circuito, y la marca de prioridad junto al nombre del docente.

## Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer la superficie de revisión de pedidos (`/designaciones/revision`) como **una sola tabla** cuyas filas se reparten en **pestañas por área del circuito**: **Todos**, **En Cátedra**, **En Coordinación**, **En Secretaría**, **En Decanato** y **Finalizados** (Aceptados + Rechazados). Cada pestaña muestra un contador y responde a una sola pregunta: qué pedidos están hoy en esa área. Un pedido en revisión está en el área que lo revisa; un pedido **Devuelto** está en el área que debe corregirlo (`propietarioActual`), no en la etapa a la que volverá al reenviarse (`etapaRetorno`). **Todos** lista todo lo que entró al circuito, sin borradores. Al entrar, la pestaña inicial es el área propia del actor (Coordinador → En Coordinación, Secretaría → En Secretaría, Decanato → En Decanato); Administración abre en Todos.

Dentro de cada pestaña de área, el orden por defecto MUST ser: pedidos **prioritarios** primero, después los **devueltos**, después el resto; dentro de cada grupo, por fecha de última actualización **ascendente** (el que espera hace más tiempo, arriba). Dentro de **Finalizados**, los **Aceptados** MUST ir antes que los **Rechazados**; dentro de cada bloque, por fecha de última actualización **descendente**.

La tabla MUST tener las columnas **Docente**, **Legajo**, **Tipo**, **Inicio**, **Últ. actualización** y **Estado**; en la pestaña **Todos** SHALL agregar la columna **Área**. **Docente** muestra el nombre sin prefijo y la marca de prioridad junto al nombre; **Legajo** muestra el legajo del docente ("—" si todavía no tiene, caso de un Alta); **Tipo** es la novedad; **Inicio** cuenta desde el primer envío a revisión; **Últ. actualización** es la fecha del evento más reciente del historial. **Estado** MUST mostrar, para un pedido en revisión, un mini-stepper y "En {etapa} · {paso}/{total}"; para un Devuelto, el mismo formato calculado sobre `etapaRetorno` con " · Devuelto por {nombre} ({rol})" al final; para un Aceptado o un Rechazado, un punto de color y el nombre del estado, sin stepper. La vista MUST respetar los filtros activos y MUST representar explícitamente los estados Loading, Empty, Error y Success. Esta es la **única** vista de la superficie.

#### Scenario: La tabla muestra Legajo, Tipo y Últ. actualización por fila

- **GIVEN** un pedido con docente con legajo asignado y con eventos en su historial
- **WHEN** el revisor ve la fila de ese pedido
- **THEN** ve el legajo, el tipo de novedad y la fecha del evento más reciente del historial

#### Scenario: Un pedido Aceptado muestra un punto de color, sin stepper

- **GIVEN** un pedido en estado Aceptado (`en_lote`), en la pestaña Finalizados
- **WHEN** el revisor ve la fila
- **THEN** la columna Estado muestra un punto de color y "Aceptado", sin el stepper de los estados en revisión

#### Scenario: Un pedido de Alta sin legajo muestra "—"

- **GIVEN** un pedido de Alta cuyo docente todavía no tiene legajo
- **WHEN** el revisor ve la fila
- **THEN** la columna Legajo muestra "—"

#### Scenario: La Tabla lista los pedidos del ámbito, ordenados por estado

- **GIVEN** un revisor con pedidos en su ámbito y unos filtros activos
- **WHEN** abre `/designaciones/revision`
- **THEN** ve sus pedidos sujetos a los filtros activos, distribuidos en la pestaña correspondiente
  y ordenados según la prioridad y el estado de la pestaña

#### Scenario: Tabla sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos que cumplan los filtros activos
- **WHEN** abre la vista Tabla
- **THEN** ve el estado vacío sin filas, sin romper la navegación

#### Scenario: La Tabla es una sola tabla del design system, con un único head

- **WHEN** el revisor abre la vista
- **THEN** ve una única tabla con un único encabezado

#### Scenario: La Tabla abre en la etapa propia del actor

- **WHEN** un actor abre la vista por primera vez
- **THEN** la pestaña inicial corresponde a su área, o a Todos para Administración

#### Scenario: Cambiar de pestaña cambia las filas de la misma tabla

- **WHEN** el revisor cambia de pestaña
- **THEN** cambia el conjunto de filas sin crear una tabla alternativa

#### Scenario: Todos muestra el ámbito completo, sin borradores

- **WHEN** el revisor abre Todos
- **THEN** ve todos los pedidos de su ámbito salvo los borradores

#### Scenario: La Tabla no tiene columnas Carrera ni Asignatura

- **WHEN** el revisor ve una fila
- **THEN** no se muestran columnas Carrera ni Asignatura

#### Scenario: Un pedido Devuelto vive en la pestaña del área que lo tiene

- **WHEN** un pedido devuelto tiene un propietario actual
- **THEN** aparece en la pestaña de esa área

#### Scenario: Un pedido devuelto a la Cátedra sale de la pestaña que lo devolvió

- **WHEN** un pedido devuelto debe corregirse en Cátedra
- **THEN** aparece en Cátedra y no en el área que lo devolvió

#### Scenario: En Decanato nunca contiene devueltos

- **WHEN** se abre la pestaña En Decanato
- **THEN** no contiene pedidos devueltos

#### Scenario: Estado y Área son columnas separadas

- **WHEN** se muestra una fila en Todos
- **THEN** Estado y Área aparecen en columnas independientes

#### Scenario: Un pedido prioritario y devuelto a la vez muestra los dos badges

- **WHEN** un pedido es prioritario y devuelto
- **THEN** muestra ambos badges sin ocultar ninguno

#### Scenario: Inicio cuenta desde el envío a revisión, no desde la creación del borrador

- **WHEN** se calcula Inicio
- **THEN** se usa el primer envío a revisión y no la creación del borrador

#### Scenario: La pantalla abre acotada al período abierto

- **WHEN** el revisor abre la pantalla
- **THEN** el filtro Período inicia en el período abierto

#### Scenario: El filtro Carrera no se le ofrece a quien ve una sola carrera

- **WHEN** un actor solo puede ver una carrera
- **THEN** no se ofrece el filtro Carrera

#### Scenario: Ordenar por una columna y volver al orden por defecto

- **WHEN** el revisor ordena una columna y vuelve a quitar el orden
- **THEN** se restaura el orden por defecto de la pestaña

#### Scenario: Los filtros acotan las filas y también los contadores de las pestañas

- **WHEN** se aplica un filtro
- **THEN** se acotan las filas y los contadores

#### Scenario: Una pestaña sin pedidos que cumplan los filtros muestra su estado vacío

- **WHEN** una pestaña no tiene coincidencias
- **THEN** muestra su estado vacío sin romper la navegación

#### Scenario: El filtro Tipo ya no ofrece "Sin novedad"

- **WHEN** se abre el filtro Tipo
- **THEN** ofrece únicamente Alta, Baja y Cambio de cargo o dedicación

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

### Requirement: Filtro de carrera en la Tabla de revisión

El sistema SHALL ofrecer, en la superficie de revisión (`/designaciones/revision`), un filtro opcional
**Carrera** (vía "+ Añadir filtro", mismo patrón que Legajo y Prioridad) que acota los pedidos visibles
a los de la carrera seleccionada. El campo `carrera` ya existe en cada pedido [BR-designaciones-009];
este filtro lo expone como criterio de búsqueda. La tabla NO muestra una columna Carrera (ver "Vista
Tabla del tablero de revisión"): el filtro acota las filas y la carrera del pedido (una sola, ver
`pedidos-designacion`) se lee en el detalle. El `Select` MUST ofrecer un catálogo cerrado de carreras (no texto libre, no "contiene"):
**Ingeniería en Informática**, **Ingeniería Industrial**, **Ingeniería Civil**, **Ingeniería Mecánica**
e **Ingeniería Electrónica**. El filtro Carrera SHALL combinarse por AND con el resto de los filtros
activos (vista, Nombre, Tipo, Legajo, Prioridad).

#### Scenario: Filtrar por Carrera acota las filas visibles

- **GIVEN** la Tabla de revisión con pedidos de varias carreras en el ámbito del actor (p. ej.
  Secretaría, que ve todo el departamento)
- **WHEN** el revisor agrega el filtro "Carrera" y elige "Ingeniería Industrial"
- **THEN** solo quedan visibles los pedidos cuyo `carrera` es exactamente "Ingeniería Industrial", en
  cualquiera de las pestañas

#### Scenario: El filtro Carrera es opcional, con el mismo patrón que Legajo y Prioridad

- **GIVEN** el filtro colapsado (sin Carrera agregada)
- **WHEN** el revisor elige "Carrera" en el selector "+ Añadir filtro"
- **THEN** aparece el `Select` de carrera, y aplicarlo acota la lista a los pedidos que coinciden
  exactamente
- **AND** puede quitarlo con el botón "×", volviendo a ver todos los pedidos sujetos al resto de los
  filtros activos

#### Scenario: El filtro Carrera se combina con los demás filtros activos

- **GIVEN** un filtro de Tipo activo (p. ej. "Alta") y el filtro Carrera en "Ingeniería Civil"
- **WHEN** ambos están aplicados a la vez
- **THEN** solo quedan visibles los pedidos que cumplen AMBAS condiciones

### Requirement: Filtro de pedidos por nombre o legajo del docente

El sistema SHALL ofrecer en la superficie de revisión (`/designaciones/revision`) filtros de columna para Docente, Legajo y Tipo, asociados a sus encabezados visibles. El filtro de Prioridad SHALL permanecer como filtro general opcional porque la tabla no tiene una columna independiente de Prioridad. La comparación de Docente y Legajo MUST ser por contenido, sin distinguir mayúsculas ni acentos. El filtro de Tipo SHALL permitir seleccionar una novedad; los filtros de columnas distintas SHALL combinarse mediante AND. Los criterios generales del tablero que no representan columnas visibles SHALL conservarse fuera de los encabezados: Período, Carrera, Prioridad y Sin movimiento.

#### Scenario: Filtrar por Nombre acota las filas visibles

- **GIVEN** la Tabla de revisión con pedidos de varios docentes en el ámbito
- **WHEN** el revisor escribe parte del nombre en el filtro del encabezado "Docente"
- **THEN** sólo quedan visibles los pedidos cuyo docente coincide sin distinguir mayúsculas ni acentos

#### Scenario: Filtrar por Tipo (siempre visible, junto a Nombre) acota las filas visibles

- **GIVEN** la Tabla de revisión con pedidos de varios tipos de novedad en el ámbito
- **WHEN** el revisor selecciona "Alta" en el menú del encabezado "Tipo"
- **THEN** sólo quedan visibles los pedidos de tipo Alta en todas las pestañas aplicables

#### Scenario: Agregar el filtro opcional Legajo acota las filas visibles

- **GIVEN** el filtro colapsado (sin Legajo agregado) y pedidos de docentes con legajo asignado
- **WHEN** el revisor escribe un legajo en el filtro del encabezado "Legajo"
- **THEN** sólo quedan visibles los pedidos cuyo docente tiene un legajo coincidente, y los pedidos sin legajo no aparecen

#### Scenario: Un pedido de Alta sin legajo no aparece al filtrar por legajo

- **GIVEN** un pedido de Alta cuyo docente todavía no tiene legajo asignado, y el filtro Legajo
  agregado con texto
- **WHEN** el revisor escribe un valor en el filtro de Legajo
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
- **WHEN** ambos están aplicados a la vez
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

- **GIVEN** pedidos devueltos a distintas áreas y pedidos en revisión
- **WHEN** se elige Devuelto
- **THEN** MUST mostrarse sólo los devueltos dentro de los restantes filtros y cada pestaña MUST contar sus coincidencias

#### Scenario: Restablecer y consultar sin coincidencias

- **GIVEN** Estado está filtrado y existen otros criterios activos
- **WHEN** el revisor selecciona un Estado sin coincidencias
- **THEN** se muestra el estado vacío sin romper las pestañas
- **AND WHEN** el revisor limpia el filtro Estado
- **THEN** vuelven a mostrarse los estados restantes sujetos a los demás filtros conservados

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
