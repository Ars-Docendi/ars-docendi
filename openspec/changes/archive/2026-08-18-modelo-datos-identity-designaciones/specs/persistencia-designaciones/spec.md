## ADDED Requirements

### Requirement: Designación vigente como entidad de estado

El sistema SHALL persistir en `designaciones.designaciones` el estado vigente de cada docente: la tupla `(persona, materia, cargo, horas)` con `vigente_desde` y `vigente_hasta` nullable. Una designación con `vigente_hasta` en NULL SHALL considerarse vigente. El sistema MUST impedir que una misma persona tenga dos designaciones vigentes simultáneas sobre la misma materia. Esta tabla SHALL ser la fuente de los datos actuales que el formulario de pedido muestra de solo lectura (cargo actual, dedicación actual, materias y horas vigentes).

#### Scenario: Consulta del estado vigente de un docente

- **GIVEN** una persona con designaciones registradas, algunas cerradas y otras vigentes
- **WHEN** el sistema consulta su estado actual
- **THEN** MUST devolver únicamente las designaciones con `vigente_hasta` en NULL

#### Scenario: Dos designaciones vigentes sobre la misma materia son rechazadas

- **GIVEN** una persona con una designación vigente sobre una materia
- **WHEN** se intenta abrir una segunda designación vigente para esa misma persona y materia
- **THEN** la base de datos MUST rechazar la operación

#### Scenario: Una designación cerrada no bloquea una nueva

- **GIVEN** una persona cuya designación sobre una materia fue cerrada con `vigente_hasta`
- **WHEN** se abre una designación nueva para esa misma persona y materia
- **THEN** la operación MUST aceptarse

### Requirement: Origen trazable de cada designación

El sistema SHALL registrar en `designaciones.designaciones` la columna nullable `origen_pedido_id`, que referencia al pedido cuya aprobación produjo la designación. Un valor NULL SHALL significar que la designación fue cargada directamente por la superficie de administración, sin pasar por el circuito de aprobación. El sistema MUST permitir distinguir ambos orígenes en cualquier consulta de trazabilidad.

#### Scenario: Designación producida por el circuito de aprobación

- **GIVEN** un pedido que recorrió el circuito y quedó aprobado
- **WHEN** el sistema materializa su resultado
- **THEN** la designación resultante MUST llevar `origen_pedido_id` apuntando a ese pedido

#### Scenario: Designación cargada a mano por administración

- **GIVEN** un operador de Secretaría cargando una asignación desde la superficie de administración de docentes
- **WHEN** guarda la asignación
- **THEN** la designación se persiste con `origen_pedido_id` en NULL

#### Scenario: Trazabilidad de una designación hasta su trámite

- **GIVEN** una designación vigente con `origen_pedido_id` cargado
- **WHEN** se consulta su origen
- **THEN** el sistema MUST poder recuperar el pedido, su historial de trámite completo y sus eventos en `audit.change_log`

### Requirement: El pedido cubre exactamente una materia

El sistema SHALL persistir cada pedido de designación con una única `materia_id` —la cátedra sobre la que el Jefe de Cátedra opera— y sus `horas` como columnas del propio pedido. La carrera del pedido SHALL derivarse de `identity.materias.carrera_id` y MUST NOT almacenarse denormalizada. El sistema MUST validar que el actor que crea el pedido tenga el rol de Jefe de Cátedra vigente sobre esa materia.

#### Scenario: La carrera del pedido se deriva de su materia

- **GIVEN** un pedido persistido sobre una materia
- **WHEN** el Coordinador consulta su tablero de revisión
- **THEN** el pedido MUST aparecer bajo la carrera a la que pertenece esa materia, resolviendo un único Coordinador competente

#### Scenario: Un Jefe de Cátedra no puede cargar un pedido sobre una cátedra ajena

- **GIVEN** un Jefe de Cátedra sin rol vigente sobre una materia dada
- **WHEN** intenta crear un pedido sobre esa materia
- **THEN** el sistema MUST denegar la creación

#### Scenario: Un rol revocado deja de habilitar la carga

- **GIVEN** un usuario cuyo rol de Jefe de Cátedra sobre una materia fue revocado (`deleted_at` no nulo)
- **WHEN** intenta crear un pedido sobre esa materia
- **THEN** el sistema MUST denegar la creación

### Requirement: Un pedido por docente por período, con la base de datos como autoridad [BR-designaciones-001]

El sistema SHALL impedir que exista más de un pedido no terminal para la misma persona dentro del mismo período, **sin importar la cátedra**. La restricción MUST implementarse como índice único parcial en PostgreSQL sobre `(periodo_id, persona_id)` excluyendo los estados `rechazado` y `cancelado`, y SHALL validarse además en el backend antes de intentar la escritura, para producir el mensaje de error correspondiente. Ante una violación del índice, el backend MUST traducirla al mismo mensaje en lugar de propagar un error de base de datos.

