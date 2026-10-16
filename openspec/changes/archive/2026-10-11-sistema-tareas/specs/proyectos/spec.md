## ADDED Requirements

### Requirement: Creación de Proyecto restringida por rol

El sistema SHALL restringir la creación de Proyectos a los roles Secretaría Académica y Decanato. El botón "Nuevo Proyecto", ubicado en la pestaña Proyectos, MUST estar oculto para cualquier otro rol, incluido Administrativos (que sí puede crear Tareas, pero no Proyectos), y el sistema MUST rechazar la creación aunque se invoque la acción sin pasar por el botón.

#### Scenario: Botón "Nuevo Proyecto" visible para Decanato

- **WHEN** un usuario con rol Decanato abre la pestaña Proyectos
- **THEN** ve el botón "Nuevo Proyecto"

#### Scenario: Botón "Nuevo Proyecto" oculto para Administrativos

- **WHEN** un usuario con rol Administrativos abre la pestaña Proyectos
- **THEN** ve el listado de proyectos pero no ve el botón "Nuevo Proyecto"

#### Scenario: Creación bloqueada para un rol sin permiso

- **GIVEN** un usuario con rol Jefe de Cátedra
- **WHEN** se invoca la acción de crear un Proyecto sin pasar por el botón
- **THEN** el sistema rechaza la creación y no persiste ningún Proyecto nuevo

### Requirement: Formulario de alta de Proyecto

El formulario de "Nuevo Proyecto" SHALL solicitar Nombre, Descripción, Fecha de Inicio, Fecha de Fin y Responsable (un único usuario, elegido con un campo de búsqueda, cuyas opciones MUST estar restringidas a candidatos de rol Secretaría Académica o Decanato). Nombre, Fecha de Inicio, Fecha de Fin y Responsable MUST ser obligatorios. La Fecha de Fin MUST ser posterior o igual a la Fecha de Inicio.

El Responsable MUST además respetar la misma jerarquía de asignación que las tareas (ver `specs/tareas/spec.md`, Requirement "Jerarquía de asignación de Responsable"): si quien crea el Proyecto es Secretaría Académica, el Responsable solo puede ser Secretaría Académica (no puede asignarlo a Decanato); si quien lo crea es Decanato, el Responsable puede ser Decanato o Secretaría Académica.

#### Scenario: Alta exitosa con todos los campos completos

- **WHEN** una autoridad completa Nombre, Fecha de Inicio, Fecha de Fin, busca y selecciona un Responsable, y confirma
- **THEN** se crea el Proyecto en estado Abierto y aparece como un nuevo cuadro en la pantalla inicial

#### Scenario: Campo obligatorio faltante bloquea el alta

- **WHEN** una autoridad intenta confirmar el formulario sin seleccionar un Responsable
- **THEN** se muestra el error "Campo obligatorio" en Responsable y no se crea el Proyecto

#### Scenario: El selector de Responsable solo ofrece candidatos de Secretaría o Decanato

- **WHEN** una autoridad abre el buscador de Responsable en el formulario de "Nuevo Proyecto"
- **THEN** solo aparecen candidatos con rol Secretaría Académica o Decanato, sin importar el catálogo completo de personas

#### Scenario: Secretaría Académica no puede asignar el Proyecto a Decanato

- **WHEN** un usuario con rol Secretaría Académica busca un Responsable en el formulario de "Nuevo Proyecto"
- **THEN** el buscador solo ofrece candidatos con rol Secretaría Académica, no Decanato

#### Scenario: Decanato puede asignar el Proyecto a Secretaría Académica

- **WHEN** un usuario con rol Decanato busca un Responsable en el formulario de "Nuevo Proyecto"
- **THEN** el buscador ofrece candidatos con rol Decanato y Secretaría Académica

#### Scenario: Fecha de Fin anterior a Fecha de Inicio

- **WHEN** una autoridad ingresa una Fecha de Fin anterior a la Fecha de Inicio y confirma
- **THEN** se muestra un error de validación y no se crea el Proyecto

### Requirement: Estados de un Proyecto

Un Proyecto SHALL tener exactamente uno de los siguientes estados en todo momento: Abierto, Finalizado o Cancelado. Todo Proyecto nuevo MUST crearse en estado Abierto.

