## MODIFIED Requirements

### Requirement: Lista "Mis pedidos" del Jefe de Cátedra

El sistema SHALL ofrecer al Jefe de Cátedra una pantalla "Mis pedidos" (`/designaciones/mis-pedidos`)
que liste los pedidos de designación de su cátedra para el período abierto, mostrando por cada pedido
el docente (sin prefijo de tratamiento), su **legajo** (o un placeholder si todavía no tiene, caso de
una Alta reciente), la materia asociada, el **tipo** (novedad), el estado (vía `StatusBadge`) y el flag
de prioritario. La pantalla MUST ofrecer un filtro con dos campos de texto siempre visibles —
**Docente** (angosto, mismo ancho que el campo equivalente de la pantalla Usuarios) y **N°** — más un
mecanismo para agregar como filtros opcionales **Legajo**, **Tipo** y **Estado** (mismo patrón que el
filtro de la pantalla de Usuarios, implementado mediante un componente de filtro genérico y reutilizable
por otras pantallas); el filtro **Tipo** SHALL ofrecer únicamente las tres novedades vigentes ("Alta",
"Baja", "Cambio de cargo o dedicación") — la novedad "Sin novedad" ya no existe en el sistema. Cada fila
MUST navegar al detalle del pedido al hacer click en cualquier parte de la fila. Al final de la fila,
los controles **"Ver"**, **"Editar"** y **"Eliminar"** (X) MUST estar siempre visibles y en la misma
posición — ninguno se oculta condicionalmente. "Ver" SHALL estar siempre habilitado. "Editar" y
"Eliminar" MUST deshabilitarse (no ocultarse) cuando el pedido no es editable, o no está en `borrador`,
respectivamente — un botón deshabilitado SHALL verse semitransparente (mismo estilo `ghost`, opacidad
reducida) y no disparar su acción al clickearlo. "Ver" y "Editar" usan el mismo formato que las acciones
de fila de la pantalla Usuarios (`Button variant="ghost" size="sm"`, sin ícono); "Eliminar" es la X roja
(ver "Eliminar un pedido en borrador"). La pantalla MUST representar explícitamente los cuatro estados
de carga: Loading, Empty, Error y Success.

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

#### Scenario: Click en la fila abre el detalle del pedido

- **GIVEN** cualquier pedido listado, en cualquier estado
- **WHEN** el Jefe de Cátedra hace click en la fila (fuera del botón "Editar")
- **THEN** el sistema navega a `/designaciones/pedidos/:id` de ese pedido

#### Scenario: El botón Editar es fijo: habilitado si el pedido es editable, deshabilitado si no

- **GIVEN** un pedido en `borrador`, o `devuelto` con el Jefe de Cátedra como propietario actual, y otro
  en cualquier otro estado (en revisión, rechazado, cancelado, en lote)
- **WHEN** se listan en "Mis pedidos"
- **THEN** ambas filas MUST mostrar el botón "Editar"
- **AND** el del primer grupo MUST estar habilitado
- **AND** el del segundo MUST estar deshabilitado (semitransparente), no oculto

#### Scenario: El botón Ver está siempre disponible y navega al detalle

- **GIVEN** cualquier pedido listado, en cualquier estado
- **WHEN** el Jefe de Cátedra hace click en el botón "Ver" de su fila
- **THEN** el sistema navega a `/designaciones/pedidos/:id` de ese pedido, sin disparar dos veces la
  navegación (el click en "Ver" no burbujea al `onClick` de la fila)

#### Scenario: La X de eliminar es fija: habilitada solo en borrador, deshabilitada en el resto

- **GIVEN** una fila con un pedido en `borrador` y otra con un pedido `devuelto`
- **WHEN** se listan en "Mis pedidos"
- **THEN** ambas filas MUST mostrar el control "Eliminar" (X roja)
- **AND** la del `borrador` MUST estar habilitada
- **AND** la del `devuelto` (y de cualquier otro estado) MUST estar deshabilitada (semitransparente),
  no oculta

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

#### Scenario: El filtro Tipo ya no ofrece "Sin novedad"

- **GIVEN** el filtro opcional Tipo agregado
- **WHEN** el Jefe de Cátedra abre el `Select` de Tipo
- **THEN** ve únicamente "Alta", "Baja" y "Cambio de cargo o dedicación" — sin la opción "Sin novedad"

### Requirement: Secciones condicionales por novedad

