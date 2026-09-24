## MODIFIED Requirements

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
