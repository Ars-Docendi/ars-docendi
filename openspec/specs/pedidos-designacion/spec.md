# pedidos-designacion

## Purpose

Gestionar por API el ciclo de vida de un pedido docente sobre una única materia, desde el borrador hasta su envío y eventual corrección, con autorización por rol y ámbito.

## Requirements

### Requirement: Persistencia remota

La creación, consulta, edición, eliminación y transición de pedidos SHALL usar la API de Designaciones. El backend MUST asignar el número, validar reglas y ámbito, y persistir cada cambio con su historial; el frontend MUST NOT mantener un store alternativo.

#### Scenario: Crear un borrador

- **GIVEN** un Jefe de Cátedra autorizado y un período activo
- **WHEN** crea un pedido válido para una materia a su cargo
- **THEN** la API devuelve un borrador con ID, número y evento `crear`

#### Scenario: Cliente envía una operación inválida

- **WHEN** cualquier cliente intenta una edición o transición no autorizada
- **THEN** el backend la rechaza sin modificar el pedido ni agregar historial

### Requirement: Catálogos canónicos

Personas, materias, cargos, dedicaciones y períodos SHALL provenir de la API. Las mutaciones MUST enviar sus UUID canónicos. Para un Alta, el formulario SHALL ofrecer como materias únicamente las materias activas donde el actor tenga una membresía vigente de Jefe de Cátedra. Para una Baja o un Cambio de cargo o dedicación, el formulario SHALL ofrecer únicamente las materias activas que estén simultáneamente dentro del ámbito del actor y entre las designaciones vigentes del docente seleccionado. Las dedicaciones seleccionables SHALL ser Categoría 1 a 6, de elección libre incluso al mantener o reducir la dedicación actual; la API MUST validar todas las referencias canónicas.

#### Scenario: Abrir formulario

- **WHEN** un actor abre el alta o edición
- **THEN** ve el período activo y los catálogos persistidos que puede utilizar, con las materias contextualizadas por la novedad y el docente cuando corresponda

#### Scenario: Abrir formulario para un Jefe con varias materias

- **GIVEN** un Jefe de Cátedra con membresías vigentes en Materia A y Materia B
- **WHEN** abre un nuevo pedido
- **THEN** no se muestra un selector de materia separado en la parte superior y la materia se solicita dentro del flujo de novedad y docente

#### Scenario: Alta ofrece todas las materias propias del Jefe

- **GIVEN** un Jefe de Cátedra con materias A y B
- **WHEN** elige la novedad "Alta"
- **THEN** puede elegir A o B como materia del pedido, después de completar los datos del docente nuevo

#### Scenario: Baja o Cambio limita las materias al docente seleccionado

- **GIVEN** el Jefe tiene A y B, y el docente seleccionado tiene designaciones vigentes en B y C
- **WHEN** selecciona "Baja" o "Cambio de cargo o dedicación"
- **THEN** la materia del pedido sólo ofrece B

#### Scenario: El docente no tiene una materia compatible

- **GIVEN** el Jefe tiene A y el docente seleccionado sólo tiene una designación vigente en B
- **WHEN** se selecciona "Baja" o "Cambio de cargo o dedicación"
- **THEN** el formulario no permite guardar y explica que el docente no tiene una designación en una materia a cargo del actor

#### Scenario: El cliente envía una materia fuera del catálogo contextual

- **GIVEN** un pedido cuyo `materiaId` no pertenece al conjunto permitido para su novedad, docente y actor
- **WHEN** intenta crearlo o editarlo
- **THEN** la API lo rechaza sin persistir cambios ni historial

#### Scenario: Cambio de dedicación libre

- **GIVEN** un docente con Categoría 2
- **WHEN** se carga un Cambio
- **THEN** el selector MUST ofrecer las seis categorías y backend MUST aceptar 1, 2 o 6 con los demás datos válidos

#### Scenario: Categoría ajena al catálogo

- **GIVEN** una nueva solicitud
- **WHEN** un cliente envía Categoría 0, texto libre o un UUID inexistente
- **THEN** backend MUST rechazar la selección sin persistir el pedido

### Requirement: Un pedido por persona y período [BR-designaciones-001]

