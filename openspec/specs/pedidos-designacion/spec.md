# pedidos-designacion

## Purpose

Flujo del Jefe de Cátedra para cargar, editar y enviar a revisión los pedidos de designación docente de su cátedra dentro del período abierto. Cubre la lista "Mis pedidos", el alta/edición de pedidos con secciones condicionales por novedad, la validación de adjuntos y justificaciones obligatorias, la máquina de estados pura del lado del Jefe de Cátedra y la persistencia mock del flujo entre roles y recargas.

## Requirements

### Requirement: Lista "Mis pedidos" del Jefe de Cátedra

El sistema SHALL ofrecer al Jefe de Cátedra una pantalla "Mis pedidos" (`/designaciones/mis-pedidos`)
que liste los pedidos de designación de su cátedra para el período abierto, mostrando por cada pedido
el docente (sin prefijo de tratamiento), su **legajo** (o un placeholder si todavía no tiene, caso de
una Alta reciente), la materia asociada, el **tipo** (novedad), el estado (vía `StatusBadge`) y el flag
de prioritario. La pantalla MUST ofrecer un filtro con dos campos de texto siempre visibles —
**Docente** (angosto, mismo ancho que el campo equivalente de la pantalla Usuarios) y **N°** — más un
mecanismo para agregar como filtros opcionales **Legajo**, **Tipo** y **Estado** (mismo patrón que el
filtro de la pantalla de Usuarios, implementado mediante un componente de filtro genérico y reutilizable
por otras pantallas). Cada fila MUST navegar al detalle del pedido al hacer click
en cualquier parte de la fila. Al final de la fila, un botón **"Ver"** MUST estar siempre visible
(navega al mismo detalle) y un botón **"Editar"** MUST mostrarse únicamente cuando el pedido es
editable por el actor — ambos con el mismo formato que las acciones de fila de la pantalla Usuarios
(`Button variant="ghost" size="sm"`, sin ícono). Cuando el pedido está en `borrador`, la fila MUST
mostrar además un control "Eliminar" (X roja, ver "Eliminar un pedido en borrador"). La pantalla MUST
representar explícitamente los cuatro estados de carga: Loading, Empty, Error y Success.

#### Scenario: Lista con pedidos existentes

- **GIVEN** un Jefe de Cátedra autenticado con pedidos cargados en el período abierto
- **WHEN** abre `/designaciones/mis-pedidos`
- **THEN** ve la lista de sus pedidos con docente (sin prefijo), legajo, materia, tipo, `StatusBadge`
  por estado y el flag de prioritario cuando corresponde

#### Scenario: Estado vacío sin pedidos

- **GIVEN** un Jefe de Cátedra sin pedidos cargados en el período abierto
- **WHEN** abre `/designaciones/mis-pedidos`
- **THEN** ve un estado vacío con la acción "Nuevo pedido", sin filas de pedidos

#### Scenario: Estado de carga y de error

- **WHEN** la consulta de pedidos está en curso
- **THEN** la pantalla muestra el estado Loading
- **AND WHEN** la consulta falla
- **THEN** la pantalla muestra el estado Error sin romper la navegación

#### Scenario: Precarga de docentes del período anterior

- **GIVEN** un período abierto recién disponible para el Jefe de Cátedra
- **WHEN** abre "Mis pedidos" por primera vez
- **THEN** ve precargados los docentes del período anterior como pedidos con novedad "Sin novedad"

#### Scenario: Click en la fila abre el detalle del pedido

- **GIVEN** cualquier pedido listado, en cualquier estado
- **WHEN** el Jefe de Cátedra hace click en la fila (fuera del botón "Editar")
- **THEN** el sistema navega a `/designaciones/pedidos/:id` de ese pedido

#### Scenario: El botón Editar solo aparece cuando el pedido es editable

