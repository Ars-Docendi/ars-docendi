# tablero-revision-tabla

## Purpose

Vista Tabla del tablero de revisión de pedidos de designación (`/designaciones/revision`): una tabla plana, única vista de la superficie, que lista los pedidos del ámbito del actor con columnas Docente, Asignatura, Novedad, Estado y Prioritario. Cubre la columna Estado que combina estado y avance en el circuito, y la columna Prioritario por ícono.

## Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer la superficie de revisión de pedidos (`/designaciones/revision`) como una
tabla **agrupada en cuatro secciones desplegables por etapa del circuito** — **En Coordinación**,
**En Secretaría**, **En Decanato** y **Finalizados** (Aceptados + Rechazados, sin sub-secciones) —
en vez de por estado de avance y en vez de una lista plana. Un pedido en estado **Devuelto** NUNCA
MUST tener sección propia: SHALL vivir en la sección de la etapa a la que volvió (su
`etapaRetorno`) — es ahí donde queda trabado hasta que se corrija y reenvíe. Este criterio (por
etapa, no por estado de avance) es intencional: permite a Secretaría Académica, Administrativo y
Decanato — que ven **todo el departamento**, a diferencia del Coordinador, que ve solo su carrera —
triangular grandes volúmenes de pedidos por dónde están trabados en la cadena.

Dentro de cada una de las 3 secciones de etapa, el orden MUST ser: pedidos **prioritarios** primero,
después los **devueltos**, después el resto — dentro de cada uno de esos grupos, por fecha de última
actualización **ascendente** (el que espera hace más tiempo, arriba). Dentro de **Finalizados**, los
**Aceptados** MUST ir antes que los **Rechazados**; dentro de cada bloque, por fecha de última
actualización **descendente** (el cierre más reciente arriba).