El form de pedido SHALL mostrar u ocultar secciones según la novedad seleccionada (Radio: "Alta" /
"Baja" / "Cambio de cargo o dedicación" — la novedad "Sin novedad" ya no existe en el sistema, el radio
tiene exactamente estas tres opciones). "Alta" y "Cambio de cargo o dedicación" exponen cargo y
dedicación solicitados, más horas de investigación, horas externas y si el docente es agente externo;
"Baja" expone el tipo de baja.

#### Scenario: La sección de solicitud aparece solo para Alta y Cambio

- **WHEN** el usuario selecciona la novedad "Alta" o "Cambio de cargo o dedicación"
- **THEN** el form muestra los campos de cargo y dedicación solicitados, los campos de horas de
  investigación y horas externas, y el checkbox de agente externo
- **AND WHEN** selecciona "Baja"
- **THEN** el form oculta esos campos (Baja no tiene designación solicitada, es la baja del docente)

#### Scenario: La sección de adjuntos se adapta a la novedad

- **WHEN** el usuario cambia la novedad seleccionada
- **THEN** la sección de adjuntos requeridos se actualiza para reflejar los adjuntos exigidos por esa
  novedad

### Requirement: Materias y horas del pedido

El sistema SHALL permitir que un pedido de novedad "Alta" o "Cambio de cargo o dedicación" incluya
cero o más asignaciones de materia, cada una con su propia carga horaria (materia + horas), agregables,
quitables y con la materia seleccionable/cambiable desde el form — el mismo patrón de lista, y la misma
regla, en ambas novedades: **ninguna de las dos exige un mínimo de materias** (regla de negocio: el
docente se da de alta, o se le procesa un cambio, solo con cargo y dedicación — las materias se asignan
después, fuera de este flujo). El sistema NO MUST impedir quitar la última fila restante en Alta ni en
Cambio. En Cambio, la lista SHALL precargarse con las materias que ya tiene el docente seleccionado,
pero queda igual de abierta a vaciarse por completo.

Para la novedad "Baja", el sistema SHALL mostrar el mismo listado de materias y horas que ya tiene el
docente (`materiasActuales`), pero íntegramente de solo lectura: ni la materia ni las horas ni el
listado en sí (agregar/quitar) son editables — es información de contexto sobre qué queda vacante, no
un dato a modificar.

El listado de materias reemplaza cualquier mención de la materia en el panel de datos actuales de
solo lectura (evita duplicar la misma información en dos lugares del form) en Cambio y en Baja.

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

#### Scenario: Cambio también permite quitar la última fila de materia

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" con una única fila de materia restante
  en el listado
- **WHEN** el Jefe de Cátedra intenta quitar esa última fila
- **THEN** el sistema MUST permitir la acción, dejando el listado en cero materias

#### Scenario: Alta se puede guardar y enviar sin ninguna materia

- **GIVEN** un pedido de novedad "Alta" con cargo y dedicación solicitados completos, adjuntos
  obligatorios cargados, y el listado de materias vacío (el Jefe de Cátedra quitó la única fila que
  traía por default)
- **WHEN** el Jefe de Cátedra hace click en "Guardar y enviar"
- **THEN** el sistema MUST guardar y enviar el pedido sin exigir ninguna materia — la validación NO MUST
  bloquear por `asignaciones` vacío en esta novedad

#### Scenario: Cambio se puede guardar y enviar sin ninguna materia

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" con cargo y dedicación solicitados
  completos, justificación cargada, docente con legajo, y el listado de materias vacío
- **WHEN** el Jefe de Cátedra hace click en "Guardar y enviar"
- **THEN** el sistema MUST guardar y enviar el pedido sin exigir ninguna materia — la validación NO MUST
  bloquear por `asignaciones` vacío en esta novedad

#### Scenario: Baja sigue exigiendo al menos una materia

- **GIVEN** un pedido de novedad "Baja" cuyo `asignaciones` llegara vacío (no debería pasar en el flujo
  normal, que precarga desde el docente existente)
- **WHEN** el Jefe de Cátedra intenta guardar y enviar
- **THEN** la validación MUST rechazarlo por falta de materias — a diferencia de Alta y Cambio, Baja no
  quedó incluida en la regla de negocio nueva

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

### Requirement: Resumen de cambios en el panel de datos actuales (Cambio)

