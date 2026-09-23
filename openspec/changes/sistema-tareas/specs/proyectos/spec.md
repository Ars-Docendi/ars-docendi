## ADDED Requirements

### Requirement: Creación de Proyecto restringida por rol

El sistema SHALL restringir la creación de Proyectos a los roles Secretaría Académica y Decanato. El botón "Nuevo Proyecto" MUST estar oculto para cualquier otro rol, incluido Administrativos (que sí puede crear Tareas, pero no Proyectos), y el sistema MUST rechazar la creación aunque se invoque la acción sin pasar por el botón.

#### Scenario: Botón "Nuevo Proyecto" visible para Decanato

- **WHEN** un usuario con rol Decanato abre la pantalla inicial de Tareas
- **THEN** ve el botón "Nuevo Proyecto"

#### Scenario: Botón "Nuevo Proyecto" oculto para Administrativos

- **WHEN** un usuario con rol Administrativos abre la pantalla inicial de Tareas
- **THEN** ve el botón "Nueva Tarea" pero no ve el botón "Nuevo Proyecto"

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

### Requirement: Pantalla de Detalle de Proyecto

El sistema SHALL ofrecer una pantalla de Detalle por Proyecto (`/tareas/proyectos/:id`), accesible desde el título de su cuadro en la pantalla inicial, que MUST mostrar: nombre, descripción, estado, Fecha de Inicio, Fecha de Fin, Responsable, y la tabla de tareas asociadas a ese Proyecto (mismo modelo de filtros, orden y colores que el resto de las tablas de tareas).

#### Scenario: Apertura del Detalle desde el cuadro del Proyecto

- **WHEN** un usuario hace click en el título del cuadro de un Proyecto en la pantalla inicial
- **THEN** navega a `/tareas/proyectos/:id` y ve todos los datos de ese Proyecto junto con sus tareas

#### Scenario: Proyecto inexistente

- **WHEN** un usuario navega a `/tareas/proyectos/:id` con un id que no existe
- **THEN** ve un mensaje de error indicando que el Proyecto no se encontró, con un enlace para volver a Tareas

### Requirement: Acceso manual a Proyectos Finalizados o Cancelados

Como la pantalla inicial de Tareas solo genera un cuadro por cada Proyecto en estado Abierto, el sistema SHALL ofrecer una pantalla de listado con **todos** los Proyectos (`/tareas/proyectos`), sin importar su Estado, accesible desde un enlace en la pantalla inicial. Desde ese listado SHALL poder navegarse al Detalle de cualquier Proyecto, incluidos los Finalizados y Cancelados.

#### Scenario: Acceder a un Proyecto Finalizado desde el listado completo

- **GIVEN** un Proyecto en estado Finalizado, que ya no tiene cuadro en la pantalla inicial
- **WHEN** un usuario abre el listado completo de Proyectos y hace click en él
- **THEN** navega a su Detalle y ve sus datos completos, incluidas sus tareas

#### Scenario: El listado completo incluye Proyectos de cualquier Estado

- **GIVEN** Proyectos Abiertos, Finalizados y Cancelados
- **WHEN** un usuario abre `/tareas/proyectos`
- **THEN** ve los tres, cada uno con su Estado indicado
