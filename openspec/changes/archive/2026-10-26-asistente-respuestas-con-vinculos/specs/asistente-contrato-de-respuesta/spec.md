## ADDED Requirements

### Requirement: La respuesta dice de qué áreas salen sus datos

El sistema SHALL acompañar cada respuesta que ejecutó una consulta con las áreas de datos que esa consulta leyó, derivadas de los schemas que cita la consulta validada.

El sistema MUST NOT incluir en ese dato la consulta, sus tablas ni la cantidad de filas.

#### Scenario: Una consulta sobre pedidos nombra Designaciones

- **GIVEN** una consulta validada que lee `designaciones.pedidos` e `identity.materias`
- **WHEN** se arma la respuesta
- **THEN** la respuesta nombra las áreas `designaciones` e `identity`, y nada más sobre la consulta

#### Scenario: Una respuesta sin consulta no nombra áreas

- **GIVEN** un turno que termina en aclaración, sin ejecutar consulta
- **WHEN** se arma la respuesta
- **THEN** la respuesta no nombra ninguna área
