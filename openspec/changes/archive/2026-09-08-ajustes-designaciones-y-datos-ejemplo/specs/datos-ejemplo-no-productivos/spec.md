## ADDED Requirements

### Requirement: Pedidos sintéticos completos y utilizables

Todos los pedidos sintéticos SHALL tener evento de creación con fecha, hora y autor Jefe de Cátedra con ámbito sobre su materia. Todo pedido que haya salido del borrador SHALL tener primer envío y cadena coherente hasta su estado; Inicio del circuito SHALL corresponder al primer envío. El docente SHALL estar asociado a esa materia por designación previa en Baja/Cambio o por la incorporación propuesta en Alta. El dataset MUST NOT usar Sin novedad y SHALL mantener cobertura de todos los estados soportados.

#### Scenario: Inicio disponible en revisión

- **GIVEN** cualquier pedido sintético visible en revisión, incluidos devueltos y finalizados
- **WHEN** se consulta su fila e historial
- **THEN** MUST tener inicio de circuito, creación anterior o simultánea al envío y eventos cronológicos coherentes con el estado final

#### Scenario: Autor y docente en el ámbito correcto

- **GIVEN** cualquier pedido sintético
- **WHEN** se resuelve su creador y materia
- **THEN** el creador MUST ser Jefe de esa materia y el docente MUST corresponder a ella, sin inventar una designación previa para las Altas

### Requirement: Valores sintéticos positivos y categorías válidas

Las cargas horarias solicitadas y las cargas vigentes conocidas de los ejemplos SHALL ser positivas, incluidas investigación y externas, y sus dedicaciones seleccionables SHALL pertenecer al catálogo 1 a 6. El dataset SHALL incluir al menos un Cambio devuelto con las tres horas solicitadas diferentes del snapshot y un docente vigente sin pedido para verificar continuidad. Los datos anteriores inexistentes de un Alta SHALL permanecer ausentes; esta regla de ejemplos MUST NOT impedir horas complementarias cero válidas en uso real.

#### Scenario: Ejemplo de devolución evidencia la corrección

- **GIVEN** el Cambio devuelto del dataset
- **WHEN** se abre detalle y edición
- **THEN** MUST distinguirse las tres cargas positivas solicitadas de las históricas

#### Scenario: Reejecución del seed

- **GIVEN** la base sembrada sin modificaciones posteriores
- **WHEN** se vuelve a sembrar
- **THEN** MUST conservarse el mismo conjunto lógico, referencias y cobertura sin duplicar filas
