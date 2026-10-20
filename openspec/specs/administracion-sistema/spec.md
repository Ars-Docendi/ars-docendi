# administracion-sistema Specification

## Purpose

Proporciona a quienes administran el sistema una vista segura de su disponibilidad operativa y una consulta de solo lectura de los cambios persistidos en la auditoría, sin requerir acceso directo a PostgreSQL.

## Requirements

### Requirement: Dashboard de salud operativa

El sistema SHALL ofrecer a usuarios con el permiso `sistema.estado.ver` una vista del estado actual del backend, de PostgreSQL y del servicio de inferencia. La vista MUST distinguir el resultado de cada ping de módulo (`designaciones`, `aulas`, `portal` y `tareas`) del resultado de la comprobación de conectividad de la base de datos y del de la comprobación de inferencia. Cada resultado MUST incluir la hora de comprobación; los de la base de datos y de la inferencia MUST incluir la duración de la comprobación. Una comprobación fallida MUST mostrarse como no disponible o sin comprobar, sin ocultar el estado de los demás componentes ni exponer detalles internos, credenciales o cadenas de conexión. La inferencia deshabilitada por configuración MUST mostrarse como deshabilitada, distinta de una falla. La vista MUST representar una comprobación remota pendiente, fallida o desactualizada de forma distinta de un resultado saludable.

#### Scenario: Todos los componentes responden

- **GIVEN** un usuario con `sistema.estado.ver` y respuestas exitosas de los cuatro pings, de la comprobación de PostgreSQL y de la comprobación de inferencia
- **WHEN** abre o actualiza el dashboard
- **THEN** ve el estado de cada módulo, de la base de datos y de la inferencia, la hora de cada comprobación y la duración de las comprobaciones de PostgreSQL e inferencia

#### Scenario: Falla un ping de módulo

- **GIVEN** un usuario con `sistema.estado.ver` y un ping que falla o vence su tiempo límite
- **WHEN** se actualiza el dashboard
- **THEN** el módulo afectado aparece como no disponible y los resultados de los demás componentes permanecen visibles

#### Scenario: PostgreSQL no está disponible

- **GIVEN** un usuario con `sistema.estado.ver` y una comprobación de conectividad fallida
- **WHEN** se actualiza el dashboard
- **THEN** PostgreSQL aparece como no disponible con un mensaje seguro, sin información de conexión ni excepción interna

#### Scenario: Inferencia deshabilitada

- **GIVEN** un usuario con `sistema.estado.ver` en un ambiente sin inferencia configurada
- **WHEN** abre el dashboard
- **THEN** la inferencia aparece como deshabilitada, con un estilo distinto de "no disponible", y los demás componentes conservan su estado

#### Scenario: Consulta del dashboard sin permiso

- **GIVEN** un usuario autenticado sin `sistema.estado.ver`
- **WHEN** intenta abrir la ruta o consultar su API
- **THEN** el frontend no muestra la pantalla y el backend deniega la consulta

### Requirement: Consulta paginada y de solo lectura de auditoría

El sistema SHALL permitir a usuarios con `auditoria.ver` consultar eventos de `audit.change_log` mediante una API de solo lectura y una pantalla administrativa. La tabla MUST presentar a simple vista fecha, usuario, acción, módulo y un resumen del cambio. MUST mostrar un nombre legible del actor cuando pueda resolverse y MUST indicar que el actor no está identificado cuando `changed_by` sea nulo o no tenga una cuenta asociada; MUST NOT sustituir ese estado por una atribución no verificada. La acción MUST expresarse en español sin confundir una actualización con una eliminación física. El módulo MUST mostrarse con una etiqueta legible derivada del schema de origen y MUST conservar un fallback para schemas desconocidos. El resumen MUST identificar el objeto y los campos modificados mediante etiquetas comprensibles; la clave de fila y `request_id` MUST estar disponibles como contexto del evento.

