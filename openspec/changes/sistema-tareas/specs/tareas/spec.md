## ADDED Requirements

### Requirement: Creación de tarea restringida por rol

El sistema SHALL restringir la creación de tareas a los roles Secretaría Académica, Decanato y Administrativos. El botón "Nueva Tarea" MUST estar oculto para cualquier otro rol (Jefe de Cátedra, Coordinador de Carrera, Docente), y el sistema MUST rechazar la creación aunque se invoque la acción sin pasar por el botón (ej. navegación directa).

#### Scenario: Botón "Nueva Tarea" visible para Administrativo

- **WHEN** un usuario con rol Administrativo abre el listado de tareas (`/tareas`)
- **THEN** ve el botón "Nueva Tarea" en la esquina superior derecha del encabezado

#### Scenario: Botón "Nueva Tarea" oculto para Jefe de Cátedra

- **WHEN** un usuario con rol Jefe de Cátedra abre el listado de tareas
- **THEN** no ve el botón "Nueva Tarea"

#### Scenario: Creación bloqueada para un rol sin permiso

- **GIVEN** un usuario con rol Docente
- **WHEN** se invoca la acción de crear tarea sin pasar por el botón
- **THEN** el sistema rechaza la creación y no persiste ninguna tarea nueva

### Requirement: Jerarquía de asignación de Responsable

El sistema SHALL restringir a quién se puede asignar como Responsable de una tarea (al crearla o al reasignarlo en una edición posterior) según una jerarquía de autoridad, de mayor a menor: Decanato, Secretaría Académica, Administrativo, Coordinador de Carrera, Jefe de Cátedra, Docente. Quien asigna (siempre la autoridad creadora — Decanato, Secretaría Académica o Administrativo, los únicos roles habilitados para crear tareas) SHALL solo poder elegir un Responsable de su mismo nivel jerárquico o de un nivel inferior; MUST NOT poder asignar a alguien de un nivel superior. Esta restricción MUST aplicarse también al crear una tarea hija. Como solo esos tres roles crean tareas, la asignación entre pares del mismo nivel solo es alcanzable entre Decanato↔Decanato, Secretaría↔Secretaría o Administrativo↔Administrativo — Coordinador, Jefe de Cátedra y Docente nunca actúan como quien asigna, solo pueden ser Responsables.

#### Scenario: Decanato asigna una tarea a Secretaría Académica

- **WHEN** un usuario con rol Decanato crea una tarea y selecciona como Responsable a un usuario con rol Secretaría Académica
- **THEN** la tarea se crea correctamente

#### Scenario: Secretaría Académica no puede asignar a Decanato

- **WHEN** un usuario con rol Secretaría Académica busca un Responsable en el formulario de "Nueva Tarea"
- **THEN** el buscador no ofrece candidatos con rol Decanato

#### Scenario: Administrativo asigna a Docente

- **WHEN** un usuario con rol Administrativo crea una tarea con Responsable de rol Docente
- **THEN** la tarea se crea correctamente, aunque haya varios niveles de diferencia

#### Scenario: Asignación permitida entre pares del mismo nivel

- **GIVEN** dos usuarios con rol Secretaría Académica
- **WHEN** uno de ellos crea una tarea y se la asigna al otro como Responsable
- **THEN** la tarea se crea correctamente

#### Scenario: Administrativo no puede asignar a Secretaría Académica

- **WHEN** un usuario con rol Administrativo busca un Responsable en el formulario de "Nueva Tarea"
- **THEN** el buscador no ofrece candidatos con rol Secretaría Académica ni Decanato

### Requirement: Formulario de alta de tarea

El formulario de "Nueva Tarea" SHALL solicitar Título, Descripción, Fecha de Inicio, Fecha de Fin, Prioridad, Tipo y Responsable (un único usuario, elegido con un campo de búsqueda: se tipea texto y se selecciona de la lista de candidatos, restringida por la jerarquía de asignación — ver Requirement "Jerarquía de asignación de Responsable"). Título, Fecha de Inicio, Fecha de Fin, Prioridad, Tipo y Responsable MUST ser obligatorios. La Fecha de Fin MUST ser posterior o igual a la Fecha de Inicio. El formulario MAY además ofrecer un selector de Proyecto (opcional — ver Requirement "Asociación de una tarea con un Proyecto"), salvo cuando la tarea que se está creando es una tarea hija, caso en el que el campo Proyecto no se ofrece porque se hereda del padre.

#### Scenario: Alta exitosa con todos los campos completos

- **WHEN** una autoridad completa Título, Fecha de Inicio, Fecha de Fin, Prioridad, Tipo, busca y selecciona un Responsable, y confirma
- **THEN** se crea la tarea en estado Pendiente, con % de avance en 0, un número correlativo asignado, y aparece en el listado