No SHALL existir más de un pedido no rechazado ni cancelado para una persona en el mismo período, aunque pertenezca a otra materia. La base MUST imponer la unicidad y la API MUST traducir el conflicto sin revelar el pedido bloqueante.

#### Scenario: Segundo pedido

- **GIVEN** una persona con un pedido vivo en el período
- **WHEN** se intenta crear otro
- **THEN** la API responde conflicto sin exponer materia, contenido ni autor del primero

### Requirement: Reglas por novedad

El backend MUST aplicar las reglas por novedad al crear y editar. `Alta` SHALL recibir los datos de una persona nueva —DNI, nombre y apellido— y exigir CV, DNI frente y DNI dorso [BR-designaciones-002]. `Baja` exige tipo, adjunto justificativo y persona con legajo [BR-designaciones-003, BR-designaciones-018]. `Cambio de cargo o dedicación` exige justificación y persona con legajo [BR-designaciones-004, BR-designaciones-018]. Las únicas novedades admitidas para nuevos pedidos, ediciones y avance del circuito SHALL ser Alta, Baja y Cambio de cargo o dedicación. El formulario MUST exigir una elección explícita. `Sin novedad` MUST NOT ofrecerse ni aceptarse como nueva solicitud; los registros legados SHALL conservarse para lectura histórica.

#### Scenario: Alta con datos nuevos

- **WHEN** el Jefe elige "Alta"
- **THEN** el formulario muestra campos de DNI y apellido/nombre, no un selector de personas ya registradas

#### Scenario: Alta incompleta

- **WHEN** se guarda un Alta sin DNI, nombre, apellido o alguno de sus tres adjuntos
- **THEN** el backend rechaza el pedido

#### Scenario: Baja o Cambio sin legajo

- **WHEN** la persona no tiene legajo
- **THEN** el backend rechaza una Baja o Cambio, pero permite un Alta con datos y documentación completos

#### Scenario: Solicitud Sin novedad rechazada

- **GIVEN** un cliente que omite el formulario
- **WHEN** intenta crear, editar, enviar, reenviar o aprobar un pedido Sin novedad
- **THEN** la API MUST rechazar la operación sin cambiar datos ni historial

#### Scenario: Lectura de pedido legado

- **GIVEN** un pedido Sin novedad anterior al cambio
- **WHEN** se consulta su detalle autorizado
- **THEN** MUST conservar su número, texto original e historial sin transformarse en otra novedad

### Requirement: Una materia por pedido

Cada pedido SHALL referir exactamente una materia y su carga horaria. En un Alta, la materia se elegirá entre las materias a cargo del Jefe de Cátedra. En una Baja o un Cambio de cargo o dedicación, la materia se elegirá entre las designaciones vigentes del docente seleccionado que estén a cargo del actor. La carrera se deriva de la materia para determinar al Coordinador competente.

#### Scenario: Enrutamiento

- **GIVEN** un pedido de una materia perteneciente a una carrera
- **WHEN** se envía
- **THEN** queda en revisión del Coordinador de esa carrera

#### Scenario: Una única materia compatible se autoselecciona

- **GIVEN** un docente seleccionado con exactamente una materia compatible con el ámbito del actor
- **WHEN** se carga una Baja o un Cambio
- **THEN** esa materia queda seleccionada automáticamente y sus datos vigentes se muestran como solo lectura

#### Scenario: Varias materias compatibles requieren elección

- **GIVEN** un docente seleccionado con dos o más materias compatibles con el ámbito del actor
- **WHEN** se carga una Baja o un Cambio
- **THEN** el formulario exige elegir una de esas materias antes de guardar

#### Scenario: Alta se enruta por la materia elegida

- **GIVEN** un Jefe de Cátedra con más de una materia propia
- **WHEN** crea un Alta y elige una materia
- **THEN** el pedido queda persistido y se enruta según la carrera de esa materia

### Requirement: Alta de persona sin cuenta de autenticación

Un Alta SHALL poder registrar los datos de una persona que todavía no tiene una fila en `identity.personas`, o reutilizar la referencia canónica ya resuelta por el backend cuando corresponda. La operación MUST no exigir UPN, cuenta Azure AD ni legajo. El pedido SHALL conservar la referencia canónica de la persona y MUST rechazar un documento duplicado sin crear un pedido parcial.