- **GIVEN** un pedido en `borrador`, o `devuelto` con el Jefe de Cátedra como propietario actual
- **WHEN** se lista en "Mis pedidos"
- **THEN** su fila MUST mostrar el botón "Editar"
- **AND** un pedido en cualquier otro estado (en revisión, rechazado, cancelado, en lote) NO MUST
  mostrar ese botón

#### Scenario: El botón Ver está siempre disponible y navega al detalle

- **GIVEN** cualquier pedido listado, en cualquier estado
- **WHEN** el Jefe de Cátedra hace click en el botón "Ver" de su fila
- **THEN** el sistema navega a `/designaciones/pedidos/:id` de ese pedido, sin disparar dos veces la
  navegación (el click en "Ver" no burbujea al `onClick` de la fila)

#### Scenario: La X roja de eliminar solo aparece en pedidos en borrador

- **GIVEN** una fila con un pedido en `borrador` y otra con un pedido `devuelto`
- **WHEN** se lista en "Mis pedidos"
- **THEN** la fila del `borrador` MUST mostrar el control "Eliminar" (X roja)
- **AND** la fila del `devuelto` (y de cualquier otro estado) NO MUST mostrarlo

#### Scenario: El legajo se muestra en la tabla, o un placeholder si el docente no tiene uno todavía

- **GIVEN** un pedido de un docente con legajo asignado, y otro de una Alta reciente sin legajo
- **WHEN** se lista en "Mis pedidos"
- **THEN** la fila del primero MUST mostrar su legajo
- **AND** la fila del segundo MUST mostrar un placeholder ("—"), sin inventar un valor

#### Scenario: Filtrar por Docente o N° acota la lista

- **GIVEN** varios pedidos listados
- **WHEN** el Jefe de Cátedra tipea en el filtro Docente o en el filtro N°
- **THEN** solo quedan visibles los pedidos que coinciden (contiene, sin distinguir mayúsculas ni
  acentos)

#### Scenario: Agregar el filtro opcional Legajo, Tipo o Estado

- **GIVEN** el filtro colapsado (sin Legajo, Tipo ni Estado agregados)
- **WHEN** el Jefe de Cátedra elige "Legajo" (o "Tipo" o "Estado") en el selector "+ Añadir filtro"
- **THEN** aparece el campo correspondiente, y aplicarlo acota la lista a los pedidos que coinciden
  (Legajo: contiene, sin distinguir mayúsculas ni acentos, igual que Docente/N°)
- **AND** puede quitarlo con el botón "×", volviendo a ver todos los pedidos sujetos al resto de los
  filtros activos

### Requirement: Creación de pedido de designación

El sistema SHALL permitir al Jefe de Cátedra crear un pedido de designación desde
`/designaciones/pedidos/nuevo`, capturando los datos comunes del pedido: docente (DNI, nombre),
antigüedad, cargo y dedicación actual (read-only, mock), una o más asignaciones de materia con sus
horas, horas de investigación, horas externas (otro departamento) y novedad. El pedido se crea en
estado `borrador`.

#### Scenario: Alta de un pedido en borrador

- **GIVEN** un Jefe de Cátedra en el form de nuevo pedido
- **WHEN** completa los datos comunes, al menos una asignación de materia válida y una novedad válida
  con sus campos requeridos
- **THEN** el pedido se persiste en estado `borrador` y aparece en "Mis pedidos"

#### Scenario: Un pedido por docente por período [BR-designaciones-001]

- **GIVEN** un docente que ya tiene un pedido en el período abierto
- **WHEN** el Jefe de Cátedra intenta crear un segundo pedido para ese mismo docente en el mismo
  período
- **THEN** el sistema MUST rechazar la creación e indicar que ya existe un pedido para ese docente en
  el período, sin importar si las materias asociadas al segundo pedido difieren de las del primero

### Requirement: Secciones condicionales por novedad

