## MODIFIED Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer la superficie de revisión de pedidos (`/designaciones/revision`) como **una
sola tabla** construida con el componente `Table` del design system —el mismo que usan Usuarios,
Docentes, Roles y Períodos—, con **pestañas** (`Tabs`) por etapa del circuito encima. Las pestañas
SHALL ser cinco, en orden: **Mi bandeja**, **En Coordinación**, **En Secretaría**, **En Decanato** y
**Finalizados**, cada una con el contador de pedidos que contiene bajo los filtros activos.

La Tabla NO MUST usar una grilla propia de `div`/`span`: era la única superficie del sistema que no
usaba `Table`, lo que la dejaba sin semántica de tabla para lectores de pantalla, sin el ordenamiento
por columna que `Table.HeaderCell` ya provee, y con anchos de columna calibrados a mano que había que
recalcular en cada cambio.

Las pestañas MUST reemplazar a las cuatro secciones desplegables previas. Las secciones repetían el
head de columnas cuatro veces, impedían comparar u ordenar filas entre etapas, y le sacaban el trabajo
a la columna de estado (dentro de la sección "En Coordinación", una celda que dice "En Coordinación"
no aporta nada). Las pestañas conservan lo que las secciones sí daban —el conteo por etapa y abrir
parado en la propia— con un solo head y una sola tabla.

Un pedido **Devuelto** NO MUST tener pestaña propia: cae en la de la etapa a la que vuelve
(`etapaRetorno`), que es donde queda trabado hasta que se corrija y reenvíe.

La pestaña **Todos** SHALL mostrar el ámbito del actor sin agrupar. MUST excluir los `borrador` —
todavía no entraron al circuito, son del Jefe de Cátedra que los está escribiendo— y NO MUST filtrar
nada más, de modo que ningún pedido del ámbito quede invisible. Esto cubre en particular los
`cancelado`, que con el agrupamiento por etapa no caían en ninguna pestaña.

NO MUST existir una pestaña "Mi bandeja" (los pedidos en turno del actor): para un revisor sería lo
mismo que la pestaña de su propia etapa, con dos rótulos distintos para el mismo conjunto. El filtro
`vista` ("completa" / "mis-pendientes"), que era un `Select` suelto en la cabecera, queda igualmente
eliminado: lo reemplazan las pestañas por etapa.

La pestaña **Finalizados** SHALL incluir los tres estados terminales: `en_lote` (Aceptado),
`rechazado` y `cancelado`.

La Tabla MUST abrir en la pestaña de la etapa propia del actor (Coordinador → "En Coordinación",
Secretaría → "En Secretaría", Decanato → "En Decanato"). Administración no tiene etapa propia —ve todo
el departamento por igual—, así que MUST abrir en "Todos".

Las columnas MUST ser: **Docente**, **Legajo**, **Tipo**, **Inicio**, **Últ. actualización**,
**Estado** y **Acciones** — mismo esqueleto que las demás tablas del sistema (identidad → atributos →
Estado → Acciones). En la pestaña **Todos** MUST sumarse una columna **Área** entre Estado y Acciones.

**Estado** y **Área** MUST ser columnas separadas: el estado por sí solo ("En revisión", "Devuelto") y
el área donde el pedido está hoy ("Cátedra", "Coordinación", "Secretaría", "Decanato"). Meter el área
dentro del badge de Estado la repetía en cada fila.

La columna **Área** MUST existir SOLO en la pestaña "Todos". En una pestaña de área su valor sería el
mismo en todas las filas y ya lo dice la pestaña; en Finalizados no hay área que mostrar. MUST
derivarse igual que el reparto en pestañas —la etapa que lo revisa, o el `propietarioActual` si está
devuelto— de modo que columna y pestaña nunca se contradigan. En los estados terminales MUST mostrar
"—": un pedido cerrado ya no está en ninguna parte del circuito.

La Tabla NO MUST tener columnas **Carrera** ni **Asignatura**: una designación puede abarcar más de
una carrera y más de una asignatura, así que una celda de grilla no puede mostrarlas completas sin
mentir por omisión. Ambas se ven enteras en el detalle del pedido; **Carrera** sigue existiendo como
filtro.