En "Cambio de cargo o dedicación", el panel de solo lectura de datos actuales SHALL mostrar, además de
antigüedad, la transición `actual → solicitado` de **todos** los campos que Cambio puede modificar:
cargo, dedicación, cada materia con su carga horaria, y horas de investigación/externas. Un campo sin
cambios SHALL mostrarse con su valor plano (sin flecha de transición). En "Baja" el panel NO MUST
mostrar transiciones (no hay valores "solicitados" que comparar). El campo "agente externo" (ver
"Horas de investigación y horas externas del pedido") NO MUST mostrarse en este panel — no tiene un
valor "actual" contra el cual comparar (es un dato nuevo, sin histórico previo).

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
validar que su suma junto con las horas de materia cierre contra la dedicación solicitada o actual. El
sistema SHALL además permitir, junto al campo de horas externas, marcar si el docente es agente
externo (`esAgenteExterno`, checkbox booleano, sin marcar por default) — mismo alcance de novedades
(Alta y Cambio); no tiene un "valor actual" contra el cual comparar (ver "Resumen de cambios en el
panel de datos actuales (Cambio)"). Cuando el checkbox "Docente es agente externo" está marcado, el
sistema SHALL mostrar un `Select` **"Departamento a cargo"** (`departamentoAgenteExterno`) con un
catálogo cerrado de 7 opciones: Departamento de Arquitectura, Departamento de Salud, Departamento de
Derecho, Departamento de Económicas, Departamento de Humanidades, Departamento de Odontología y
Secretaría Académica. El `Select` NO MUST mostrarse cuando el checkbox está desmarcado, y el sistema
MUST exigir un valor seleccionado antes de permitir "Guardar y enviar" mientras el checkbox esté
marcado. Al desmarcar el checkbox, el sistema MUST limpiar cualquier departamento previamente
seleccionado.

#### Scenario: Carga de horas de investigación y externas

- **GIVEN** un Jefe de Cátedra cargando un pedido de "Alta" o "Cambio de cargo o dedicación"
- **WHEN** completa los campos "Horas de investigación" y "Horas externas (otro depto.)" con valores
  numéricos
- **THEN** el pedido guarda ambos valores asociados al docente, no a una materia en particular

#### Scenario: Sin validación de cierre contra la dedicación

- **GIVEN** un pedido con horas de materia, investigación y externas cargadas
- **WHEN** la suma de esas horas no coincide con lo esperable para la dedicación solicitada
- **THEN** el sistema NO MUST bloquear el guardado por esa discrepancia (las horas son campos libres)

#### Scenario: Marcar al docente como agente externo

- **GIVEN** un Jefe de Cátedra cargando un pedido de "Alta" o "Cambio de cargo o dedicación"
- **WHEN** marca el checkbox "Docente es agente externo", ubicado junto al campo "Horas externas (otro
  depto.)"
- **THEN** el pedido guarda `esAgenteExterno: true`, visible luego en el resumen de detalle del pedido

#### Scenario: Agente externo no aplica a Baja

- **GIVEN** un pedido de novedad "Baja"
- **WHEN** el Jefe de Cátedra visualiza el form
- **THEN** NO MUST ver el checkbox "Docente es agente externo" (Baja no muestra la sección de
  designación solicitada)

#### Scenario: Marcar agente externo habilita el selector de departamento

- **GIVEN** un Jefe de Cátedra cargando un pedido de "Alta" o "Cambio de cargo o dedicación", sin
  marcar "Docente es agente externo"
- **WHEN** marca el checkbox
- **THEN** aparece el `Select` "Departamento a cargo" con las 7 opciones del catálogo cerrado

#### Scenario: Desmarcar agente externo limpia el departamento

- **GIVEN** un pedido con "Docente es agente externo" marcado y un departamento seleccionado
- **WHEN** el Jefe de Cátedra desmarca el checkbox
- **THEN** el `Select` "Departamento a cargo" desaparece y el sistema MUST limpiar el valor
  seleccionado (no queda guardado un departamento sin su checkbox)

#### Scenario: Agente externo sin departamento bloquea el envío

- **GIVEN** un pedido de "Alta" o "Cambio de cargo o dedicación" con "Docente es agente externo"
  marcado y ningún departamento seleccionado
- **WHEN** el Jefe de Cátedra hace click en "Guardar y enviar"
- **THEN** el sistema MUST bloquear el envío e indicar que el departamento a cargo es obligatorio
- **AND** "Guardar pedido" (sin enviar) NO MUST bloquearse por esta razón, igual que el resto de los
  campos obligatorios del pedido
