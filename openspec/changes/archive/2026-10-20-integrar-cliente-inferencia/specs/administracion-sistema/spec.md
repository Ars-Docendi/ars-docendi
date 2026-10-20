## MODIFIED Requirements

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