#### Scenario: Segundo pedido para el mismo docente en el mismo período [BR-designaciones-001]

- **GIVEN** una persona que ya tiene un pedido no terminal en el período abierto
- **WHEN** se intenta crear un segundo pedido para esa persona en ese período
- **THEN** el sistema MUST rechazar la creación e indicar que ya existe un pedido para ese docente en el período

#### Scenario: El bloqueo alcanza a otra cátedra [BR-designaciones-001]

- **GIVEN** una persona con un pedido no terminal cargado por el Jefe de Cátedra de una materia
- **WHEN** el Jefe de Cátedra de otra materia intenta cargar un pedido para esa misma persona en el mismo período
- **THEN** el sistema MUST rechazar la creación, e informarlo **sin exponer datos del pedido bloqueante**, que pertenece a una cátedra ajena al actor

#### Scenario: Dos escrituras concurrentes [BR-designaciones-001]

- **GIVEN** dos requests simultáneos creando un pedido para la misma persona y período
- **WHEN** ambos superan la validación del backend
- **THEN** el índice único MUST hacer fallar exactamente uno, y el backend MUST traducir esa falla al mensaje de duplicado

#### Scenario: Tras un rechazo se puede volver a presentar [BR-designaciones-001]

- **GIVEN** una persona cuyo único pedido del período quedó en estado `rechazado`
- **WHEN** se crea un pedido nuevo para esa persona en el mismo período
- **THEN** la operación MUST aceptarse, porque el índice excluye los estados terminales

### Requirement: Snapshot inmutable de los datos vigentes al enviar

El sistema SHALL congelar en el pedido, al momento de enviarlo a revisión, los datos vigentes del docente que el trámite fotografía: cargo actual, dedicación actual, materia y horas vigentes. Ese snapshot MUST NOT recalcularse al consultarse después. El detalle del pedido SHALL mostrar el snapshot, no el estado vigente al momento de la consulta.

#### Scenario: El trámite conserva su verdad histórica

- **GIVEN** un pedido enviado a revisión cuando el docente tenía un cargo dado
- **WHEN** el cargo vigente del docente cambia mientras el pedido recorre la cadena de aprobación
- **THEN** el detalle del pedido MUST seguir mostrando el cargo que el docente tenía al enviarse, no el actual

#### Scenario: El snapshot se toma al enviar, no al crear

- **GIVEN** un pedido en estado `borrador` que todavía no fue enviado
- **WHEN** los datos vigentes del docente cambian
- **THEN** el pedido MUST reflejar los datos actualizados, porque el snapshot todavía no fue tomado

### Requirement: Historial del trámite como dato de dominio

El sistema SHALL persistir el historial de cada pedido en `designaciones.pedido_historial`, con la acción, el rol con el que actuó el actor, la etapa del pedido al momento del evento, el comentario o justificativo y la fecha. El historial MUST NOT derivarse de `audit.change_log`: el rol con el que se actuó no es derivable de un usuario que puede tener varios, y el comentario es dato de negocio exigido por BR-designaciones-005. La tabla SHALL estar además auditada mediante `audit.attach`, y sus filas MUST NOT purgarse.

#### Scenario: Cada transición deja un evento persistido

- **WHEN** se aplica una transición válida sobre un pedido (enviar, aceptar, rechazar, devolver, reenviar, priorizar)
- **THEN** el sistema MUST persistir un evento en `pedido_historial` con su acción, rol, etapa y fecha

#### Scenario: El rol con el que se actuó queda registrado explícitamente

- **GIVEN** un actor que tiene más de un rol asignado en el sistema
- **WHEN** ejecuta una acción de revisión sobre un pedido
- **THEN** el evento del historial MUST registrar el rol concreto con el que actuó, sin ambigüedad

#### Scenario: El justificativo queda persistido junto al evento [BR-designaciones-005]

- **WHEN** un revisor rechaza un pedido con su justificativo, o lo devuelve con su comentario
- **THEN** ese texto MUST persistirse en el evento correspondiente de `pedido_historial`

#### Scenario: Una modificación manual del historial deja rastro

- **WHEN** se modifica o elimina directamente una fila de `pedido_historial`
- **THEN** `audit.change_log` MUST registrar el evento

### Requirement: Aprobación de un pedido materializada sobre las designaciones vigentes

El sistema SHALL traducir la aprobación de un pedido a escrituras sobre `designaciones.designaciones`, dentro de una única transacción, según su novedad: un **Alta** abre una designación nueva; una **Baja** cierra la designación vigente fijando `vigente_hasta`; un **Cambio de cargo o dedicación** cierra la vigente y abre una nueva con los valores solicitados; **Sin novedad** MUST no alterar el estado vigente. Toda designación así producida MUST llevar `origen_pedido_id` apuntando al pedido aprobado.

