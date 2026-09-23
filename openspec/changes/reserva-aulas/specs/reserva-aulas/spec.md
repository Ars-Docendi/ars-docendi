## ADDED Requirements

### Requirement: Acceso restringido por permiso

La pantalla `/aulas` y sus endpoints SHALL estar disponibles únicamente para actores con el permiso
`aulas.solicitar` (Docente, incluye Jefe de Cátedra) o `aulas.aprobar` (Administrativo). Un actor sin
ninguno de los dos permisos MUST NOT ver la pantalla en la navegación ni poder acceder a sus rutas o
endpoints. La autorización del backend SHALL ser la autoridad final para cada acción, independientemente
de lo que oculte el frontend.

#### Scenario: Un docente sin el permiso ve la pantalla oculta

- **GIVEN** un usuario autenticado cuyo rol no tiene `aulas.solicitar` ni `aulas.aprobar`
- **WHEN** se renderiza la navegación
- **THEN** el enlace a "Reserva de Aulas" no aparece

#### Scenario: Acceso directo a la API sin permiso

- **GIVEN** un usuario autenticado sin `aulas.solicitar` ni `aulas.aprobar`
- **WHEN** llama a cualquier endpoint de `/api/aulas/solicitudes*`
- **THEN** la API responde 403 sin exponer datos de solicitudes ajenas

#### Scenario: Docente con `aulas.solicitar` ve solo su vista

- **GIVEN** un usuario con `aulas.solicitar` y sin `aulas.aprobar`
- **WHEN** abre `/aulas`
- **THEN** ve la vista "Mis solicitudes" y no ve la vista "Todas las solicitudes"

#### Scenario: Administrativo con `aulas.aprobar` ve solo su vista

- **GIVEN** un usuario con `aulas.aprobar` y sin `aulas.solicitar`
- **WHEN** abre `/aulas`
- **THEN** ve la vista "Todas las solicitudes" y no ve la vista "Mis solicitudes"

### Requirement: Crear solicitud de reserva de aula

Un Docente con `aulas.solicitar` SHALL poder crear una solicitud de reserva de aula indicando día,
horario desde, horario hasta, cantidad aproximada de alumnos, materia y comisión. La materia SHALL
elegirse entre las materias que el docente solicitante tiene asignadas (`identity.materias` vía sus
membresías en `identity.user_roles`) — NO es texto libre ni un catálogo abierto. El backend MUST
asignar el solicitante a partir del usuario autenticado (no un campo editable del formulario), MUST
inicializar el estado en `Pendiente`, MUST validar que el horario hasta sea posterior al horario desde,
que la cantidad de alumnos sea mayor a cero, y que la materia elegida esté entre las asignadas al
docente antes de persistir.

#### Scenario: Crear una solicitud válida

- **GIVEN** un Docente autenticado con `aulas.solicitar` y la materia "Análisis Matemático II" entre
  sus materias asignadas
- **WHEN** envía día, horario desde 08:00, horario hasta 10:00, 30 alumnos aproximados, esa materia y
  comisión "K3001"
- **THEN** la API crea la solicitud en estado `Pendiente`, asociada al Docente autenticado, sin aula
  asignada

#### Scenario: Horario hasta anterior o igual al horario desde

- **GIVEN** un Docente autenticado con `aulas.solicitar`
- **WHEN** envía horario desde 10:00 y horario hasta 09:00 (o igual a 10:00)
- **THEN** la API rechaza la solicitud sin persistir nada

#### Scenario: Cantidad de alumnos inválida

- **GIVEN** un Docente autenticado con `aulas.solicitar`
- **WHEN** envía una cantidad aproximada de alumnos igual o menor a cero
- **THEN** la API rechaza la solicitud sin persistir nada

#### Scenario: Materia que no pertenece al docente

- **GIVEN** un Docente autenticado con `aulas.solicitar` y una materia que NO está entre sus materias
  asignadas