Cada sección MUST mostrar un header con el título del grupo y un contador con el texto **"Total: {n}"**
(no el número solo) — el contador MUST tener un color de fondo y de texto distinto tanto del fondo del
header como del color de los headers de columna, para que no pase inadvertido; MUST permitir
expandir/colapsar su contenido con un click; cada sección expandida MUST mostrar su
propio head de columnas (no un head único compartido arriba de las 4). Al entrar a la pantalla, MUST
arrancar expandida **únicamente** la sección correspondiente al rol del actor — Coordinador → "En
Coordinación", Secretaría → "En Secretaría", Decanato → "En Decanato" —; las demás MUST arrancar
colapsadas. Administración no tiene sección propia en este esquema: las 4 secciones MUST arrancar
colapsadas para ese rol. Las 4 secciones MUST mostrarse como bloques visualmente separados entre sí
(no apiladas sin espacio), y el título de cada sección MUST tener un color distinto al de los headers
de columna. Dentro de cada sección, la tabla MUST tener las columnas **Docente**, **Legajo**,
**Asignatura**, **Tipo**, **Fecha última actualización**, **Estado** y **Prioritario** — **Docente**
muestra el nombre del docente sin prefijo (ni "Prof." ni ningún otro), **Legajo** muestra el legajo
del docente ("—" si todavía no tiene, caso de una Alta), **Tipo** es el mismo dato de novedad que
antes se tituló "Novedad" (paridad textual con Mis Pedidos), **Fecha última actualización** es la
fecha del evento más reciente del historial del pedido (cualquier acción, no solo el envío), y
**Estado** MUST mostrar: para un pedido en revisión activa (etapas Coordinador/Secretaría/Decanato),
mini-stepper parcial (de 4 barras) + **"En {etapa} · {paso}/{total}"**; para un pedido Devuelto, el
**mismo stepper y el mismo formato "En {etapa} · {paso}/{total}"** — pero calculado sobre
`etapaRetorno` (la etapa a la que volvió, la misma que decide en qué sección vive la fila) en vez de
sobre el estado — con **" · Devuelto por {nombre} ({rol})"** agregado al final de esa misma línea,
donde `{nombre}` y `{rol}` son quien devolvió el pedido (el revisor que lo rechazó pidiendo
corrección, no quien debe corregirlo ahora); el stepper y la etapa NUNCA MUST desaparecer ni
reemplazarse por un simple punto de color mientras el pedido tenga `etapaRetorno` — solo si no lo
tiene (no debería pasar — invariante de dominio) SHALL caer a un punto de color con únicamente
"Devuelto por {nombre} ({rol})", sin stepper ni etapa; para un pedido Aceptado, un punto de color y
"Aceptado" (sin stepper — es un estado terminal, no hay avance que mostrar); para un pedido
Rechazado, un punto de color y "Rechazado". La vista MUST respetar los filtros activos (vista
`mis-pendientes`/`completa`, tipo de
novedad, prioridad, nombre y legajo del docente [ver "Filtro de pedidos por nombre o legajo del
docente"]) y MUST representar explícitamente los estados Loading, Empty, Error y Success. Esta es la
**única** vista de la superficie — no existe una vista alternativa.

#### Scenario: La Tabla agrupa los pedidos del ámbito en secciones por etapa del circuito

- **GIVEN** un revisor con pedidos en su ámbito en distintas etapas y unos filtros activos
- **WHEN** abre `/designaciones/revision`
- **THEN** ve cuatro secciones — En Coordinación, En Secretaría, En Decanato, Finalizados —, cada una
  con sus pedidos sujetos a los filtros activos y un contador de cuántos contiene

#### Scenario: Un pedido Devuelto vive en la sección de su etapa de retorno

- **GIVEN** un pedido en estado Devuelto cuya `etapaRetorno` es "en Decanato", devuelto por
  "M. Díaz" (Coordinador)
- **WHEN** el revisor abre la vista Tabla
- **THEN** ese pedido aparece dentro de la sección "En Decanato" (no en una sección "Devueltos"
  separada), con su celda Estado mostrando el mismo mini-stepper que un estado en revisión (paso
  3/4) y el texto "En Decanato · 3/4 · Devuelto por M. Díaz (Coordinador)" — el detalle de la
  devolución al costado del stepper y la etapa, no en su lugar

#### Scenario: Dentro de una sección de etapa, prioritarios y devueltos van primero

- **GIVEN** la sección "En Coordinación" con un pedido prioritario, un pedido devuelto (no
  prioritario) y pedidos activos en revisión (ninguno prioritario ni devuelto)
- **WHEN** el revisor ve esa sección
- **THEN** el pedido prioritario aparece primero, después el devuelto, después el resto ordenado por
  fecha de última actualización ascendente (el que espera hace más tiempo, arriba)

#### Scenario: Dentro de Finalizados, los Aceptados van antes que los Rechazados

- **GIVEN** la sección "Finalizados" con pedidos Aceptados y Rechazados
- **WHEN** el revisor ve esa sección
- **THEN** todos los Aceptados aparecen antes que todos los Rechazados, y dentro de cada bloque el
  cierre más reciente aparece arriba

#### Scenario: Arranca expandida solo la sección del rol del actor

- **GIVEN** un actor Secretaría con pedidos en varias de las 4 secciones
- **WHEN** abre `/designaciones/revision` por primera vez en la sesión
- **THEN** solo la sección "En Secretaría" se muestra expandida, con sus filas visibles
- **AND** las otras 3 secciones se muestran colapsadas

#### Scenario: Administración arranca con las 4 secciones colapsadas

- **GIVEN** un actor Administración con pedidos en varias de las 4 secciones
- **WHEN** abre `/designaciones/revision` por primera vez en la sesión
- **THEN** las 4 secciones se muestran colapsadas (Administración no tiene sección propia)

#### Scenario: El contador de una sección muestra "Total:" y se distingue visualmente

- **GIVEN** una sección con 2 pedidos
- **WHEN** el revisor mira el header de esa sección (esté expandida o colapsada)
- **THEN** ve el texto "Total: 2" (no solo "2"), en un color de fondo y de texto distinto del fondo
  del header y de los headers de columna

#### Scenario: Cada sección expandida tiene su propio head de columnas

- **GIVEN** dos secciones expandidas a la vez (la del rol del actor, más otra expandida manualmente)
- **WHEN** el revisor mira la pantalla
- **THEN** ve dos heads de columnas, uno arriba de las filas de cada sección — no uno solo compartido

#### Scenario: Colapsar y volver a expandir una sección

- **GIVEN** la vista Tabla con la sección "En Decanato" expandida
- **WHEN** el revisor hace click en el header de esa sección
- **THEN** el contenido de "En Decanato" se colapsa (las demás secciones no cambian)
- **AND WHEN** vuelve a hacer click en el mismo header
- **THEN** el contenido se expande de nuevo

#### Scenario: La tabla muestra Legajo, Tipo y Fecha última actualización por fila

- **GIVEN** un pedido con docente con legajo asignado y con eventos en su historial
- **WHEN** el revisor ve la fila de ese pedido en la Tabla de revisión
- **THEN** ve el legajo del docente en la columna Legajo, el chip de novedad bajo el header Tipo, y
  la fecha del evento más reciente del historial (formato dd/mm/aaaa) en la columna Fecha última
  actualización

#### Scenario: Un pedido Aceptado muestra un punto de color, sin el stepper de avance

- **GIVEN** un pedido en estado Aceptado (`en_lote`), dentro de la sección Finalizados
- **WHEN** el revisor ve la fila de ese pedido
- **THEN** la columna Estado muestra un punto de color y el texto "Aceptado" — mismo lenguaje visual
  que Devuelto/Rechazado, sin el stepper de 4 barras que sí usan los estados en revisión

#### Scenario: El nombre del docente se muestra sin prefijo

- **GIVEN** un pedido cuyo docente se llama "Ana Pérez"
- **WHEN** el revisor ve la fila de ese pedido en la Tabla de revisión
- **THEN** la columna Docente muestra "Ana Pérez", sin el prefijo "Prof." que mostraba antes

#### Scenario: Un pedido de Alta sin legajo muestra "—" en la columna Legajo

- **GIVEN** un pedido de Alta cuyo docente todavía no tiene legajo asignado
- **WHEN** el revisor ve la fila de ese pedido en la Tabla de revisión
- **THEN** la columna Legajo muestra "—" en vez de un valor vacío

#### Scenario: Sección sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos que cumplan los filtros activos en una sección puntual (p. ej.
  En Decanato)
- **WHEN** abre la vista Tabla
- **THEN** esa sección se muestra con contador en 0 y un texto de estado vacío, sin romper la
  navegación ni el resto de las secciones

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
