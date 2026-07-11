## MODIFIED Requirements

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

## ADDED Requirements

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
