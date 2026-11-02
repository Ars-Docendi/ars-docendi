## ADDED Requirements

### Requirement: Seguimiento del pedido según permiso

La API SHALL entregar la etapa real, el historial y a quién se devolvió un pedido sólo a actores con `designaciones.ver_seguimiento`. A los demás SHALL entregarles un estado simplificado (`borrador`, `en_proceso`, `devuelto`, `aprobado`, `rechazado`, `cancelado`) sin historial ni etapa de retorno: `en_proceso` mientras el pedido circula o está devuelto a otro actor, `devuelto` sólo cuando volvió al rol del actor, junto con el motivo de la devolución, y `aprobado` cuando quedó en lote. Las acciones permitidas SHALL seguir calculándose sobre el estado real.

#### Scenario: Jefe de Cátedra con un pedido en revisión

- **GIVEN** un Jefe de Cátedra sin `designaciones.ver_seguimiento` y un pedido suyo en revisión de Secretaría
- **WHEN** consulta el pedido
- **THEN** lo ve "en proceso", sin historial ni etapa

#### Scenario: Pedido devuelto al Jefe

- **GIVEN** un pedido devuelto por Coordinación al Jefe de Cátedra con el comentario "Falta el CV"
- **WHEN** el Jefe consulta el pedido
- **THEN** lo ve "devuelto" con el motivo "Falta el CV", sin saber quién lo devolvió

#### Scenario: Pedido devuelto a otro actor

- **GIVEN** un pedido que Decanato devolvió a Secretaría
- **WHEN** el Jefe de Cátedra lo consulta
- **THEN** lo ve "en proceso"

#### Scenario: Fin del circuito

- **GIVEN** un pedido que Decanato aceptó y quedó en lote
- **WHEN** el Jefe de Cátedra lo consulta
- **THEN** lo ve "aprobado"

#### Scenario: Revisor con seguimiento

- **GIVEN** un actor de Secretaría con `designaciones.ver_seguimiento`
- **WHEN** consulta un pedido en revisión
- **THEN** ve la etapa real y el historial completo
