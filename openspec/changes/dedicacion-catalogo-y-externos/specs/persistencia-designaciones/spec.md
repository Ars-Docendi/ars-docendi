# Spec Delta

## MODIFIED Requirements

### Requirement: Designación vigente como entidad de estado

El sistema SHALL persistir en `designaciones.designaciones` el estado vigente de cada docente: la tupla `(persona, materia, cargo y dedicación o sector externo, horas)` con `vigente_desde` y `vigente_hasta` nullable. Una designación con `vigente_hasta` en NULL SHALL considerarse vigente. El sistema MUST impedir que una misma persona tenga dos designaciones vigentes simultáneas sobre la misma materia. Esta tabla SHALL ser la fuente de los datos actuales que el formulario de pedido muestra de solo lectura (cargo actual, dedicación actual o sector externo, materias y horas vigentes).

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

### Requirement: Catálogo persistido de dedicaciones

El sistema SHALL mantener en el esquema designaciones un catálogo con las siete categorías seleccionables 0 a 6, con identificador canónico, código único, nombre y estado activo. Pedidos y designaciones SHALL referenciarlo con integridad en base y MUST NOT guardar la dedicación como texto libre; las nuevas selecciones MUST pertenecer a ese catálogo y estar activas. La administración de docentes SHALL respetar el mismo catálogo.

#### Scenario: Integridad de una nueva selección

- **GIVEN** un pedido o designación nuevo
- **WHEN** se intenta escribir una referencia inexistente
- **THEN** la base MUST rechazarla

#### Scenario: Categoría 0 como opción del catálogo

- **GIVEN** un docente Ayudante Alumno con Categoría 0
- **WHEN** se registra su designación o se carga un pedido con Categoría 0
- **THEN** la dedicación MUST referir a la Categoría 0 del catálogo y mostrarse como cualquier otra categoría

#### Scenario: Preservación de categoría histórica

- **GIVEN** un snapshot de pedido congelado antes de este cambio con Categoría 0
- **WHEN** se consulta el pedido o se exporta el lote
- **THEN** MUST mostrar el texto del snapshot tal como fue congelado, sin recalcularlo desde el catálogo

#### Scenario: Sin dedicación textual

- **WHEN** se intenta guardar una dedicación como texto, sin referencia al catálogo
- **THEN** el sistema MUST rechazarlo, porque la única forma de dedicación es la del catálogo

#### Scenario: Desactivar una categoría preserva referencias

- **GIVEN** registros que refieren a una categoría luego inactivada
- **WHEN** se consulta el historial o se intenta una nueva selección
- **THEN** MUST resolverse lo histórico y MUST rechazarse su nueva selección
