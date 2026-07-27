## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Filtro de pedidos por nombre o legajo del docente

El sistema SHALL ofrecer, en la superficie de revisión (`/designaciones/revision`), el mismo
componente de filtro genérico y reutilizable que usa "Mis pedidos" (`shared/ui/FiltrosLista.tsx`):
**Nombre** del docente y **Tipo** de novedad como campos siempre visibles al inicio de la barra
("Nombre" primero, "Tipo" al costado) — Tipo deja de ser un filtro opcional (rompe a propósito la
paridad de patrón con Mis Pedidos que se buscó al principio de este mismo requirement: pedido
explícito y posterior del cliente, específico de esta pantalla) —, más **Legajo** y **Prioridad**
como filtros opcionales vía "+ Añadir filtro" (con botón "×" para quitarlos). La comparación de
Nombre y Legajo MUST ser "contiene", sin distinguir mayúsculas ni acentos (mismo criterio que
Usuarios y Mis Pedidos). El filtro de **Vista** ("Mis pendientes"/"Vista completa") MUST permanecer
separado, fuera de este bloque — no es un filtro por dato del pedido, es un selector de alcance.
Todos los filtros activos se combinan entre sí mediante AND. Un pedido de Alta cuyo docente todavía
no tiene legajo asignado NUNCA MUST aparecer al filtrar por legajo (no hay dato contra el cual
matchear), pero SHALL seguir apareciendo cuando el filtro de legajo está vacío o no agregado.

#### Scenario: Filtrar por Nombre acota las filas visibles

- **GIVEN** la Tabla de revisión con pedidos de varios docentes en el ámbito
- **WHEN** el revisor tipea parte del nombre de un docente en el filtro Nombre (siempre visible)
- **THEN** solo quedan visibles los pedidos cuyo docente contiene ese texto en el nombre (sin
  distinguir mayúsculas ni acentos)

#### Scenario: Filtrar por Tipo (siempre visible, junto a Nombre) acota las filas visibles

- **GIVEN** la Tabla de revisión con pedidos de varios tipos de novedad en el ámbito
- **WHEN** el revisor elige un tipo puntual (p. ej. "Alta") en el select Tipo, visible desde el
  principio junto a Nombre — sin pasar por "+ Añadir filtro"
- **THEN** solo quedan visibles los pedidos de ese tipo de novedad

#### Scenario: Agregar el filtro opcional Legajo acota las filas visibles

- **GIVEN** el filtro colapsado (sin Legajo agregado) y pedidos de docentes con legajo asignado
- **WHEN** el revisor elige "Legajo" en el selector "+ Añadir filtro" y tipea parte de un legajo
- **THEN** solo quedan visibles los pedidos cuyo docente tiene ese legajo (contiene)
- **AND** puede quitarlo con el botón "×", volviendo a ver todos los pedidos sujetos al resto de los
  filtros activos

#### Scenario: Un pedido de Alta sin legajo no aparece al filtrar por legajo

- **GIVEN** un pedido de Alta cuyo docente todavía no tiene legajo asignado, y el filtro Legajo
  agregado con texto
- **WHEN** se aplica el filtro
- **THEN** ese pedido no aparece en ninguna sección
- **AND WHEN** el filtro de Legajo se quita o queda vacío
- **THEN** ese pedido vuelve a aparecer normalmente (sujeto a los demás filtros)

#### Scenario: Prioridad es opcional, igual que Legajo (Tipo no — es fijo)

- **GIVEN** el filtro colapsado (sin Prioridad agregada)
- **WHEN** el revisor elige "Prioridad" en el selector "+ Añadir filtro"
- **THEN** aparece el campo correspondiente, y aplicarlo acota la lista a los pedidos que coinciden
- **AND** puede quitarlo con el botón "×"
- **AND** el selector "+ Añadir filtro" nunca ofrece "Tipo" — ya está siempre visible, junto a Nombre

#### Scenario: Los filtros activos se combinan entre sí

- **GIVEN** un filtro de Tipo activo (p. ej. "Alta") y texto en el filtro Nombre
- **WHEN** ambos están aplicados a la vez
- **THEN** solo quedan visibles los pedidos que cumplen AMBAS condiciones