#### Scenario: Alta de persona nueva

- **GIVEN** un DNI que no existe en `identity.personas`
- **WHEN** el Jefe guarda o envía un Alta válida
- **THEN** el sistema registra la persona sin legajo ni usuario Azure AD y persiste el pedido con su `personaId` canónico

#### Scenario: Documento ya registrado

- **GIVEN** un DNI que ya existe en `identity.personas`
- **WHEN** se intenta registrar una nueva persona para un Alta
- **THEN** la API rechaza el conflicto de identidad sin duplicar la persona ni persistir un pedido incompleto

#### Scenario: Primer login posterior al Alta

- **GIVEN** una persona registrada por un Alta sin usuario Azure AD
- **WHEN** se autentica por primera vez con el documento correspondiente
- **THEN** el sistema crea o vincula `identity.users` a la persona existente sin crear una segunda persona

### Requirement: Edición y eliminación

Un borrador SHALL ser editable y eliminable sólo por un Jefe de Cátedra dentro de su ámbito. Un pedido devuelto SHALL ser editable por su propietario actual —Jefe, Coordinador o Secretaría— dentro de su ámbito, pero MUST NOT ser eliminable [BR-designaciones-008, BR-designaciones-009].

#### Scenario: Corregir una devolución

- **GIVEN** un pedido devuelto a Coordinación o Secretaría
- **WHEN** el propietario edita datos válidos y lo reenvía
- **THEN** la API acepta ambas operaciones y conserva el snapshot inicial

#### Scenario: Borrar un borrador

- **WHEN** su Jefe de Cátedra elimina un borrador dentro de su ámbito
- **THEN** el pedido deja de existir

### Requirement: Envío y reenvío idempotentes

Enviar y reenviar SHALL exigir `Idempotency-Key`. Repetir la misma solicitud con la misma clave MUST devolver la respuesta confirmada sin duplicar la transición. Reusar la clave con otra solicitud MUST responder conflicto.

#### Scenario: Reintento de red

- **WHEN** el cliente repite un envío confirmado con la misma clave
- **THEN** existe un único evento `enviar`

### Requirement: Snapshot histórico

Al enviar por primera vez, el backend SHALL congelar cargo, dedicación, materia y horas vigentes. El detalle SHALL mostrar ese snapshot en los valores anteriores aunque cambien o se desactiven los catálogos actuales. Las horas solicitadas de materia, investigación y externas MUST conservarse separadas y provenir del pedido tanto en el detalle como al editar. Editar y reenviar MUST NOT recalcularlo.

#### Scenario: Cambia la designación vigente

- **GIVEN** un pedido ya enviado
- **WHEN** cambian la designación o la materia del catálogo
- **THEN** su detalle conserva los datos fotografiados al enviar

#### Scenario: Alta con snapshot vacío conserva las horas pedidas

- **GIVEN** un Alta con horas solicitadas 12, 3 y 2 y sin designación anterior
- **WHEN** se envía y se vuelve a consultar
- **THEN** MUST mostrar 12, 3 y 2 como solicitud y ausencia de valores anteriores

#### Scenario: Editar devuelto y reenviar conserva la corrección

- **GIVEN** un devuelto con snapshot 8, 2 y 1 y solicitud 12, 4 y 3
- **WHEN** su propietario cambia la solicitud a 16, 5 y 2, guarda, recarga y reenvía
- **THEN** MUST conservar 16, 5 y 2 como solicitud y 8, 2 y 1 como snapshot

#### Scenario: Guardar borrador conserva las tres cargas

- **GIVEN** un borrador válido con horas de materia, investigación y externas
- **WHEN** se modifican, guardan y recargan
- **THEN** MUST recuperarse exactamente los valores guardados

### Requirement: Lista Mis pedidos

`/designaciones/mis-pedidos` SHALL listar los pedidos visibles del período, con número, persona, legajo, materia, novedad, estado y prioridad. La fila y la acción Ver SHALL abrir el detalle. Editar y Eliminar SHALL mostrarse sólo cuando la API incluya esas acciones.

