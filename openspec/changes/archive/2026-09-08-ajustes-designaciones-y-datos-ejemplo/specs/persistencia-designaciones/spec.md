## MODIFIED Requirements

### Requirement: Aprobación de un pedido materializada sobre las designaciones vigentes

El sistema SHALL traducir la aprobación de un pedido a escrituras sobre `designaciones.designaciones`, dentro de una única transacción, según su novedad: un **Alta** abre una designación nueva; una **Baja** cierra la designación vigente fijando `vigente_hasta`; un **Cambio de cargo o dedicación** cierra la vigente y abre una nueva con los valores solicitados. La ausencia de novedad aprobada MUST conservar las designaciones vigentes sin crear pedido alguno. Toda designación así producida MUST llevar `origen_pedido_id` apuntando al pedido aprobado.

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
- **THEN** la designación vigente MUST cerrarse y MUST abrirse una nueva con el cargo, la dedicación y las tres cargas horarias solicitadas, ambas en la misma transacción

#### Scenario: Sin novedad no altera el estado vigente

- **GIVEN** una persona y materia con designación vigente y sin novedad aprobada
- **WHEN** se consulta el resultado para el lote del período
- **THEN** MUST conservar sus valores sin crear un pedido Sin novedad

#### Scenario: Un fallo parcial no deja estado inconsistente

- **GIVEN** un pedido de "Cambio" en proceso de materialización
- **WHEN** la apertura de la designación nueva falla
- **THEN** el cierre de la anterior MUST revertirse, dejando el estado vigente intacto

### Requirement: Snapshot inmutable de los datos vigentes al enviar

El sistema SHALL congelar en el pedido, al momento de enviarlo a revisión, los datos vigentes del docente que el trámite fotografía: cargo actual, dedicación actual, materia y horas vigentes. Ese snapshot MUST NOT recalcularse al consultarse después. El panel de valores anteriores del detalle SHALL mostrar el snapshot, no el estado vigente al momento de la consulta. Las tres horas solicitadas MUST consultarse por separado desde el pedido; investigación y externas del snapshot MUST provenir del estado vigente al primer envío, sin copiar la solicitud. Datos históricos desconocidos MUST permanecer ausentes, sin inventar ceros.

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

El historial consultado y mostrado SHALL estar ordenado por instante ascendente con desempate estable. Cada evento SHALL mostrar fecha y hora en zona America/Argentina/Buenos_Aires, con formato dd/MM/yyyy HH:mm; el orden MUST basarse en el instante original.

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

#### Scenario: Eventos del mismo día

- **GIVEN** eventos de las 15:30 UTC y 12:15 UTC recibidos fuera de orden
- **WHEN** se muestra el historial
- **THEN** MUST aparecer primero 09:15 y luego 12:30 con sus fechas locales y autores

## ADDED Requirements

### Requirement: Catálogo persistido de dedicaciones

El sistema SHALL mantener en el esquema designaciones un catálogo con las seis categorías seleccionables 1 a 6, con identificador canónico, código único, nombre y estado activo. Pedidos y designaciones SHALL referenciarlo con integridad en base; las nuevas selecciones MUST pertenecer a ese catálogo y estar activas. La administración de docentes SHALL respetar el mismo catálogo.

#### Scenario: Integridad de una nueva selección

- **GIVEN** un pedido o designación nuevo
- **WHEN** se intenta escribir una referencia inexistente
- **THEN** la base MUST rechazarla

#### Scenario: Preservación de categoría histórica

- **GIVEN** una designación o snapshot previo con Categoría 0
- **WHEN** se migra y consulta
- **THEN** MUST conservar ese texto como histórico sin recategorizarlo ni ofrecerlo como séptima opción

#### Scenario: Desactivar una categoría preserva referencias

- **GIVEN** registros que refieren a una categoría luego inactivada
- **WHEN** se consulta el historial o se intenta una nueva selección
- **THEN** MUST resolverse lo histórico y MUST rechazarse su nueva selección

### Requirement: Horas complementarias del estado vigente

Las designaciones SHALL conservar horas de investigación y externas, además de las de materia, para consultas, snapshots y exportación. Alta y Cambio MUST materializar los valores solicitados. La migración MUST recuperar valores conocidos sin inventar valores para datos ausentes.

#### Scenario: Aprobación conserva las tres horas

- **GIVEN** un Alta o Cambio con cargas 12, 4 y 2
- **WHEN** Decanato aprueba y se consulta la designación resultante
- **THEN** MUST devolver 12, 4 y 2 y futuras solicitudes MUST poder fotografiar esos valores vigentes
