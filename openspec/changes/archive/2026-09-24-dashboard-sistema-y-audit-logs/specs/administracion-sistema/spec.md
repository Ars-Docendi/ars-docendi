## Purpose

Proporciona a quienes administran el sistema una vista segura de su disponibilidad operativa y una consulta de solo lectura de los cambios persistidos en la auditoría, sin requerir acceso directo a PostgreSQL.

## ADDED Requirements

### Requirement: Dashboard de salud operativa

El sistema SHALL ofrecer a usuarios con el permiso `sistema.estado.ver` una vista del estado actual del backend y de PostgreSQL. La vista MUST distinguir el resultado de cada ping de módulo (`designaciones`, `aulas`, `portal` y `tareas`) del resultado de la comprobación de conectividad de la base de datos. Cada resultado MUST incluir la hora de comprobación; el de la base de datos MUST incluir la duración de la consulta. Una comprobación fallida MUST mostrarse como no disponible o sin comprobar, sin ocultar el estado de los demás componentes ni exponer detalles internos, credenciales o cadenas de conexión. La vista MUST representar una comprobación remota pendiente, fallida o desactualizada de forma distinta de un resultado saludable.

#### Scenario: Todos los componentes responden

- **GIVEN** un usuario con `sistema.estado.ver` y respuestas exitosas de los cuatro pings y de la comprobación de PostgreSQL
- **WHEN** abre o actualiza el dashboard
- **THEN** ve el estado de cada módulo, el estado de la base de datos, la hora de cada comprobación y la duración de la consulta a PostgreSQL

#### Scenario: Falla un ping de módulo

- **GIVEN** un usuario con `sistema.estado.ver` y un ping que falla o vence su tiempo límite
- **WHEN** se actualiza el dashboard
- **THEN** el módulo afectado aparece como no disponible y los resultados de los demás componentes permanecen visibles

#### Scenario: PostgreSQL no está disponible

- **GIVEN** un usuario con `sistema.estado.ver` y una comprobación de conectividad fallida
- **WHEN** se actualiza el dashboard
- **THEN** PostgreSQL aparece como no disponible con un mensaje seguro, sin información de conexión ni excepción interna

#### Scenario: Consulta del dashboard sin permiso

- **GIVEN** un usuario autenticado sin `sistema.estado.ver`
- **WHEN** intenta abrir la ruta o consultar su API
- **THEN** el frontend no muestra la pantalla y el backend deniega la consulta

### Requirement: Consulta paginada y de solo lectura de auditoría

El sistema SHALL permitir a usuarios con `auditoria.ver` consultar eventos de `audit.change_log` mediante una API de solo lectura y una pantalla administrativa. El listado MUST presentar como mínimo fecha, acción, schema, tabla, clave de fila, actor (que puede ser desconocido), columnas cambiadas y `request_id`; MUST ordenar los eventos del más reciente al más antiguo de manera estable y paginar en el servidor. La consulta MUST admitir filtros por rango de fechas, acción, schema, tabla, actor y clave de fila, y MUST rechazar rangos inválidos y límites de página fuera del máximo permitido. La pantalla MUST permitir inspeccionar cambios anteriores y nuevos sólo mediante valores aprobados y seguros: MUST enmascarar campos personales o secretos, ocultar valores de campos no clasificados y MUST NOT devolver ni presentar los snapshots `old_row`/`new_row` completos sin procesar ni `client_ip`. La consulta MUST NOT modificar ni eliminar eventos.

#### Scenario: Consultar eventos recientes

- **GIVEN** un usuario con `auditoria.ver` y eventos registrados
- **WHEN** abre la pantalla de auditoría sin filtros
- **THEN** recibe una página acotada de eventos recientes con los metadatos mínimos definidos y orden estable por fecha e identificador

#### Scenario: Filtrar eventos

- **GIVEN** un usuario con `auditoria.ver` y filtros válidos de fechas, acción, schema, tabla, actor o clave de fila
- **WHEN** consulta la auditoría
- **THEN** la API devuelve únicamente eventos que cumplen los filtros, paginados en el servidor

#### Scenario: Rango o paginación inválidos

- **GIVEN** un rango de fechas invertido o un tamaño de página superior al máximo
- **WHEN** el usuario envía la consulta
- **THEN** la API rechaza la entrada con un error de validación estable y no ejecuta una consulta sin límite

#### Scenario: Inspeccionar un cambio con PII

- **GIVEN** un evento cuyos snapshots contienen campos personales, secretos o campos sin clasificar
- **WHEN** el usuario autorizado abre el detalle del evento
- **THEN** ve los nombres de columnas modificadas y sólo valores permitidos; los campos sensibles aparecen enmascarados y los desconocidos no exponen su valor

#### Scenario: Consultar auditoría sin permiso

- **GIVEN** un usuario autenticado sin `auditoria.ver`
- **WHEN** intenta abrir la ruta o consultar la API
- **THEN** el frontend no muestra la pantalla y el backend deniega la consulta sin revelar eventos

#### Scenario: No hay eventos para los filtros

- **GIVEN** un usuario con `auditoria.ver` y filtros válidos sin coincidencias
- **WHEN** termina la consulta
- **THEN** la pantalla presenta un estado vacío distinto de carga y error