- **WHEN** envía una solicitud con esa materia
- **THEN** la API rechaza la solicitud sin persistir nada, sin importar que la materia exista en el
  catálogo general

#### Scenario: Campo obligatorio faltante

- **GIVEN** un Docente autenticado con `aulas.solicitar`
- **WHEN** envía la solicitud sin día, materia o comisión
- **THEN** la API rechaza la solicitud sin persistir nada

### Requirement: Listar las materias propias del docente

El sistema SHALL exponer, para un Docente con `aulas.solicitar`, el catálogo de sus propias materias
asignadas — el mismo conjunto que acota la creación de solicitudes — para poblar el desplegable de
Materia del formulario "Nueva solicitud".

#### Scenario: Listar materias propias

- **GIVEN** un Docente con materias asignadas en `identity.user_roles`
- **WHEN** consulta su catálogo de materias propias
- **THEN** la API devuelve exactamente esas materias, con su código y nombre

#### Scenario: Docente sin materias asignadas

- **GIVEN** un Docente sin ninguna materia asignada
- **WHEN** consulta su catálogo de materias propias
- **THEN** la API devuelve una lista vacía, y el formulario de nueva solicitud impide guardar sin una

### Requirement: Listar mis solicitudes

La vista "Mis solicitudes" SHALL listar únicamente las solicitudes creadas por el Docente autenticado,
con día, horario desde, horario hasta, capacidad (cantidad aproximada de alumnos), código y nombre de
materia, comisión y estado (`Pendiente`, `Aprobada`, `Rechazada` o `Cancelada`). Cuando el estado sea
`Aprobada`, la fila SHALL mostrar además el aula asignada. Un Docente MUST NOT poder ver solicitudes de
otro Docente desde esta vista.

#### Scenario: Listar solicitudes propias con distintos estados

- **GIVEN** un Docente con una solicitud `Pendiente`, una `Aprobada` con aula "Lab 3" y una `Cancelada`
- **WHEN** abre "Mis solicitudes"
- **THEN** ve las tres filas con su estado, y solo la fila `Aprobada` muestra "Lab 3" como aula asignada

#### Scenario: Un docente no ve solicitudes ajenas

- **GIVEN** el Docente A con una solicitud y el Docente B con otra
- **WHEN** el Docente A abre "Mis solicitudes"
- **THEN** solo ve su propia solicitud, nunca la del Docente B

#### Scenario: Sin solicitudes propias

- **GIVEN** un Docente sin ninguna solicitud creada
- **WHEN** abre "Mis solicitudes"
- **THEN** ve el estado vacío de la lista, sin romper la pantalla

### Requirement: Cancelar solicitud propia

Un Docente con `aulas.solicitar` SHALL poder cancelar una solicitud propia solo mientras esté en estado
`Pendiente`. El backend MUST rechazar la cancelación de una solicitud ajena y MUST rechazar la
cancelación de una solicitud que ya esté `Aprobada` o `Cancelada`.

#### Scenario: Cancelar una solicitud pendiente propia

- **GIVEN** una solicitud `Pendiente` del Docente autenticado
- **WHEN** la cancela
- **THEN** la solicitud pasa a estado `Cancelada` y deja de poder aprobarse

#### Scenario: No se puede cancelar una solicitud ajena

- **GIVEN** una solicitud `Pendiente` del Docente B
- **WHEN** el Docente A intenta cancelarla
- **THEN** la API la rechaza sin modificar el estado

#### Scenario: No se puede cancelar una solicitud ya aprobada

- **GIVEN** una solicitud `Aprobada` del Docente autenticado, con aula asignada
- **WHEN** intenta cancelarla
- **THEN** la API rechaza la operación y la solicitud conserva su estado `Aprobada` y su aula asignada

#### Scenario: No se puede cancelar una solicitud ya cancelada

- **GIVEN** una solicitud ya `Cancelada`
- **WHEN** su Docente intenta cancelarla de nuevo
- **THEN** la API rechaza la operación sin duplicar ningún cambio de estado