#### Scenario: Campo obligatorio faltante bloquea el alta

- **WHEN** una autoridad intenta confirmar el formulario sin seleccionar un Responsable
- **THEN** se muestra el error "Campo obligatorio" en Responsable y no se crea la tarea

#### Scenario: Alta sin Tipo bloqueada

- **WHEN** una autoridad intenta confirmar el formulario sin seleccionar un Tipo
- **THEN** se muestra el error "Campo obligatorio" en Tipo y no se crea la tarea

#### Scenario: Fecha de Fin anterior a Fecha de Inicio

- **WHEN** una autoridad ingresa una Fecha de Fin anterior a la Fecha de Inicio y confirma
- **THEN** se muestra un error de validación y no se crea la tarea

### Requirement: Tipo de tarea

Toda tarea SHALL tener un Tipo, uno de: Extensión, Administrativa, Posgrado, Investigación, Académicas o Decanato. El Tipo MUST mostrarse en la pantalla de Detalle. No es una columna del listado: la tabla de tareas mantiene su formato actual (Nro, Título, Autor, Responsable, Fecha Inicio, Fecha Fin, Prioridad, % Avance, Estado, Acciones), sin agregar Tipo como columna ni como filtro de header.

#### Scenario: El Tipo se muestra en el Detalle

- **GIVEN** una tarea con Tipo "Posgrado"
- **WHEN** se renderiza su pantalla de Detalle
- **THEN** se muestra "Posgrado" como Tipo de la tarea

#### Scenario: El Tipo no aparece en el listado

- **GIVEN** una tarea con Tipo "Posgrado"
- **WHEN** se renderiza su fila en el listado
- **THEN** ninguna columna muestra el Tipo, y el header no ofrece filtrar por Tipo

### Requirement: Asociación de una tarea con un Proyecto

Una tarea MAY asociarse a un único Proyecto existente mediante un selector en el formulario de alta/edición; una tarea sin Proyecto asociado SHALL seguir siendo válida y completa (el Proyecto nunca es obligatorio para una tarea de primer nivel). Si la tarea es una tarea hija (tiene una tarea padre — ver Requirement "Tareas hijas"), su Proyecto MUST heredarse automáticamente del Proyecto de su padre, y el campo Proyecto MUST NOT ofrecerse como editable de forma independiente en ese caso.

#### Scenario: Crear una tarea sin Proyecto

- **WHEN** una autoridad completa el formulario de alta sin seleccionar ningún Proyecto
- **THEN** la tarea se crea igual, sin Proyecto asociado

#### Scenario: Crear una tarea asociada a un Proyecto

- **WHEN** una autoridad selecciona el Proyecto "Nuevo sistema de Ingeniería para Testing" al completar el formulario
- **THEN** la tarea se crea asociada a ese Proyecto

#### Scenario: Una tarea hija hereda el Proyecto del padre

- **GIVEN** una tarea padre asociada al Proyecto "Nuevo sistema de Ingeniería para Testing"
- **WHEN** una autoridad crea una tarea hija de esa tarea
- **THEN** la hija queda asociada automáticamente a "Nuevo sistema de Ingeniería para Testing", sin que el formulario ofrezca elegir otro Proyecto

### Requirement: Relación simple entre tareas

El sistema SHALL permitir vincular dos tareas entre sí como "relacionadas", desde la pantalla de Detalle, para dar acceso rápido de una a la otra. Esta relación MUST NOT implicar jerarquía (no es una relación padre/hija) ni afectar el Estado o el % de avance de ninguna de las dos tareas. La relación SHALL ser visible desde el Detalle de ambas tareas (bidireccional). Una tarea MAY tener cero, una o varias tareas relacionadas.

#### Scenario: Relacionar dos tareas existentes

- **WHEN** un usuario con acceso a la tarea A la relaciona con la tarea B desde el Detalle de A
- **THEN** el Detalle de A muestra un acceso rápido a B, y el Detalle de B muestra un acceso rápido a A

#### Scenario: Quitar una relación

- **GIVEN** dos tareas relacionadas entre sí
- **WHEN** un usuario quita la relación desde el Detalle de una de ellas
- **THEN** ninguna de las dos vuelve a mostrar a la otra como relacionada

### Requirement: Tareas hijas (jerarquía padre/hijas)