#### Scenario: Proyecto nuevo nace en Abierto

- **WHEN** una autoridad crea un Proyecto
- **THEN** el estado inicial del Proyecto es Abierto

### Requirement: Cambio de estado de un Proyecto restringido por rol

El sistema SHALL restringir el cambio de estado de un Proyecto (a Finalizado o a Cancelado) a los roles Secretaría Académica y Decanato — los mismos habilitados para crearlos, sin restricción adicional a que el usuario sea específicamente el Responsable de ese Proyecto.

#### Scenario: Decanato finaliza un Proyecto

- **GIVEN** un Proyecto Abierto
- **WHEN** un usuario con rol Decanato lo marca como Finalizado
- **THEN** el estado del Proyecto pasa a Finalizado

#### Scenario: Un rol sin permiso no puede cambiar el estado

- **GIVEN** un Proyecto Abierto
- **WHEN** un usuario con rol Administrativos intenta cambiar su estado
- **THEN** el sistema rechaza la acción y el Proyecto conserva su estado anterior

### Requirement: Un Proyecto Cancelado no pasa a Finalizado

El sistema MUST rechazar (422) el cambio de estado de un Proyecto Cancelado a Finalizado, y el Detalle de Proyecto MUST NOT ofrecer esa acción. Para finalizarlo debe reabrirse antes.

#### Scenario: Finalizar un Proyecto Cancelado

- **GIVEN** un Proyecto Cancelado
- **WHEN** un Decano intenta pasarlo a Finalizado
- **THEN** el servidor responde 422, el Proyecto sigue Cancelado y el botón "Finalizar" no se muestra

### Requirement: Pantalla de Detalle de Proyecto

El sistema SHALL ofrecer una pantalla de Detalle por Proyecto (`/tareas/proyectos/:id`), accesible desde el título de su cuadro en la pantalla inicial, que MUST mostrar: nombre, descripción, estado, Fecha de Inicio, Fecha de Fin, Responsable, y la tabla de tareas asociadas a ese Proyecto (mismo modelo de filtros, orden y colores que el resto de las tablas de tareas).

#### Scenario: Apertura del Detalle desde el cuadro del Proyecto

- **WHEN** un usuario hace click en el título del cuadro de un Proyecto en la pantalla inicial
- **THEN** navega a `/tareas/proyectos/:id` y ve todos los datos de ese Proyecto junto con sus tareas

#### Scenario: Proyecto inexistente

- **WHEN** un usuario navega a `/tareas/proyectos/:id` con un id que no existe
- **THEN** ve un mensaje de error indicando que el Proyecto no se encontró, con un enlace para volver a Tareas

### Requirement: Acceso manual a Proyectos Finalizados o Cancelados

Como la pantalla inicial de Tareas solo genera un cuadro por cada Proyecto en estado Abierto, el sistema SHALL ofrecer una pantalla de listado con **todos** los Proyectos (`/tareas/proyectos`), sin importar su Estado, accesible desde la pestaña "Proyectos" del menú lateral. Desde ese listado SHALL poder navegarse al Detalle de cualquier Proyecto, incluidos los Finalizados y Cancelados.

#### Scenario: Acceder a un Proyecto Finalizado desde el listado completo

- **GIVEN** un Proyecto en estado Finalizado, que ya no tiene cuadro en la pantalla inicial
- **WHEN** un usuario abre el listado completo de Proyectos y hace click en él
- **THEN** navega a su Detalle y ve sus datos completos, incluidas sus tareas

#### Scenario: El listado completo incluye Proyectos de cualquier Estado

- **GIVEN** Proyectos Abiertos, Finalizados y Cancelados
- **WHEN** un usuario abre `/tareas/proyectos`
- **THEN** ve los tres, cada uno con su Estado indicado

### Requirement: Pestaña Proyectos

El menú lateral SHALL ofrecer, junto a "Tareas", una pestaña "Proyectos" (`/tareas/proyectos`) visible para quien tiene el permiso `tareas.gestionar`. Su pantalla MUST ser un listado básico de todos los proyectos (Nombre, Responsable, Fecha de fin, Estado) con, a lo sumo, el botón "Nuevo Proyecto" para quien tiene `proyectos.gestionar`; la pantalla inicial de Tareas ya no ofrece ni el botón ni un enlace al listado.