### Requirement: Listar todas las solicitudes (Administrativo)

La vista "Todas las solicitudes" SHALL listar, para un Administrativo con `aulas.aprobar`, las
solicitudes de reserva de aula de todos los Docentes, con los mismos datos que "Mis solicitudes"
(incluido el código de materia) más la identificación del Docente solicitante.

#### Scenario: Administrativo ve solicitudes de todos los docentes

- **GIVEN** solicitudes creadas por el Docente A y por el Docente B
- **WHEN** un Administrativo abre "Todas las solicitudes"
- **THEN** ve ambas solicitudes, cada una identificando a su Docente solicitante

#### Scenario: Sin solicitudes en el sistema

- **GIVEN** que no existe ninguna solicitud creada todavía
- **WHEN** un Administrativo abre "Todas las solicitudes"
- **THEN** ve el estado vacío de la lista, sin romper la pantalla

### Requirement: Asignar y actualizar el aula de una solicitud

Un Administrativo con `aulas.aprobar` SHALL poder asignar un aula a una solicitud `Pendiente`,
indicando el identificador del aula; al asignar, el backend MUST transicionar la solicitud a estado
`Aprobada` y persistir el aula asignada. Sobre una solicitud ya `Aprobada`, el Administrativo SHALL
poder actualizar el aula asignada (sin volver a pasar por `Pendiente` ni afectar el estado) por si se
equivocó o cambian las condiciones del examen. El backend MUST rechazar la operación sobre una
solicitud `Cancelada` o `Rechazada`.

#### Scenario: Asignar aula a una solicitud pendiente

- **GIVEN** una solicitud `Pendiente`
- **WHEN** un Administrativo le asigna el aula "Aula 204"
- **THEN** la solicitud pasa a estado `Aprobada` con "Aula 204" como aula asignada, visible para el
  Docente en "Mis solicitudes"

#### Scenario: Actualizar el aula de una solicitud ya aprobada

- **GIVEN** una solicitud `Aprobada` con aula "Aula 204"
- **WHEN** un Administrativo le asigna el aula "Aula 305"
- **THEN** la solicitud permanece `Aprobada` y pasa a mostrar "Aula 305" como aula asignada

#### Scenario: No se puede asignar ni actualizar el aula de una solicitud cancelada

- **GIVEN** una solicitud `Cancelada`
- **WHEN** un Administrativo intenta asignarle o actualizarle un aula
- **THEN** la API rechaza la operación sin modificar el estado ni el aula

#### Scenario: No se puede asignar ni actualizar el aula de una solicitud rechazada

- **GIVEN** una solicitud `Rechazada`
- **WHEN** un Administrativo intenta asignarle o actualizarle un aula
- **THEN** la API rechaza la operación sin modificar el estado ni el aula

### Requirement: Rechazar solicitud pendiente (Administrativo)

Un Administrativo con `aulas.aprobar` SHALL poder rechazar una solicitud `Pendiente` indicando un
motivo obligatorio; al rechazar, el backend MUST transicionar la solicitud a estado `Rechazada` y
persistir el motivo. `Rechazada` SHALL ser un estado terminal: el backend MUST rechazar un nuevo
intento de rechazar, asignar aula o cancelar sobre una solicitud ya `Rechazada`. El backend MUST
rechazar la operación si no se envía un motivo, o si la solicitud no está `Pendiente`.

#### Scenario: Rechazar una solicitud pendiente con motivo

- **GIVEN** una solicitud `Pendiente`
- **WHEN** un Administrativo la rechaza con el motivo "El aula solicitada no está disponible en ese
  horario"
- **THEN** la solicitud pasa a estado `Rechazada` con ese motivo persistido, sin aula asignada

#### Scenario: Rechazar sin motivo

- **GIVEN** una solicitud `Pendiente`
- **WHEN** un Administrativo intenta rechazarla sin indicar un motivo
- **THEN** la API rechaza la operación sin modificar el estado