El sistema SHALL permitir crear tareas hijas dentro de una tarea (su padre), para descomponer una tarea compleja en partes más chicas de gestión independiente. Cada tarea hija SHALL comportarse como una tarea completa e independiente: tiene su propio Responsable (que puede coincidir o no con el Responsable de su padre), sus propias Fecha de Inicio y Fecha de Fin, su propio Estado y su propio % de avance — ninguno de estos campos del padre SHALL recalcularse automáticamente a partir de sus hijas; el Estado y el % de avance del padre se editan a mano, igual que en cualquier otra tarea. Una tarea hija MAY a su vez tener sus propias tareas hijas (jerarquía de más de un nivel). Crear una tarea hija MUST estar restringido a los mismos roles habilitados para crear tareas (Secretaría Académica, Decanato, Administrativos).

#### Scenario: Crear una tarea hija

- **GIVEN** una tarea padre "Migración del campus virtual"
- **WHEN** una autoridad crea una tarea hija "Migrar el módulo de calificaciones" desde el Detalle del padre
- **THEN** la hija se crea como una tarea independiente, con su propio Responsable, Estado y % de avance en 0, y aparece listada como hija de "Migración del campus virtual"

#### Scenario: Modificar una hija no afecta al padre

- **GIVEN** una tarea padre con 30% de avance y una tarea hija con 10% de avance
- **WHEN** el Responsable de la hija actualiza su avance a 80%
- **THEN** el % de avance de la tarea padre sigue en 30%, sin cambios

#### Scenario: Una tarea hija puede tener sus propias hijas

- **GIVEN** una tarea hija "Migrar el módulo de calificaciones"
- **WHEN** una autoridad crea una tarea hija de esa hija
- **THEN** la nueva tarea se crea correctamente, anidada dos niveles debajo de la tarea padre original

#### Scenario: Un rol sin permiso no puede crear tareas hijas

- **GIVEN** un usuario con rol Docente
- **WHEN** intenta crear una tarea hija de una tarea existente
- **THEN** el sistema rechaza la acción

### Requirement: Pantalla de Detalle de Tarea

El sistema SHALL ofrecer una pantalla de detalle por tarea (`/tareas/:id`), accesible al hacer click en una fila del listado, que MUST mostrar: número de tarea, título, estado, prioridad, semáforo de vencimiento, descripción, fecha de inicio, fecha de fin, % de avance, Responsable, Autor, solución (cuando exista), comentarios internos e historial de cambios.

#### Scenario: Apertura del detalle desde el listado

- **WHEN** un usuario hace click en una fila del listado de tareas
- **THEN** navega a `/tareas/:id` y ve todos los datos de esa tarea

#### Scenario: Tarea inexistente

- **WHEN** un usuario navega a `/tareas/:id` con un id que no existe
- **THEN** ve un mensaje de error indicando que la tarea no se encontró, con un enlace para volver al listado

### Requirement: Porcentaje de avance editable por el Responsable

Toda tarea SHALL tener un porcentaje de avance entre 0 y 100, inicializado en 0 al crearse. Únicamente el Responsable de la tarea (o la autoridad creadora) SHALL poder modificarlo, desde la pantalla de Detalle. El sistema MUST rechazar valores fuera del rango 0-100.

#### Scenario: El Responsable actualiza el porcentaje de avance

- **GIVEN** una tarea con 20% de avance, asignada como Responsable al usuario actual
- **WHEN** el Responsable lo actualiza a 60
- **THEN** el porcentaje de avance de la tarea pasa a 60 y queda registrado en el historial

#### Scenario: Valor fuera de rango rechazado

- **WHEN** el Responsable intenta ingresar un porcentaje de avance de 120
- **THEN** el sistema muestra un error de validación y no guarda el cambio

#### Scenario: Un tercero no puede editar el avance

- **GIVEN** una tarea asignada a otro Responsable, creada por otra autoridad
- **WHEN** un usuario ajeno a la tarea intenta editar el porcentaje de avance
- **THEN** el sistema no permite la edición

### Requirement: Comentarios internos en la tarea

El sistema SHALL permitir agregar comentarios internos de texto libre en la pantalla de Detalle, visibles para cualquiera con acceso a esa tarea, ordenados cronológicamente con autor y fecha.

#### Scenario: Agregar un comentario

- **WHEN** un usuario con acceso a la tarea escribe un comentario y lo envía
- **THEN** el comentario aparece al final del hilo con su nombre, rol y fecha

### Requirement: Historial de cambios de la tarea

El sistema SHALL registrar en el historial de la tarea cada creación, cambio de estado, actualización de porcentaje de avance, edición de campos y cancelación, con quién lo hizo, su rol, el estado resultante y la fecha. El historial MUST mostrarse en orden cronológico en la pantalla de Detalle.

#### Scenario: Cambio de estado queda registrado

- **WHEN** el Responsable cambia el estado de la tarea de Pendiente a En curso
- **THEN** el historial muestra un nuevo evento con esa transición, el nombre y rol del Responsable, y la fecha
