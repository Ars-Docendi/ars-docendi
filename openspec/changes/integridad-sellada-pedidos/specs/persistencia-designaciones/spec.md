# Spec Delta

## MODIFIED Requirements

### Requirement: Historial del trámite como dato de dominio

El sistema SHALL persistir el historial de cada pedido en `designaciones.pedido_historial`, con la acción, el rol con el que actuó el actor, la etapa del pedido al momento del evento, el comentario o justificativo y la fecha. El historial MUST NOT derivarse de `audit.change_log`: el rol con el que se actuó no es derivable de un usuario que puede tener varios, y el comentario es dato de negocio exigido por BR-designaciones-005. La tabla SHALL estar además auditada mediante `audit.attach`, y sus filas MUST NOT purgarse. La base MUST rechazar la modificación de cualquier evento y su borrado, salvo el que arrastra la eliminación de un pedido en borrador [BR-designaciones-019]. El historial consultado y mostrado SHALL estar ordenado por instante ascendente con desempate estable. Cada evento SHALL mostrar fecha y hora en zona America/Argentina/Buenos_Aires, con formato dd/MM/yyyy HH:mm; el orden MUST basarse en el instante original.

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

- **WHEN** un cliente SQL intenta modificar o eliminar directamente una fila de `pedido_historial`
- **THEN** la base MUST rechazar la sentencia y conservar el evento
- **AND** si las protecciones fueron desactivadas, `audit.change_log` MUST registrar el cambio y la verificación de integridad MUST reportarlo

#### Scenario: Eliminar un borrador arrastra su historial

- **GIVEN** un pedido en borrador con eventos `crear` y `editar`
- **WHEN** el Jefe de Cátedra lo elimina
- **THEN** el pedido y sus eventos MUST eliminarse sin error

#### Scenario: Eventos del mismo día

- **GIVEN** eventos de las 15:30 UTC y 12:15 UTC recibidos fuera de orden
- **WHEN** se muestra el historial
- **THEN** MUST aparecer primero 09:15 y luego 12:30 con sus fechas locales y autores

## ADDED Requirements

### Requirement: Un pedido enviado no se borra físicamente

La base SHALL rechazar el borrado físico de un pedido que no esté en `borrador`, cualquiera sea el
cliente que lo ejecute. Un pedido que dejó de interesar sigue el circuito (cancelar, rechazar).

#### Scenario: Borrado por SQL de un pedido enviado

- **GIVEN** un pedido en `en_revision_decanato`
- **WHEN** un cliente SQL intenta borrarlo
- **THEN** la base MUST rechazar la sentencia y conservar el pedido con su historial