El form de pedido SHALL mostrar u ocultar secciones según la novedad seleccionada (Radio: "Sin
novedad" / "Alta" / "Baja" / "Cambio de cargo o dedicación"). "Alta" y "Cambio de cargo o dedicación"
exponen cargo y dedicación solicitados, más horas de investigación y horas externas; "Baja" expone el
tipo de baja; "Sin novedad" no expone campos adicionales más allá de la materia y horas vigentes del
docente (solo lectura).

#### Scenario: La sección de solicitud aparece solo para Alta y Cambio

- **WHEN** el usuario selecciona la novedad "Alta" o "Cambio de cargo o dedicación"
- **THEN** el form muestra los campos de cargo y dedicación solicitados, y los campos de horas de
  investigación y horas externas
- **AND WHEN** selecciona "Sin novedad"
- **THEN** el form oculta los campos de cargo y dedicación solicitados y los de horas de investigación
  y externas

#### Scenario: La sección de adjuntos se adapta a la novedad

- **WHEN** el usuario cambia la novedad seleccionada
- **THEN** la sección de adjuntos requeridos se actualiza para reflejar los adjuntos exigidos por esa
  novedad

### Requirement: Materias y horas del pedido

El sistema SHALL permitir que un pedido de novedad "Alta" o "Cambio de cargo o dedicación" incluya
una o más asignaciones de materia, cada una con su propia carga horaria (materia + horas), agregables,
quitables y con la materia seleccionable/cambiable desde el form — el mismo patrón de lista en ambas
novedades. El listado SHALL tener siempre al menos 1 asignación: el sistema MUST impedir quitar la
última fila restante. En Cambio, la lista SHALL precargarse con las materias que ya tiene el docente
seleccionado, pero queda abierta a los mismos cambios que en Alta (agregar, quitar, cambiar materia,
editar horas).

Para la novedad "Baja", el sistema SHALL mostrar el mismo listado de materias y horas que ya tiene el
docente (`materiasActuales`), pero íntegramente de solo lectura: ni la materia ni las horas ni el
listado en sí (agregar/quitar) son editables — es información de contexto sobre qué queda vacante, no
un dato a modificar. Para "Sin novedad", el pedido SHALL tener exactamente una asignación
correspondiente a la materia vigente del docente, no editable ni en la materia ni en las horas.

El listado de materias reemplaza cualquier mención de la materia en el panel de datos actuales de
solo lectura (evita duplicar la misma información en dos lugares del form), tanto en Cambio como en
Baja.

#### Scenario: Alta con múltiples materias

- **GIVEN** un Jefe de Cátedra cargando un pedido de "Alta"
- **WHEN** agrega una segunda fila de materia + horas mediante la acción "Agregar materia"
- **THEN** el pedido guarda ambas asignaciones (materia + horas) sin necesidad de crear un segundo
  pedido para el mismo docente

#### Scenario: Cambio precarga el listado de materias del docente, editable

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" sobre un docente con una o más
  materias asignadas
- **WHEN** el Jefe de Cátedra visualiza el form
- **THEN** ve una fila por cada materia a la que pertenece el docente, con la materia seleccionable en
  un `Select` y la carga horaria en un campo editable

#### Scenario: Cambio permite agregar, quitar y cambiar materias

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" con su listado de materias precargado
- **WHEN** el Jefe de Cátedra agrega una fila nueva, quita una fila existente, o cambia la materia
  seleccionada en una fila
- **THEN** el pedido guarda el listado resultante de asignaciones (materia + horas)

#### Scenario: No se puede dejar un pedido sin materias [Alta y Cambio]

- **GIVEN** un pedido de novedad "Alta" o "Cambio de cargo o dedicación" con una única fila de materia
  restante en el listado
- **WHEN** el Jefe de Cátedra intenta quitar esa última fila
- **THEN** el sistema MUST impedir la acción (la UI no ofrece quitar la última fila, y la validación
  de guardado MUST rechazar un `asignaciones` vacío si ocurriera por otra vía)

#### Scenario: Cambio y Baja no repiten la materia como columna plana en la franja superior

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" o "Baja"
- **WHEN** el Jefe de Cátedra visualiza la franja superior del panel de datos actuales (Antigüedad /
  Cargo / Dedicación)
- **THEN** esa franja MUST mostrar antigüedad, cargo actual y dedicación actual sin una columna plana
  de "Materia" — la materia vive en la sección de materias y horas (Cambio: editable, arriba del
  panel; Baja: listado de solo lectura) y, en Cambio, también en la sub-sección de transición del
  propio panel (ver "Resumen de cambios en el panel de datos actuales")

#### Scenario: Baja muestra el listado de materias y horas del docente, de solo lectura

- **GIVEN** un pedido de novedad "Baja" sobre un docente con una o más materias asignadas
- **WHEN** el Jefe de Cátedra visualiza el form
- **THEN** ve una fila por cada materia a la que pertenece el docente, con su carga horaria, sin
  ningún control editable (ni `Select` de materia, ni `Input` de horas, ni acción de agregar/quitar)

#### Scenario: Sin novedad muestra la materia vigente sin edición

- **GIVEN** un pedido de novedad "Sin novedad"
- **WHEN** el Jefe de Cátedra visualiza el form
- **THEN** ve la materia vigente del docente como asignación única, sin poder editar la materia ni
  las horas ni agregar otras

### Requirement: Cargo solicitado sin restricción de jerarquía

En "Alta" y "Cambio de cargo o dedicación", el sistema SHALL permitir seleccionar el cargo solicitado
libremente entre todos los valores del catálogo (`CARGOS` completo), sin restringir las opciones
disponibles según el cargo actual del docente. Este change NO MUST implementar ninguna regla de "solo
se puede subir" para el cargo — esa restricción pertenece al tema C (jerarquía de cargos), fuera de
alcance.

#### Scenario: Cargo solicitado admite cualquier valor del catálogo

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" sobre un docente con cargo actual
  "Adjunto"
- **WHEN** el Jefe de Cátedra abre el `Select` de "Cargo solicitado"
- **THEN** ve disponibles los 4 cargos del catálogo (incluidos los inferiores a "Adjunto"), sin ningún
  cargo deshabilitado ni mensaje que restrinja la selección

### Requirement: Dedicación solicitada en Cambio solo puede mejorar

En "Cambio de cargo o dedicación", el sistema SHALL restringir la dedicación solicitada a valores
jerárquicamente mejores que la dedicación actual del docente. La escala de dedicaciones es
descendente: `Categoría 0` es la de mayor jerarquía y `Categoría 6` la de menor; "mejor" significa un
índice estrictamente menor al de la dedicación actual (no se admite igual). El `Select` de "Dedicación
solicitada" MUST ofrecer únicamente esas opciones, y la validación de guardado MUST rechazar igual
cualquier dedicación solicitada que no sea estrictamente mejor (defensa en profundidad). En "Alta" no
aplica esta restricción (no hay dedicación actual con la cual comparar).

#### Scenario: El Select de dedicación solo ofrece opciones mejores que la actual

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" sobre un docente con dedicación actual
  "Categoría 3"
- **WHEN** el Jefe de Cátedra abre el `Select` de "Dedicación solicitada"
- **THEN** ve disponibles únicamente "Categoría 0", "Categoría 1" y "Categoría 2" — ninguna opción
  igual o peor que "Categoría 3"

#### Scenario: La validación rechaza una dedicación que no mejora

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" con dedicación actual "Categoría 3" y
  dedicación solicitada "Categoría 3" o peor (llegada por un medio distinto al `Select`, p. ej. editar
  un borrador)
- **WHEN** el Jefe de Cátedra intenta guardar el pedido
- **THEN** el sistema MUST bloquear el guardado e indicar que la dedicación solicitada debe ser mejor
  que la actual

### Requirement: Resumen de cambios en el panel de datos actuales (Cambio)

En "Cambio de cargo o dedicación", el panel de solo lectura de datos actuales SHALL mostrar, además de
antigüedad, la transición `actual → solicitado` de **todos** los campos que Cambio puede modificar:
cargo, dedicación, cada materia con su carga horaria, y horas de investigación/externas. Un campo sin
cambios SHALL mostrarse con su valor plano (sin flecha de transición). En "Baja" y "Sin novedad" el
panel NO MUST mostrar transiciones (no hay valores "solicitados" que comparar).

#### Scenario: El panel muestra la transición de cargo y dedicación

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" con cargo actual "Adjunto" → cargo
  solicitado "Titular", y dedicación actual "Categoría 3" → dedicación solicitada "Categoría 1"
- **WHEN** el Jefe de Cátedra visualiza el panel de datos actuales
- **THEN** ve "Adjunto → Titular" en la fila de cargo y "Categoría 3 → Categoría 1" en la fila de
  dedicación

#### Scenario: El panel compara el listado de materias por nombre

- **GIVEN** un pedido de Cambio donde el docente tenía "Programación I (6h)" y "Programación II (4h)",
  y el Jefe de Cátedra cambió las horas de "Programación I" a 8h, quitó "Programación II" y agregó
  "Bases de Datos (4h)"
- **WHEN** visualiza el panel de datos actuales
- **THEN** ve tres filas: "Programación I: 6h → 8h", "Programación II: 6h · quitada" (o equivalente) y
  "Bases de Datos: nueva · 4h" (o equivalente)

#### Scenario: El panel muestra la transición de horas de investigación y externas

- **GIVEN** un pedido de Cambio donde el docente tenía 2h de investigación y 0h externas, y el Jefe de
  Cátedra cargó 4h de investigación y dejó las externas sin cambios
- **WHEN** visualiza el panel de datos actuales
- **THEN** ve "Investigación: 2h → 4h" y "Externas: 0h" (sin flecha, no cambió)

### Requirement: Horas de investigación y horas externas del pedido

El sistema SHALL permitir cargar, en las novedades "Alta" y "Cambio de cargo o dedicación", horas de
investigación y horas externas (otro departamento) como campos numéricos libres del pedido, sin
validar que su suma junto con las horas de materia cierre contra la dedicación solicitada o actual.

#### Scenario: Carga de horas de investigación y externas

- **GIVEN** un Jefe de Cátedra cargando un pedido de "Alta" o "Cambio de cargo o dedicación"
- **WHEN** completa los campos "Horas de investigación" y "Horas externas (otro depto.)" con valores
  numéricos
- **THEN** el pedido guarda ambos valores asociados al docente, no a una materia en particular

#### Scenario: Sin validación de cierre contra la dedicación

- **GIVEN** un pedido con horas de materia, investigación y externas cargadas
- **WHEN** la suma de esas horas no coincide con lo esperable para la dedicación solicitada
- **THEN** el sistema NO MUST bloquear el guardado por esa discrepancia (las horas son campos libres)

### Requirement: Tipificación de la baja

El sistema SHALL exigir, cuando la novedad es "Baja", la selección de un "Tipo de baja" entre
"Renuncia", "Jubilación" u "Otro", mostrado antes del campo "Motivo de la baja". Si el tipo
seleccionado es "Otro", el sistema SHALL exigir además una descripción en texto libre.

#### Scenario: Tipo de baja obligatorio

- **GIVEN** un pedido con novedad "Baja"
- **WHEN** el Jefe de Cátedra intenta guardar sin haber seleccionado un tipo de baja
- **THEN** la validación MUST bloquear el guardado e indicar que el tipo de baja es obligatorio

#### Scenario: "Otro" exige detalle en texto libre

- **GIVEN** un pedido con novedad "Baja" y tipo de baja "Otro"
- **WHEN** el Jefe de Cátedra intenta guardar sin completar el detalle en texto libre
- **THEN** la validación MUST bloquear el guardado e indicar que el detalle es obligatorio para "Otro"

### Requirement: Adjuntos y justificación obligatorios por novedad

El sistema SHALL validar los adjuntos y la justificación obligatorios según la novedad antes de
permitir guardar o enviar el pedido. Una novedad "Alta" MUST exigir CV + foto de DNI frente + foto de
DNI dorso [BR-designaciones-002]; "Baja" MUST exigir un adjunto justificativo [BR-designaciones-003];
"Cambio de cargo o dedicación" MUST exigir una justificación [BR-designaciones-004]. Además, "Baja" y
"Cambio de cargo o dedicación" MUST exigir que el docente referenciado tenga legajo asignado
[BR-designaciones-018] — ambas novedades operan sobre un docente ya existente en el sistema, que por
eso ya tiene un legajo; "Alta" no exige legajo (el docente todavía no existe, se lo asigna el
sistema/RRHH después). (En el prototipo los adjuntos son solo metadata mock.)

#### Scenario: Alta exige CV, DNI frente y DNI dorso [BR-designaciones-002]

- **GIVEN** un pedido con novedad "Alta"
- **WHEN** falta alguno de los adjuntos CV, DNI frente o DNI dorso
- **THEN** la validación MUST bloquear el guardado e indicar el adjunto faltante

#### Scenario: Baja exige justificativo [BR-designaciones-003]

- **GIVEN** un pedido con novedad "Baja"
- **WHEN** falta el adjunto justificativo
- **THEN** la validación MUST bloquear el guardado e indicar que el justificativo es obligatorio

#### Scenario: Cambio exige justificación [BR-designaciones-004]

- **GIVEN** un pedido con novedad "Cambio de cargo o dedicación"
- **WHEN** la justificación está vacía
- **THEN** la validación MUST bloquear el guardado e indicar que la justificación es obligatoria

#### Scenario: Baja o Cambio exigen que el docente tenga legajo [BR-designaciones-018]

- **GIVEN** un pedido con novedad "Baja" o "Cambio de cargo o dedicación"
- **WHEN** el docente referenciado no tiene legajo asignado
- **THEN** la validación MUST bloquear el guardado e indicar que el legajo es obligatorio

#### Scenario: Alta no exige legajo [BR-designaciones-018]

- **GIVEN** un pedido con novedad "Alta"
- **WHEN** el docente (nuevo) no tiene legajo asignado
- **THEN** la validación NO MUST bloquear el guardado por esa razón

#### Scenario: La validación inline bloquea el submit inválido

- **WHEN** el usuario intenta guardar o enviar un pedido con campos requeridos faltantes
- **THEN** el form muestra el error inline en el campo afectado y no envía la acción al store

### Requirement: Enviar y reenviar desde el form de pedido

El sistema SHALL ofrecer, dentro del form de pedido (`/designaciones/pedidos/nuevo` o
`/designaciones/pedidos/:id/editar`), un botón adicional a "Guardar pedido" que guarda los datos y, en
el mismo paso, envía el pedido a revisión: **"Guardar y enviar"** cuando el resultado queda en
`borrador` (creación, o edición de un borrador existente), o **"Guardar y reenviar"** cuando se está
editando un pedido `devuelto`. **"Guardar pedido" MUST guardar siempre el estado actual del form, sin
exigir que los campos obligatorios estén completos** — el Jefe de Cátedra tiene que poder guardar un
borrador a medio completar y retomarlo después. La validación completa (adjuntos, justificación, tipo
de baja, legajo [BR-designaciones-018], etc.) MUST aplicarse únicamente al enviar/reenviar: "Guardar y
enviar" y "Guardar y reenviar" MUST bloquear la acción si hay errores, sin guardar ni enviar nada — a
diferencia de "Guardar pedido", que nunca se bloquea por esta causa. El sistema NO MUST ofrecer
ninguna acción para cancelar (pasar a `cancelado`) un pedido en esta pantalla ni en el form.

#### Scenario: Crear y enviar en un solo paso

- **GIVEN** un Jefe de Cátedra completando el form de un pedido nuevo, con todos los datos válidos
- **WHEN** hace click en "Guardar y enviar"
- **THEN** el sistema crea el pedido en `borrador` y de inmediato lo envía a revisión
  (`en_revision_coordinador`)

#### Scenario: Editar un borrador y enviarlo

- **GIVEN** un Jefe de Cátedra editando un pedido en `borrador`, con datos válidos
- **WHEN** hace click en "Guardar y enviar"
- **THEN** el sistema guarda los cambios y envía el pedido a revisión en el mismo paso

#### Scenario: Editar un devuelto y reenviarlo

- **GIVEN** un Jefe de Cátedra editando un pedido `devuelto` del que es propietario, con datos válidos
- **WHEN** hace click en "Guardar y reenviar"
- **THEN** el sistema guarda los cambios y reenvía el pedido, retomando la etapa que lo devolvió

#### Scenario: Guardar pedido siempre guarda, aunque falten campos obligatorios

- **GIVEN** el form con datos incompletos (p. ej. falta un adjunto obligatorio, o el tipo de baja)
- **WHEN** el Jefe de Cátedra hace click en "Guardar pedido"
- **THEN** el sistema MUST guardar el estado actual del form tal cual está, sin bloquear la acción ni
  exigir que los campos obligatorios estén completos

#### Scenario: Guardar y enviar bloquea si faltan campos obligatorios

- **GIVEN** el form con datos inválidos (p. ej. falta un adjunto obligatorio)
- **WHEN** el Jefe de Cátedra hace click en "Guardar y enviar" (o "Guardar y reenviar")
- **THEN** el sistema MUST bloquear la acción y mostrar los errores correspondientes, sin guardar ni
  enviar nada

### Requirement: Eliminar un pedido en borrador

El sistema SHALL permitir al Jefe de Cátedra propietario eliminar definitivamente un pedido propio
que esté en `borrador` — a diferencia de las transiciones de estado (aceptar, rechazar, devolver,
enviar, cancelar), eliminar no deja un evento en el historial: el pedido deja de existir. La acción
MUST estar disponible tanto desde la fila del pedido en "Mis pedidos" (control "Eliminar", X roja)
como desde el detalle del pedido (`/designaciones/pedidos/:id`), únicamente cuando el pedido está en
`borrador` y el actor es su Jefe de Cátedra propietario. El sistema MUST exigir una confirmación
explícita en un modal antes de eliminar. Un pedido `devuelto` — aunque también sea editable — NO MUST
ofrecer esta acción: ya tiene una revisión asociada en su historial.

#### Scenario: Eliminar un borrador propio, con confirmación

- **GIVEN** un Jefe de Cátedra viendo un pedido propio en `borrador` (en la lista o en el detalle)
- **WHEN** dispara "Eliminar" y confirma en el modal
- **THEN** el sistema borra el pedido definitivamente y ya no aparece en "Mis pedidos"

#### Scenario: Cancelar el modal no elimina nada

- **GIVEN** el modal de confirmación de "Eliminar" abierto sobre un borrador
- **WHEN** el Jefe de Cátedra elige "Cancelar" (o cierra el modal)
- **THEN** el sistema no ejecuta ninguna mutación y el pedido sigue listado

#### Scenario: Un pedido devuelto no ofrece la acción Eliminar

- **GIVEN** un pedido `devuelto` del que el actor es el Jefe de Cátedra propietario
- **WHEN** se lista en "Mis pedidos" o se abre su detalle
- **THEN** el sistema NO MUST ofrecer la acción "Eliminar" (solo "Editar")

#### Scenario: Eliminar desde el detalle vuelve a "Mis pedidos"

- **GIVEN** un Jefe de Cátedra viendo el detalle de su propio borrador
- **WHEN** elimina el pedido y confirma
- **THEN** el sistema navega de regreso a "Mis pedidos"

### Requirement: Edición de pedido en borrador o devuelto

El sistema SHALL permitir editar un pedido únicamente cuando está en estado `borrador`, o en estado `devuelto` cuando el actor es su propietario actual. La edición no cambia el estado del pedido [BR-designaciones-008].

#### Scenario: Editar un borrador propio

- **GIVEN** un pedido en estado `borrador` del Jefe de Cátedra
- **WHEN** el Jefe de Cátedra lo edita desde `/designaciones/pedidos/:id/editar` y guarda
- **THEN** los cambios se persisten y el pedido permanece en `borrador`

#### Scenario: No se puede editar tras enviar a revisión [BR-designaciones-008]

- **GIVEN** un pedido en estado `en_revision_coordinador`
- **WHEN** el Jefe de Cátedra intenta editarlo
- **THEN** el sistema MUST denegar la edición (el pedido queda read-only para el JC salvo devolución)

### Requirement: Cancelación de pedido en borrador

El sistema SHALL permitir al Jefe de Cátedra cancelar un pedido únicamente en estado `borrador`, llevándolo al estado terminal `cancelado`.

#### Scenario: Cancelar un borrador

- **GIVEN** un pedido en estado `borrador`
- **WHEN** el Jefe de Cátedra lo cancela
- **THEN** el pedido pasa a estado `cancelado` y deja de ofrecer acciones de edición o envío

#### Scenario: No se puede cancelar fuera de borrador

- **GIVEN** un pedido en estado `en_revision_coordinador`
- **WHEN** se intenta cancelarlo
- **THEN** el sistema MUST denegar la acción

### Requirement: Envío de pedido a revisión

El sistema SHALL permitir al Jefe de Cátedra dueño de la cátedra enviar a revisión un pedido en estado `borrador`, transicionándolo a `en_revision_coordinador` e iniciando la cadena de aprobación [BR-designaciones-008].

#### Scenario: Enviar un borrador a revisión

- **GIVEN** un pedido válido en estado `borrador`
- **WHEN** el Jefe de Cátedra lo envía a revisión
- **THEN** el pedido pasa a `en_revision_coordinador` y se registra el evento "enviar" en su historial

#### Scenario: No se puede enviar un pedido que no está en borrador

- **GIVEN** un pedido en un estado distinto de `borrador`
- **WHEN** se intenta enviarlo a revisión
- **THEN** el sistema MUST denegar la acción

### Requirement: Guards e idempotencia de la máquina de estados (lado Jefe de Cátedra)

La máquina de estados SHALL implementarse como lógica pura (sin React ni I/O) que, dada `(pedido, acción, actor)`, valida los guards y devuelve el pedido resultante o lanza un error de dominio. Las acciones sobre pedidos en estados terminales (`cancelado`, `rechazado`, `en_lote`) MUST ser denegadas (idempotencia terminal). Cada transición MUST registrar un evento en el historial del pedido.

#### Scenario: Acción sobre un pedido terminal es denegada

- **GIVEN** un pedido en estado `cancelado`
- **WHEN** se intenta enviar, editar o cancelar
- **THEN** la máquina de estados MUST lanzar un error de dominio y no modificar el pedido

#### Scenario: Cada transición deja rastro en el historial

- **WHEN** se aplica una transición válida (`enviar`, `cancelar`, `editar`)
- **THEN** el pedido resultante incluye un nuevo evento de historial con la acción, el rol, la etapa y la fecha

### Requirement: Persistencia del flujo mock entre roles y recargas

El sistema SHALL persistir el estado de los pedidos en un store que sobreviva a recargas de página y a cambios de rol dentro de la sesión (store singleton hidratado desde `localStorage`). Toda lectura y escritura de pedidos MUST pasar por una capa `api/` asíncrona que actúe como único punto de reemplazo por el backend real.

#### Scenario: El estado persiste tras recargar

- **GIVEN** un pedido creado y enviado a revisión
- **WHEN** se recarga la página
- **THEN** el pedido conserva su estado e historial

#### Scenario: El estado es coherente al cambiar de rol

- **GIVEN** un pedido enviado por el Jefe de Cátedra
- **WHEN** el usuario cambia al rol revisor de la etapa correspondiente
- **THEN** el pedido es visible en el estado en que quedó, sin perder su historial
