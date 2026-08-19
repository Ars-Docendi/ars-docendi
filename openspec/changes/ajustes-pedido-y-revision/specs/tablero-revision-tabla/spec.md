## MODIFIED Requirements

### Requirement: Vista Tabla del tablero de revisión

El sistema SHALL ofrecer la superficie de revisión de pedidos (`/designaciones/revision`) como una
tabla agrupada en cuatro secciones desplegables por etapa del circuito — **En Coordinación**, **En
Secretaría**, **En Decanato** y **Finalizados** (Aceptados + Rechazados) — de los pedidos del ámbito
del actor [BR-designaciones-009]. Un pedido **Devuelto** vive en la sección de la etapa a la que
volvió (`etapaRetorno`), no en una sección propia. Cada sección expandida MUST mostrar las columnas
**Docente**, **Legajo**, **Carrera**, **Asignatura**, **Tipo**, **Fecha última actualización**,
**Estado** y **Prioritario**. La columna **Carrera** MUST mostrar el nombre **abreviado** de la carrera
del pedido (mismo campo `carrera` que usa el filtro Carrera), según un mapeo cerrado: "Ingeniería en
Informática" → "Informática", "Ingeniería Industrial" → "Industrial", "Ingeniería Civil" → "Civil",
"Ingeniería Mecánica" → "Mecánica", "Ingeniería Electrónica" → "Electrónica" — nunca el nombre completo.

La columna **Estado** MUST mostrar, para un pedido en revisión activa, un mini-stepper parcial +
"En {etapa} · {paso}/{total}"; para un pedido **Devuelto**, el mismo stepper y formato calculado sobre
`etapaRetorno`, con "· Devuelto por {nombre} ({rol})" agregado al final; para un pedido Aceptado o
Rechazado, un punto de color + la etiqueta correspondiente, sin stepper.

Además de la celda Estado, la **fila entera** MUST llevar un fondo de color quando el pedido es
prioritario o está devuelto — para que se distingan de un vistazo del resto de las filas de su
sección, sin necesidad de leer el texto: fondo **rojo** (`--danger-100`) si `pedido.prioritario` es
verdadero; si no, fondo **amarillo** (`--warning-100`) si `pedido.estado` es `devuelto`; sin fondo
especial en cualquier otro caso. Cuando un pedido es prioritario y está devuelto a la vez, MUST
prevalecer el fondo **rojo** (prioritario) — la celda Estado sigue mostrando el detalle de la
devolución (stepper + "Devuelto por…") sin importar el color de fondo de la fila.

La columna **Prioritario** MUST mostrar un ícono de bandera cuando `pedido.prioritario` es verdadero
y/o un ícono de flechita cuando `pedido.estado` es `devuelto` — ambos son independientes entre sí (a
diferencia del fondo de fila, acá **no hay** prevalencia: un pedido prioritario y devuelto a la vez
MUST mostrar los dos íconos juntos). La celda MUST centrar su contenido: con un solo ícono (cualquiera
de los dos), MUST quedar centrado en la columna; con los dos a la vez, MUST quedar un espacio entre
ambos, con la bandera a la izquierda y la flechita a la derecha.

