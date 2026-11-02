# Spec Delta

## ADDED Requirements

### Requirement: Identidad completa de eventos sobre relaciones

Los eventos nuevos de tablas con clave compuesta SHALL conservar todos los componentes de esa clave en una representación estable. La consulta MUST distinguir una relación precisa de una agrupación legada y no atribuir a una relación eventos indistinguibles. Los eventos históricos MUST NOT reescribirse ni desaparecer al introducir el formato nuevo.

#### Scenario: Dos permisos del mismo rol

- **GIVEN** un rol con dos relaciones de permiso distintas
- **WHEN** se registran y consultan sus eventos nuevos
- **THEN** las identidades precisas distinguen rol y permiso; filtrar por una relación no devuelve los cambios de la otra

#### Scenario: Habilidad e interés del mismo perfil

- **GIVEN** una habilidad asociada a un perfil como habilidad e interés
- **WHEN** se consulta su historia precisa
- **THEN** distingue perfil, habilidad y tipo

#### Scenario: Evento legacy sin identidad recuperable

- **GIVEN** un evento antiguo con clave parcial y sin componentes suficientes en sus snapshots
- **WHEN** se consulta una historia precisa
- **THEN** no se inventa identidad ni se incluye como coincidencia exacta; sigue disponible como agrupación histórica declarada

### Requirement: Metadatos históricos con desempate determinístico

La consulta de historia SHALL desempatar eventos por identificador además de fecha. Fecha y actor de cada resultado MUST provenir del mismo evento seleccionado. El resultado SHALL conservar orden estable en eventos de una misma transacción y diferenciar creación, última modificación y eliminación.

#### Scenario: Eventos empatados

- **GIVEN** creación y dos modificaciones de una fila con el mismo timestamp
- **WHEN** se consulta su historia
- **THEN** creación corresponde al primer evento pertinente y última modificación al último por ID, con su actor correcto

### Requirement: Snapshots nuevos minimizados y legado explícito

Los eventos nuevos MUST persistir valores anteriores/nuevos solo para campos expresamente clasificados como seguros y necesarios, además de nombres de columnas modificadas e identidad técnica. Datos personales, secretos y valores no clasificados MUST NOT duplicarse en snapshots nuevos. La consulta del legado SHALL mantener redacción y controles de acceso sin alterar los eventos automáticamente.

#### Scenario: Cambio de datos personales

- **GIVEN** una persona modifica documento, teléfono o cuenta
- **WHEN** se genera auditoría nueva
- **THEN** se registra actor, operación, identidad y columnas afectadas sin copiar esos valores sensibles

#### Scenario: Cambio intradía trazable

- **GIVEN** cambia una vigencia y su origen de pedido
- **WHEN** se genera auditoría
- **THEN** se conservan valores técnicos de cargo, dedicación, horas, fechas y orígenes anterior/nuevo necesarios para reconstruir el cambio sin copiar PII del docente

#### Scenario: Evento viejo con PII

- **GIVEN** snapshots antiguos contienen datos personales
- **WHEN** un usuario autorizado consulta la auditoría
- **THEN** sigue recibiendo solo valores aprobados y nunca snapshots crudos, correo de actor ni secretos

### Requirement: Retención auditada sin purga automática

La documentación operativa SHALL distinguir metadata de eventos, snapshots nuevos minimizados y valores históricos sensibles, sus accesos y respaldos. MUST declarar que redacción de API no equivale a borrado en BD. Sin política institucional y autorización específica MUST NOT ejecutarse purga, anonimización retrospectiva ni eliminación de historial de trámites.

#### Scenario: Política no aprobada

- **GIVEN** no existe autorización específica para tratar snapshots históricos
- **WHEN** se despliega la mejora de minimización
- **THEN** no se purgan eventos ni se atribuye cumplimiento de un plazo legal inventado

### Requirement: Consulta de auditoría con medición y límites preservados

La lectura administrativa SHALL conservar filtros seguros, conteo correcto, paginación del servidor y orden estable. Sus mejoras MUST verificarse con planes, buffers y carga sintética sin quitar límites, permisos o redacción. La adopción de índices SHALL demostrar utilidad para consultas identificadas y considerar costo de escritura.

#### Scenario: Lista reciente sobre muchos eventos

- **GIVEN** muchos eventos sintéticos y una página pequeña solicitada
- **WHEN** se verifica el plan del listado reciente y su conteo
- **THEN** se documenta el trabajo de ambas consultas y no se presenta optimizar solo la página como optimización del conteo
