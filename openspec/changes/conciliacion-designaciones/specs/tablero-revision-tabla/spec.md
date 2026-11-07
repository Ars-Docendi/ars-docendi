## MODIFIED Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer la superficie de revisión de pedidos (`/designaciones/revision`) como **una sola tabla** cuyas filas se reparten en **pestañas por área del circuito**: **Todos**, **En Cátedra**, **En Coordinación**, **En Secretaría**, **En Decanato** y **Finalizados** (Aceptados + Rechazados). Cada pestaña muestra un contador y responde a una sola pregunta: qué pedidos están hoy en esa área. Un pedido en revisión está en el área que lo revisa; un pedido **Devuelto** está en el área que debe corregirlo (`propietarioActual`), no en la etapa a la que volverá al reenviarse (`etapaRetorno`). **Todos** lista todo lo que entró al circuito, sin borradores. Al entrar, la pestaña inicial es el área propia del actor (Coordinador → En Coordinación, Decanato → En Decanato); Secretaría abre en **Finalizados**, donde exporta el lote del período, y Administración abre en Todos. El rol del actor se reconoce por su código, no por el nombre visible del rol.

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
- **THEN** la pestaña inicial corresponde a su área, a Finalizados para Secretaría o a Todos para Administración

#### Scenario: El nombre visible del rol no cambia la pestaña inicial

- **GIVEN** un actor con el rol `secretaria`, cuyo nombre visible es "Secretaría Académica"
- **WHEN** abre la vista por primera vez
- **THEN** la pestaña inicial es Finalizados

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