La consulta MUST ordenar los eventos del más reciente al más antiguo de manera estable y paginar en el servidor. MUST conservar filtros por rango de fechas, acción, módulo/schema, tabla, actor y clave de fila; el filtro de actor MUST admitir búsqueda por nombre legible. MUST rechazar rangos inválidos y límites de página fuera del máximo permitido. La pantalla MUST permitir inspeccionar cambios anteriores y nuevos sólo mediante valores aprobados y seguros: MUST enmascarar campos personales o secretos, ocultar valores de campos no clasificados y MUST NOT devolver ni presentar los snapshots `old_row`/`new_row` completos sin procesar ni `client_ip`. MUST NOT mostrar UPN o correo del actor. La consulta MUST NOT modificar ni eliminar eventos.

#### Scenario: Consultar eventos recientes

- **GIVEN** un usuario con `auditoria.ver` y eventos registrados
- **WHEN** abre la pantalla de auditoría sin filtros
- **THEN** recibe una página acotada, ordenada por fecha e identificador, con fecha, nombre del usuario o fallback, acción en español, módulo y resumen del cambio visibles en la tabla
- **AND** puede consultar la clave de fila y `request_id` como contexto del evento

#### Scenario: Identificar al actor

- **GIVEN** un evento cuyo `changed_by` refiere a una cuenta de `identity.users`
- **WHEN** se presenta el evento
- **THEN** se muestra el nombre de la persona vinculada si existe, o `display_name` de la cuenta como fallback
- **AND** no se exponen UPN, correo ni otros datos personales de la cuenta

#### Scenario: Actor no identificado

- **GIVEN** un evento con `changed_by` nulo o sin una cuenta resoluble
- **WHEN** se presenta el evento
- **THEN** la interfaz indica que el actor no está identificado y no lo atribuye a un proceso automático sin evidencia

#### Scenario: Etiquetar la acción y el módulo

- **GIVEN** eventos de schemas conocidos y desconocidos con acciones `INSERT`, `UPDATE` o `DELETE`
- **WHEN** se presenta cada evento
- **THEN** se muestra una acción comprensible y una etiqueta de módulo derivada del schema
- **AND** los schemas desconocidos conservan una etiqueta de fallback
- **AND** una acción `UPDATE` no se presenta como eliminación física

#### Scenario: Inspeccionar un cambio con PII

- **GIVEN** un evento cuyos snapshots contienen campos personales, secretos o campos sin clasificar
- **WHEN** el usuario autorizado inspecciona el resumen o el detalle
- **THEN** ve el objeto y los nombres legibles de los campos modificados, con valores sólo para campos expresamente aprobados
- **AND** los campos sensibles aparecen enmascarados, los desconocidos no exponen su valor y no se presentan snapshots crudos ni `client_ip`

#### Scenario: Filtrar eventos

- **GIVEN** un usuario con `auditoria.ver` y filtros válidos de fechas, acción, módulo/schema, tabla, actor o clave de fila
- **WHEN** consulta la auditoría
- **THEN** la API devuelve únicamente eventos que cumplen los filtros, paginados en el servidor
- **AND** la búsqueda por actor admite el nombre legible mostrado en la tabla

#### Scenario: Rango o paginación inválidos

- **GIVEN** un rango de fechas invertido o un tamaño de página superior al máximo
- **WHEN** el usuario envía la consulta
- **THEN** la API rechaza la entrada con un error de validación estable y no ejecuta una consulta sin límite

#### Scenario: Consultar auditoría sin permiso

- **GIVEN** un usuario autenticado sin `auditoria.ver`
- **WHEN** intenta abrir la ruta o consultar la API
- **THEN** el frontend no muestra la pantalla y el backend deniega la consulta sin revelar eventos

#### Scenario: No hay eventos para los filtros

- **GIVEN** un usuario con `auditoria.ver` y filtros válidos sin coincidencias
- **WHEN** termina la consulta
- **THEN** la pantalla presenta un estado vacío distinto de carga y error

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