La celda **Estado** MUST usar `StatusBadge` del design system (vía `EstadoPedidoBadge`), nunca un chip
propio: la librería ya define los `kind` `devuelto`, `aprobado`, `rechazado` y `prioritario`. Un pedido
prioritario MUST sumar el badge "Prioritario" al lado del badge de estado — los dos a la vez, sin que
uno tape al otro.

La **fila** NO MUST llevar fondo de color por estado. Un fondo por fila repetía señales que ya viven en
la celda Estado y forzaba una prevalencia arbitraria entre prioritario y devuelto cuando un pedido era
las dos cosas, tapando una de las dos.

El badge de la celda Estado NO MUST llevar el área: eso es la columna Área. Para los estados de
revisión eso ya lo dice la etiqueta por defecto ("En revisión · Secretaría"). Para un **Devuelto**,
"Devuelto" a secas no alcanza: el pedido está en manos del área que lo tiene que corregir
(`propietarioActual`), NO en la etapa a la que va a volver (`etapaRetorno`). El badge MUST decir
"Devuelto · en {área}", con el mismo vocabulario que las pestañas (Cátedra / Coordinación / Secretaría
/ Decanato) — de un devuelto se hace cargo el **área**, no una persona en particular. Sin
`propietarioActual` declarado, MUST caer al genérico "Devuelto".

Las columnas **Inicio** y **Últ. actualización** MUST mostrar solo la fecha (dd/mm/aaaa). **Inicio**
MUST tomar el primer evento `enviar` del historial (entrada al circuito), NO el `crear` del borrador:
el tiempo que un pedido estuvo guardado sin enviar no es tiempo de revisión y haría incomparables dos
filas.

Los filtros MUST vivir **arriba de las pestañas**, no dentro de la tabla: se aplican al ámbito entero
—los contadores de las pestañas ya salen filtrados— así que ubicarlos dentro de la tabla los haría
leer como si fueran de la pestaña abierta. MUST acotar tanto las filas visibles como los contadores.

**Nombre**, **Tipo** y **Período** SHALL ser campos fijos siempre visibles; **Legajo**, **Prioridad**,
**Sin movimiento** y **Carrera** SHALL ser opcionales vía "+ Añadir filtro". Mostrar los siete a la vez
satura la cabecera; los que se agregan quedan visibles como campos con su "×", así que no hay filtros
activos escondidos.

El filtro **Período** SHALL ofrecer los períodos de designación creados y acotar por el `periodoId` del
pedido. Es preferible a un rango de fechas libre: los períodos son una entidad del dominio, ya están
creados y nombrados ("1er cuatrimestre 2026"), y el pedido ya los referencia.

**Período** MUST abrir en el período **abierto** (`activo`, del que solo puede haber uno a la vez), no
en "Todos": un revisor trabaja sobre el período en curso y las designaciones de cuatrimestres cerrados
son ruido. La opción "Todos los períodos" MUST existir, al final de la lista, y el período abierto MUST
rotularse como tal. Precisamente por arrancar aplicado, **Período** MUST ser un filtro fijo y no uno
opcional: un filtro que acota desde el inicio no puede estar escondido detrás de "+ Añadir filtro", o
el usuario vería una lista recortada sin saber por qué. Si no hubiera ningún período abierto, MUST caer
a "Todos".

El filtro **Sin movimiento** SHALL acotar a los pedidos que llevan más de 7, 15 o 30 días sin que su
historial registre un evento. Responde "¿qué está trabado?" sin poner un contador de días en cada fila.

El filtro **Carrera** SHALL ofrecerse SOLO a los actores que ven más de una carrera. Para un
Coordinador, cuyo ámbito ES una carrera [BR-designaciones-009], no acotaría nada.