#### Scenario: Filtros

- **WHEN** se filtra por docente, número, legajo, tipo o estado
- **THEN** la tabla conserva sólo las coincidencias sin distinguir mayúsculas ni acentos

#### Scenario: Estados remotos

- **WHEN** la consulta carga, falla, queda vacía o responde con datos
- **THEN** la pantalla representa explícitamente Loading, Error, Empty o Success

### Requirement: Validación anticipada y autoridad

El frontend SHALL anticipar las mismas reglas para dar feedback inmediato, pero el backend MUST ser la autoridad. Guardar, enviar y reenviar MUST validar antes de llamar a la API.

#### Scenario: Guardado o envío desde el formulario

- **WHEN** el usuario elige Guardar, Guardar y enviar o Guardar y reenviar
- **THEN** el frontend muestra errores de campos requeridos antes de ejecutar las mutaciones

### Requirement: Identificación visible por número

Toda referencia visible a un pedido SHALL usar su número de negocio. El UUID MUST quedar reservado a rutas y referencias técnicas y MUST NOT aparecer como etiqueta, título, mensaje, tooltip o contenido de descarga del pedido.

#### Scenario: Número distinto del UUID

- **GIVEN** un pedido con número 2026-0123 y UUID técnico diferente
- **WHEN** se abre su detalle, edición o modal de acción
- **THEN** toda identificación visible MUST usar 2026-0123 y no el UUID

### Requirement: Filtros y ordenamiento por encabezado en Mis pedidos

La tabla de `/designaciones/mis-pedidos` SHALL ofrecer un control de filtro accesible en cada encabezado de datos: N°, Docente, Legajo, Cátedra, Tipo, Enviado y Estado. La columna Acciones MUST NOT ofrecer filtro ni ordenamiento. Los criterios de texto SHALL buscar coincidencias parciales sin distinguir mayúsculas ni tildes; los criterios categóricos SHALL permitir seleccionar uno o más valores. Los filtros de columnas distintas SHALL combinarse con lógica AND y las opciones múltiples de una misma columna SHALL combinarse con lógica OR.

La tabla SHALL permitir ordenar N°, Docente, Legajo, Cátedra, Tipo, Enviado y Estado desde sus encabezados. Cada orden SHALL alternar entre ascendente, descendente y sin orden manual, y el orden SHALL aplicarse después de filtrar.

#### Scenario: Filtrar por docente desde el encabezado

- **GIVEN** la tabla contiene pedidos de varios docentes
- **WHEN** el operador abre el filtro del encabezado "Docente" y escribe una parte del nombre sin tilde
- **THEN** la tabla muestra sólo los pedidos cuyo docente coincide sin distinguir mayúsculas ni tildes

#### Scenario: Filtrar por tipo y estado

- **GIVEN** la tabla contiene pedidos de distintos tipos y estados
- **WHEN** el operador selecciona "Alta" en Tipo y "En revisión" en Estado
- **THEN** la tabla muestra únicamente pedidos que cumplen ambos criterios

#### Scenario: Limpiar un filtro sin perder los demás

- **GIVEN** existen filtros activos en Docente y Estado
- **WHEN** el operador activa "Limpiar filtro" en Docente
- **THEN** se elimina sólo el criterio de Docente y el filtro de Estado continúa aplicado

#### Scenario: Ordenar después de filtrar

- **GIVEN** un filtro activo dejó visibles una parte de los pedidos
- **WHEN** el operador ordena el encabezado "Enviado" en forma ascendente
- **THEN** sólo las filas visibles se ordenan cronológicamente y no reaparecen pedidos excluidos

#### Scenario: Aplicar filtros antes de paginar

- **GIVEN** la lista supera el tamaño de página
- **WHEN** el operador aplica un filtro desde un encabezado
- **THEN** la paginación se recalcula sobre el conjunto filtrado y conserva la navegación a detalle y las acciones permitidas

#### Scenario: Operación accesible del encabezado

- **WHEN** el operador enfoca el control de filtro de un encabezado y presiona Enter o Espacio
- **THEN** se abre el menú sin activar la navegación de la fila, y el encabezado comunica el orden mediante su estado accesible cuando corresponde