#### Scenario: No se puede rechazar una solicitud que no está pendiente

- **GIVEN** una solicitud `Aprobada`, o una ya `Cancelada`, o una ya `Rechazada`
- **WHEN** un Administrativo intenta rechazarla
- **THEN** la API rechaza la operación sin modificar el estado ni el motivo existente

### Requirement: Ver el motivo de una solicitud rechazada (Docente)

En "Mis solicitudes", un Docente SHALL poder ver el detalle completo de una solicitud propia en
estado `Rechazada` —incluido el motivo del rechazo— haciendo doble click sobre su fila. El popup de
detalle SHALL mostrar día, horario, código y nombre de materia, comisión, capacidad y el motivo de
rechazo. Las filas en otros estados dentro de esta vista MUST NOT abrir este popup.

#### Scenario: Ver el motivo de una solicitud rechazada

- **GIVEN** una solicitud propia `Rechazada` con motivo "El aula solicitada no está disponible en ese
  horario"
- **WHEN** el Docente hace doble click sobre esa fila en "Mis solicitudes"
- **THEN** se abre un popup de solo lectura con los datos de la solicitud y ese motivo visible

#### Scenario: Doble click sobre una fila que no está rechazada

- **GIVEN** una solicitud propia `Pendiente`, `Aprobada` o `Cancelada`
- **WHEN** el Docente hace doble click sobre esa fila en "Mis solicitudes"
- **THEN** no ocurre ninguna acción

### Requirement: Orden y filtro por encabezado en ambas listas

Las tablas de "Mis solicitudes" y "Todas las solicitudes" SHALL ofrecer un control de filtro y
ordenamiento accesible en cada encabezado de columna con datos (Día, Horario, Capacidad, Materia,
Comisión, Estado y, en "Todas las solicitudes", Docente), igual patrón que la tabla de Mis Pedidos de
Designaciones. La columna Cód. Materia SHALL mostrarse sin filtro ni ordenamiento propios (el filtro de
Materia ya cubre la búsqueda por materia); la columna Aula/Aula asignada y la columna Acciones MUST NOT
ofrecer filtro ni ordenamiento. Los criterios de texto SHALL buscar coincidencias parciales sin
distinguir mayúsculas ni tildes; los criterios categóricos (Estado) SHALL permitir seleccionar uno o
más valores. Los filtros de columnas distintas SHALL combinarse con lógica AND y las opciones múltiples
de una misma columna SHALL combinarse con lógica OR. Cada columna ordenable SHALL alternar entre
ascendente, descendente y sin orden manual.

#### Scenario: Filtrar por materia

- **GIVEN** la tabla contiene solicitudes de varias materias
- **WHEN** se escribe parte del nombre de una materia en el filtro del encabezado "Materia"
- **THEN** la tabla muestra solo las solicitudes cuya materia coincide, sin distinguir mayúsculas ni
  tildes

#### Scenario: Filtrar por estado

- **GIVEN** la tabla contiene solicitudes `Pendiente`, `Aprobada` y `Cancelada`
- **WHEN** se seleccionan "Pendiente" y "Aprobada" en el filtro de Estado
- **THEN** la tabla muestra únicamente esas solicitudes, ocultando las `Cancelada`

#### Scenario: Combinar filtros de columnas distintas

- **GIVEN** un filtro activo en Materia y otro en Estado
- **WHEN** ambos están aplicados a la vez
- **THEN** solo quedan visibles las solicitudes que cumplen ambos criterios

#### Scenario: Ordenar por día

- **GIVEN** solicitudes con distintos días
- **WHEN** se ordena el encabezado "Día" en forma ascendente
- **THEN** las filas visibles quedan ordenadas cronológicamente, respetando los filtros activos

#### Scenario: Limpiar un filtro sin perder los demás

- **GIVEN** filtros activos en Materia y Estado
- **WHEN** se limpia el filtro de Materia
- **THEN** el filtro de Estado continúa aplicado y solo se pierde el criterio de Materia