El filtro **Tipo** SHALL ofrecer únicamente las tres novedades vigentes ("Alta", "Baja", "Cambio de
cargo o dedicación"). La vista MUST representar explícitamente los estados Loading, Empty, Error y
Success. Esta es la **única** vista de la superficie.

Los headers de las columnas **Docente**, **Legajo**, **Tipo**, **Inicio**, **Últ. actualización** y
**Estado** SHALL ser ordenables, usando el `sort` / `onSortChange` que `Table.HeaderCell` ya provee. El
ciclo MUST ser ascendente → descendente → **sin orden manual**: el orden por defecto de la pestaña
(prioritarios, después devueltos, después el que espera hace más tiempo) es información, no un orden
arbitrario, así que el usuario tiene que poder volver a él. Las fechas MUST ordenarse
cronológicamente y los legajos como números, no por su texto.

#### Scenario: La Tabla es una sola tabla del design system, con un único head

- **GIVEN** un revisor con pedidos en su ámbito
- **WHEN** abre `/designaciones/revision`
- **THEN** ve exactamente una tabla, con un único head de columnas — Docente, Legajo, Tipo, Inicio,
  Últ. actualización, Estado, Acciones — y cinco pestañas con sus contadores encima

#### Scenario: La Tabla abre en la etapa propia del actor

- **GIVEN** un Coordinador de carrera
- **WHEN** abre la vista
- **THEN** la pestaña "En Coordinación" está seleccionada
- **AND** un actor de Administración, que no tiene área propia, abre en "Todos"

#### Scenario: Cambiar de pestaña cambia las filas de la misma tabla

- **GIVEN** un pedido en Coordinación y otro en Secretaría
- **WHEN** el revisor pasa de la pestaña "En Coordinación" a "En Secretaría"
- **THEN** la tabla muestra el segundo pedido y deja de mostrar el primero, sin montar otra tabla

#### Scenario: Todos muestra el ámbito completo, sin borradores

- **GIVEN** un pedido en revisión, uno cancelado y un borrador dentro del ámbito del actor
- **WHEN** el revisor abre la pestaña "Todos"
- **THEN** ve el pedido en revisión y el cancelado —que con el agrupamiento por etapa no caía en
  ninguna pestaña— y NO ve el borrador

#### Scenario: La Tabla no tiene columnas Carrera ni Asignatura

- **GIVEN** un pedido cuya `carrera` es "Ingeniería Industrial", con una o varias asignaturas
- **WHEN** el revisor ve su fila
- **THEN** no existe ninguna columna Carrera ni Asignatura

#### Scenario: Un pedido Devuelto vive en la pestaña del área que lo tiene

- **GIVEN** un pedido devuelto por Decanato, cuyo `propietarioActual` es "Secretaría" y cuya
  `etapaRetorno` es "En Decanato"
- **WHEN** el revisor abre la vista
- **THEN** ese pedido aparece en la pestaña "En Secretaría" y NO en "En Decanato" — no existe una
  pestaña "Devueltos"
- **AND** la fila NO lleva ningún fondo de color

#### Scenario: Un pedido devuelto a la Cátedra sale de la pestaña que lo devolvió

- **GIVEN** un pedido que Coordinación devolvió a la Cátedra (`propietarioActual` "Jefe de Cátedra",
  `etapaRetorno` "En Coordinación")
- **WHEN** un Coordinador abre la vista
- **THEN** NO lo ve en su pestaña "En Coordinación" — ya no lo tiene, lo está esperando
- **AND** lo encuentra en la pestaña "En Cátedra", con el badge "Devuelto · en Cátedra"

#### Scenario: En Decanato nunca contiene devueltos

- **GIVEN** pedidos devueltos desde cada una de las tres etapas de revisión
- **WHEN** el revisor abre la pestaña "En Decanato"
- **THEN** no ve ninguno: no existe una etapa por encima de Decanato que pueda devolverle un pedido

#### Scenario: Estado y Área son columnas separadas

- **GIVEN** un pedido devuelto cuyo `propietarioActual` es "Jefe de Cátedra" y cuya `etapaRetorno` es
  "En Decanato"
- **WHEN** el revisor ve su fila
- **THEN** en la pestaña "Todos" la columna Estado dice "Devuelto" y la columna Área dice "Cátedra" —
  dónde está, no a qué etapa va a volver
- **AND** en una pestaña de área no hay columna Área, y el badge de Estado sigue diciendo solo
  "Devuelto"
- **AND** el badge es el mismo para todo actor: no depende de quién esté mirando

#### Scenario: Un pedido prioritario y devuelto a la vez muestra los dos badges

- **GIVEN** un pedido marcado como prioritario y en estado Devuelto a la vez
- **WHEN** el revisor ve su fila
- **THEN** la celda Estado muestra el badge de devolución Y el badge "Prioritario", sin que uno tape al
  otro y sin fondo de fila

#### Scenario: Inicio cuenta desde el envío a revisión, no desde la creación del borrador

- **GIVEN** un pedido creado el 05/01/2026 y enviado a revisión el 10/03/2026
- **WHEN** el revisor ve su fila
- **THEN** la columna Inicio muestra "10/03/2026", nunca la fecha del borrador

#### Scenario: La pantalla abre acotada al período abierto

- **GIVEN** varios períodos creados, uno de ellos `activo`
- **WHEN** el revisor abre la vista
- **THEN** el filtro Período está visible (no detrás de "+ Añadir filtro") y ya viene con el período
  abierto seleccionado, rotulado como "(abierto)"
- **AND** puede pasar a "Todos los períodos", que figura al final de la lista

#### Scenario: El filtro Carrera no se le ofrece a quien ve una sola carrera

- **GIVEN** un Coordinador de carrera y un actor de Secretaría
- **WHEN** cada uno abre el selector "+ Añadir filtro"
- **THEN** el Coordinador NO ve la opción "Carrera" y el de Secretaría sí

#### Scenario: Ordenar por una columna y volver al orden por defecto

- **GIVEN** la Tabla con varias filas
- **WHEN** el revisor hace clic tres veces en el header "Docente"
- **THEN** ve las filas ordenadas por nombre ascendente, después descendente, y después de vuelta en
  el orden por defecto de la pestaña

#### Scenario: Los filtros acotan las filas y también los contadores de las pestañas

- **GIVEN** dos pedidos en la etapa del actor y un filtro de Nombre que solo matchea uno
- **WHEN** el filtro está aplicado
- **THEN** la tabla muestra una fila y el contador de esa pestaña dice 1

#### Scenario: Una pestaña sin pedidos que cumplan los filtros muestra su estado vacío

- **GIVEN** un revisor sin pedidos finalizados
- **WHEN** abre la pestaña "Finalizados"
- **THEN** ve el estado vacío, sin tabla y sin romper la navegación

#### Scenario: El filtro Tipo ya no ofrece "Sin novedad"

- **GIVEN** el revisor con la vista Tabla abierta
- **WHEN** abre el `Select` del filtro fijo Tipo
- **THEN** ve únicamente "Alta", "Baja" y "Cambio de cargo o dedicación"

#### Scenario: La Tabla lista los pedidos del ámbito, ordenados por estado

- **GIVEN** un revisor con pedidos en su ámbito y unos filtros activos
- **WHEN** abre `/designaciones/revision`
- **THEN** ve sus pedidos sujetos a los filtros activos, distribuidos en la pestaña correspondiente
  y ordenados según la prioridad y el estado de la pestaña

#### Scenario: Tabla sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos que cumplan los filtros activos
- **WHEN** abre la vista Tabla
- **THEN** ve el estado vacío sin filas, sin romper la navegación

## ADDED Requirements

### Requirement: Filtro de carrera en la Tabla de revisión

El sistema SHALL ofrecer, en la superficie de revisión (`/designaciones/revision`), un filtro opcional
**Carrera** (vía "+ Añadir filtro", mismo patrón que Legajo y Prioridad) que acota los pedidos visibles
a los de la carrera seleccionada. El campo `carrera` ya existe en cada pedido [BR-designaciones-009];
este filtro lo expone como criterio de búsqueda. La tabla NO muestra una columna Carrera (ver "Vista
Tabla del tablero de revisión"): un pedido puede abarcar más de una carrera, así que el filtro acota
las filas pero el valor se lee completo en el detalle del pedido. El `Select` MUST ofrecer un catálogo cerrado de carreras (no texto libre, no "contiene"):
**Ingeniería en Informática**, **Ingeniería Industrial**, **Ingeniería Civil**, **Ingeniería Mecánica**
e **Ingeniería Electrónica**. El filtro Carrera SHALL combinarse por AND con el resto de los filtros
activos (vista, Nombre, Tipo, Legajo, Prioridad).

#### Scenario: Filtrar por Carrera acota las filas visibles

- **GIVEN** la Tabla de revisión con pedidos de varias carreras en el ámbito del actor (p. ej.
  Secretaría, que ve todo el departamento)
- **WHEN** el revisor agrega el filtro "Carrera" y elige "Ingeniería Industrial"
- **THEN** solo quedan visibles los pedidos cuyo `carrera` es exactamente "Ingeniería Industrial", en
  cualquiera de las cuatro secciones

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
