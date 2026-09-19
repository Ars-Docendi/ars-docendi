## ADDED Requirements

### Requirement: Creación de tarea restringida por rol

El sistema SHALL restringir la creación de tareas a los roles Secretaría Académica, Decanato y Administrativos. El botón "Nueva Tarea" MUST estar oculto para cualquier otro rol (Jefe de Cátedra, Coordinador, Docente), y el sistema MUST rechazar la creación aunque se invoque la acción sin pasar por el botón (ej. navegación directa).

#### Scenario: Botón "Nueva Tarea" visible para Administración

- **WHEN** un usuario con rol Administración abre el listado de tareas (`/tareas`)
- **THEN** ve el botón "Nueva Tarea" en la esquina superior derecha del encabezado

#### Scenario: Botón "Nueva Tarea" oculto para Jefe de Cátedra

- **WHEN** un usuario con rol Jefe de Cátedra abre el listado de tareas
- **THEN** no ve el botón "Nueva Tarea"

#### Scenario: Creación bloqueada para un rol sin permiso

- **GIVEN** un usuario con rol Docente
- **WHEN** se invoca la acción de crear tarea sin pasar por el botón
- **THEN** el sistema rechaza la creación y no persiste ninguna tarea nueva

### Requirement: Formulario de alta de tarea

El formulario de "Nueva Tarea" SHALL solicitar Título, Descripción, Fecha de Inicio, Fecha de Fin, Prioridad y Responsable (un único usuario, elegido con un campo de búsqueda: se tipea texto y se selecciona de la lista de candidatos). Título, Fecha de Inicio, Fecha de Fin, Prioridad y Responsable MUST ser obligatorios. La Fecha de Fin MUST ser posterior o igual a la Fecha de Inicio.

#### Scenario: Alta exitosa con todos los campos completos

- **WHEN** una autoridad completa Título, Fecha de Inicio, Fecha de Fin, Prioridad, busca y selecciona un Responsable, y confirma
- **THEN** se crea la tarea en estado Pendiente, con % de avance en 0, un número correlativo asignado, y aparece en el listado

#### Scenario: Campo obligatorio faltante bloquea el alta

- **WHEN** una autoridad intenta confirmar el formulario sin seleccionar un Responsable
- **THEN** se muestra el error "Campo obligatorio" en Responsable y no se crea la tarea

#### Scenario: Fecha de Fin anterior a Fecha de Inicio

- **WHEN** una autoridad ingresa una Fecha de Fin anterior a la Fecha de Inicio y confirma
- **THEN** se muestra un error de validación y no se crea la tarea

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