La vista MUST respetar los filtros activos: vista (`mis-pendientes`/`completa`), **Nombre** y **Tipo**
del docente como campos fijos siempre visibles, y **Legajo**, **Prioridad** y **Carrera** (ver "Filtro
de carrera en la Tabla de revisión") como filtros opcionales vía "+ Añadir filtro". El filtro **Tipo**
SHALL ofrecer únicamente las tres novedades vigentes ("Alta", "Baja", "Cambio de cargo o dedicación")
— la novedad "Sin novedad" ya no existe en el sistema. La vista MUST representar explícitamente los
estados Loading, Empty, Error y Success. Esta es la **única** vista de la superficie — no existe una
vista alternativa.

#### Scenario: La Tabla agrupa los pedidos del ámbito en secciones por etapa del circuito

- **GIVEN** un revisor con pedidos en su ámbito en distintas etapas y unos filtros activos
- **WHEN** abre `/designaciones/revision`
- **THEN** ve cuatro secciones — En Coordinación, En Secretaría, En Decanato, Finalizados —, cada una
  con sus pedidos sujetos a los filtros activos y un contador de cuántos contiene

#### Scenario: Un pedido Devuelto vive en la sección de su etapa de retorno, con la fila en amarillo

- **GIVEN** un pedido en estado Devuelto (no prioritario) cuya `etapaRetorno` es "En Decanato", devuelto
  por "M. Díaz" (Coordinador)
- **WHEN** el revisor abre la vista Tabla
- **THEN** ese pedido aparece dentro de la sección "En Decanato" (no en una sección "Devueltos"
  separada), con su fila entera en fondo amarillo (`--warning-100`) y su celda Estado mostrando el
  stepper (paso 3/4) y el texto "En Decanato · 3/4 · Devuelto por M. Díaz (Coordinador)"

#### Scenario: Un pedido prioritario muestra la fila entera en rojo

- **GIVEN** un pedido marcado como prioritario, no devuelto
- **WHEN** el revisor ve su fila en cualquiera de las 4 secciones
- **THEN** la fila entera tiene fondo rojo (`--danger-100`)

#### Scenario: Un pedido prioritario y devuelto a la vez muestra rojo, no amarillo

- **GIVEN** un pedido marcado como prioritario y en estado Devuelto a la vez
- **WHEN** el revisor ve su fila
- **THEN** la fila tiene fondo rojo (gana prioritario), y la celda Estado sigue mostrando el detalle
  completo de la devolución (stepper + "Devuelto por…")

#### Scenario: Tabla sin pedidos en el ámbito

- **GIVEN** un revisor sin pedidos que cumplan los filtros activos
- **WHEN** abre la vista Tabla
- **THEN** ve el estado vacío sin filas, sin romper la navegación

#### Scenario: La columna Carrera muestra el nombre abreviado

- **GIVEN** un pedido cuyo `carrera` es "Ingeniería Industrial"
- **WHEN** el revisor ve la fila de ese pedido en cualquiera de las 4 secciones
- **THEN** la columna Carrera muestra "Industrial" (abreviado), no "Ingeniería Industrial"

#### Scenario: El filtro Tipo ya no ofrece "Sin novedad"

- **GIVEN** el revisor con la vista Tabla abierta
- **WHEN** abre el `Select` del filtro fijo Tipo
- **THEN** ve únicamente "Alta", "Baja" y "Cambio de cargo o dedicación" — sin la opción "Sin novedad"

#### Scenario: Un pedido Aceptado o Rechazado no lleva fondo de fila especial

- **GIVEN** un pedido en estado Aceptado (`en_lote`) y otro Rechazado, ninguno prioritario, dentro de la
  sección Finalizados
- **THEN** la columna Estado muestra un punto de color + la etiqueta correspondiente, sin stepper, y la
  fila NO MUST llevar el fondo rojo ni el amarillo

#### Scenario: Un pedido devuelto (no prioritario) muestra la flechita centrada, no la bandera

- **GIVEN** un pedido en estado Devuelto, no marcado como prioritario
- **WHEN** el revisor ve su fila
- **THEN** la columna Prioritario muestra únicamente el ícono de flechita, centrado en la columna

#### Scenario: Un pedido prioritario (no devuelto) muestra la bandera centrada, no la flechita

- **GIVEN** un pedido marcado como prioritario, no devuelto
- **WHEN** el revisor ve su fila
- **THEN** la columna Prioritario muestra únicamente el ícono de bandera, centrado en la columna — en
  la misma posición horizontal que ocuparía la flechita si el pedido fuera solo devuelto (un solo
  ícono siempre queda centrado, sea cual sea)

#### Scenario: Un pedido prioritario y devuelto a la vez muestra los dos íconos, uno a cada lado

- **GIVEN** un pedido marcado como prioritario y en estado Devuelto a la vez
- **WHEN** el revisor ve su fila
- **THEN** la columna Prioritario muestra la bandera a la izquierda y la flechita a la derecha, con un
  espacio entre ambas — sin importar que la fila esté en fondo rojo (D-7/prioritario gana el fondo,
  pero no el ícono)

## ADDED Requirements

### Requirement: Filtro de carrera en la Tabla de revisión

El sistema SHALL ofrecer, en la superficie de revisión (`/designaciones/revision`), un filtro opcional
**Carrera** (vía "+ Añadir filtro", mismo patrón que Legajo y Prioridad) que acota los pedidos visibles
a los de la carrera seleccionada. El campo `carrera` ya existe en cada pedido [BR-designaciones-009];
este filtro lo expone como criterio de búsqueda (la columna **Carrera** que muestra su valor abreviado
en la tabla se define en "Vista Tabla del tablero de revisión" — mismo rótulo en el filtro y en la
columna). El `Select` MUST ofrecer un catálogo cerrado de carreras (no texto libre, no "contiene"):
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