#### Scenario: Aprobación de un Alta

- **GIVEN** un pedido de novedad "Alta" que completó el circuito de aprobación
- **WHEN** el sistema materializa el resultado
- **THEN** MUST abrirse una designación vigente para esa persona y materia, con `origen_pedido_id` al pedido

#### Scenario: Aprobación de una Baja

- **GIVEN** un pedido de novedad "Baja" que completó el circuito
- **WHEN** el sistema materializa el resultado
- **THEN** la designación vigente de esa persona sobre esa materia MUST quedar cerrada con su `vigente_hasta`

#### Scenario: Aprobación de un Cambio

- **GIVEN** un pedido de novedad "Cambio de cargo o dedicación" que completó el circuito
- **WHEN** el sistema materializa el resultado
- **THEN** la designación vigente MUST cerrarse y MUST abrirse una nueva con el cargo y la dedicación solicitados, ambas en la misma transacción

#### Scenario: Sin novedad no altera el estado vigente

- **GIVEN** un pedido de novedad "Sin novedad" que completó el circuito
- **WHEN** el sistema materializa el resultado
- **THEN** las designaciones vigentes de esa persona MUST quedar sin cambios

#### Scenario: Un fallo parcial no deja estado inconsistente

- **GIVEN** un pedido de "Cambio" en proceso de materialización
- **WHEN** la apertura de la designación nueva falla
- **THEN** el cierre de la anterior MUST revertirse, dejando el estado vigente intacto

### Requirement: Período con una única ventana activa

El sistema SHALL persistir los períodos de designación en `designaciones.periodos` con su ventana de carga y su rango de impacto, y MUST garantizar en la base de datos que exista a lo sumo un período activo a la vez.

#### Scenario: Activar un segundo período es rechazado

- **GIVEN** un período ya marcado como activo
- **WHEN** se intenta marcar otro período como activo sin desactivar el primero
- **THEN** la base de datos MUST rechazar la operación

#### Scenario: Los pedidos referencian su período

- **WHEN** se crea un pedido
- **THEN** MUST quedar asociado al período bajo el cual se cargó, y esa asociación MUST no cambiar después

### Requirement: Catálogo único de cargos

El sistema SHALL persistir los cargos docentes en un catálogo único `designaciones.cargos`, con `code`, `nombre`, `orden` e `is_active`. Ese catálogo SHALL ser la única fuente de valores de cargo para pedidos y designaciones, reemplazando los vocabularios divergentes que hoy conviven en el frontend. La columna `orden` SHALL registrar la jerarquía institucional, sin que este change la use para restringir selecciones.

#### Scenario: Un cargo fuera del catálogo es rechazado

- **WHEN** se intenta persistir un pedido o una designación con un cargo que no existe en el catálogo
- **THEN** la base de datos MUST rechazar la operación

#### Scenario: Un cargo dado de baja no se ofrece pero no rompe lo existente

- **GIVEN** un cargo marcado con `is_active = FALSE` y designaciones históricas que lo referencian
- **WHEN** un operador abre el selector de cargos
- **THEN** ese cargo MUST no ofrecerse como opción nueva, y las designaciones que lo referencian MUST seguir resolviéndose correctamente

### Requirement: Toda tabla de negocio auditada, sin denormalizar metadata

Toda tabla de negocio creada por este change SHALL registrar sus cambios en `audit.change_log` mediante `audit.attach`. Las tablas MUST NOT declarar columnas `created_by`, `updated_at`, `updated_by` ni `deleted_by`: esa metadata se consulta con `audit.row_history(...)`. `created_at` SHALL ser la única denormalización admitida. Una tabla SHALL declarar `deleted_at` únicamente cuando el soft-delete sea una decisión de dominio, y en ese caso sus índices de unicidad MUST ser parciales sobre `deleted_at IS NULL`.

#### Scenario: Una escritura queda registrada con su autor

- **GIVEN** un usuario autenticado ejecutando una operación de escritura sobre una tabla de negocio
- **WHEN** la operación se completa
- **THEN** `audit.change_log` MUST registrar el evento con `changed_by` correspondiente a ese usuario

#### Scenario: Ninguna tabla denormaliza metadata de auditoría

- **WHEN** se inspecciona el schema `designaciones`
- **THEN** MUST no existir ninguna columna `created_by`, `updated_at`, `updated_by` ni `deleted_by`

#### Scenario: La historia de una fila se recupera desde el log

- **GIVEN** una fila que fue creada y luego modificada
- **WHEN** se consulta `audit.row_history` para esa fila
- **THEN** MUST devolver su fecha y autor de creación y los de su última modificación