#### Scenario: Pestaña visible para quien crea tareas

- **GIVEN** un usuario con `tareas.gestionar`
- **WHEN** abre la aplicación
- **THEN** el menú lateral muestra "Proyectos" y al elegirla ve el listado de proyectos

### Requirement: Edición de un Proyecto

El Detalle de Proyecto SHALL ofrecer un botón "Editar" a quien tiene `proyectos.gestionar`, que MUST abrir el mismo formulario del alta precargado con Nombre, Descripción, Fecha de Inicio, Fecha de Fin y Responsable, con las mismas validaciones. El Estado no se edita ahí (se cambia con sus propios botones). El servidor MUST exponer `PUT /api/tareas/proyectos/{id}` (permiso `proyectos.gestionar`), y solo cuando cambia el Responsable MUST exigir que sea Decanato o Secretaría Académica y respete la jerarquía; conservar al mismo Responsable no lo reevalúa.

#### Scenario: Editar los datos de un Proyecto

- **GIVEN** un Decano en el Detalle de un Proyecto
- **WHEN** elige "Editar", cambia el nombre y la Fecha de fin y guarda
- **THEN** el Detalle y el listado muestran los datos nuevos y el Estado no cambia

#### Scenario: Edición bloqueada sin permiso

- **GIVEN** un usuario sin `proyectos.gestionar`
- **WHEN** invoca `PUT /api/tareas/proyectos/{id}`
- **THEN** recibe 403 y el Proyecto no cambia

### Requirement: Disposición de los datos en el Detalle de Proyecto

En el Detalle de Proyecto el panel "Datos" MUST ubicarse arriba de la Descripción, con la Fecha de inicio y la Fecha de fin una al lado de la otra y, debajo, el Responsable.

#### Scenario: Orden de los paneles

- **WHEN** un usuario abre el Detalle de un Proyecto
- **THEN** ve primero "Datos" (fechas de inicio y fin en la misma fila, luego el Responsable) y después la Descripción

### Requirement: Crear una tarea dentro de un Proyecto

El Detalle de un Proyecto cuyo estado admite tareas SHALL ofrecer el botón "Nueva Tarea" a quien puede crear tareas. El formulario MUST abrir con el Proyecto ya asignado y no editable, y la tarea creada MUST aparecer en la tabla de tareas de ese Proyecto. El botón MUST estar oculto en Proyectos que ya no admiten tareas.

#### Scenario: Nueva tarea desde el Proyecto

- **GIVEN** un Decano en el Detalle de un Proyecto Abierto
- **WHEN** elige "Nueva Tarea", completa el formulario y guarda
- **THEN** la tarea queda asociada a ese Proyecto y aparece en su tabla de tareas

#### Scenario: Proyecto cerrado

- **WHEN** un usuario abre el Detalle de un Proyecto Finalizado
- **THEN** no ve el botón "Nueva Tarea"

### Requirement: Filtros y orden del listado de Proyectos

La tabla de la pestaña Proyectos SHALL ofrecer el mismo tipo de filtros por columna que la tabla de Tareas (texto libre en N°, Nombre, Inicio y Fin; selección múltiple en Responsable y Estado) y el mismo ciclo de orden por encabezado (ascendente, descendente, sin orden manual). Sin orden manual, los proyectos MUST ordenarse primero por estado, según el orden del catálogo de estados, y luego por fecha de inicio ascendente. Las opciones del filtro Estado MUST salir del catálogo.

#### Scenario: Orden por defecto

- **GIVEN** proyectos Abiertos, Finalizados y Cancelados con distintas fechas de inicio
- **WHEN** un usuario abre la pestaña Proyectos
- **THEN** ve primero los Abiertos, luego los Finalizados y luego los Cancelados, y dentro de cada grupo por fecha de inicio ascendente

#### Scenario: Filtrar por estado

- **WHEN** el usuario marca "Cancelado" en el filtro de la columna Estado
- **THEN** la tabla muestra solo los proyectos Cancelados
