## MODIFIED Requirements

### Requirement: Catálogos canónicos

Personas, materias, cargos, dedicaciones y períodos SHALL provenir de la API. Las mutaciones MUST enviar sus UUID canónicos. Los formularios SHALL ofrecer sólo materias y cargos activos dentro del ámbito del actor. Las dedicaciones seleccionables SHALL ser Categoría 1 a 6, de elección libre incluso al mantener o reducir la dedicación actual; la API MUST validar sus referencias canónicas.

#### Scenario: Abrir formulario

- **WHEN** un actor abre el alta o edición
- **THEN** ve el período activo y los catálogos persistidos que puede utilizar

#### Scenario: Cambio de dedicación libre

- **GIVEN** un docente con Categoría 2
- **WHEN** se carga un Cambio
- **THEN** el selector MUST ofrecer las seis categorías y backend MUST aceptar 1, 2 o 6 con los demás datos válidos

#### Scenario: Categoría ajena al catálogo

- **GIVEN** una nueva solicitud
- **WHEN** un cliente envía Categoría 0, texto libre o un UUID inexistente
- **THEN** backend MUST rechazar la selección sin persistir el pedido

### Requirement: Reglas por novedad

El backend MUST aplicar las reglas por novedad al crear y editar. `Alta` exige CV, DNI frente y DNI dorso [BR-designaciones-002]. `Baja` exige tipo, adjunto justificativo y persona con legajo [BR-designaciones-003, BR-designaciones-018]. `Cambio de cargo o dedicación` exige justificación y persona con legajo [BR-designaciones-004, BR-designaciones-018]. Las únicas novedades admitidas para nuevos pedidos, ediciones y avance del circuito SHALL ser Alta, Baja y Cambio de cargo o dedicación. El formulario MUST exigir una elección explícita. `Sin novedad` MUST NOT ofrecerse ni aceptarse como nueva solicitud; los registros legados SHALL conservarse para lectura histórica.

#### Scenario: Alta incompleta

- **WHEN** se guarda un Alta sin alguno de sus tres adjuntos
- **THEN** el backend rechaza el pedido

#### Scenario: Baja o Cambio sin legajo

- **WHEN** la persona no tiene legajo
- **THEN** el backend rechaza una Baja o Cambio, pero permite un Alta con documentación completa

#### Scenario: Solicitud Sin novedad rechazada

- **GIVEN** un cliente que omite el formulario
- **WHEN** intenta crear, editar, enviar, reenviar o aprobar un pedido Sin novedad
- **THEN** la API MUST rechazar la operación sin cambiar datos ni historial

#### Scenario: Lectura de pedido legado

- **GIVEN** un pedido Sin novedad anterior al cambio
- **WHEN** se consulta su detalle autorizado
- **THEN** MUST conservar su número, texto original e historial sin transformarse en otra novedad

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

## ADDED Requirements

### Requirement: Identificación visible por número

Toda referencia visible a un pedido SHALL usar su número de negocio. El UUID MUST quedar reservado a rutas y referencias técnicas y MUST NOT aparecer como etiqueta, título, mensaje, tooltip o contenido de descarga del pedido.

#### Scenario: Número distinto del UUID

- **GIVEN** un pedido con número 2026-0123 y UUID técnico diferente
- **WHEN** se abre su detalle, edición o modal de acción
- **THEN** toda identificación visible MUST usar 2026-0123 y no el UUID
