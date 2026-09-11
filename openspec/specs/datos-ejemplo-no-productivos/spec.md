# datos-ejemplo-no-productivos Specification

## Purpose

Define un conjunto sintético, seguro y repetible que permite validar integralmente la aplicación en ambientes no productivos sin copiar información real.

## Requirements

### Requirement: Siembra limitada a ambientes no productivos

El sistema MUST impedir la siembra sintética en producción y MUST impedir que una base productiva sea utilizada como origen de datos para un ambiente no productivo.

#### Scenario: Intento de sembrar producción

- **GIVEN** un operador que identifica el destino como producción
- **WHEN** solicita ejecutar el proceso de siembra sintética
- **THEN** el proceso MUST abortar antes de insertar o modificar filas

#### Scenario: Intento de copiar datos productivos

- **GIVEN** un ambiente no productivo y una base productiva configurada como origen
- **WHEN** se inicia la siembra
- **THEN** el proceso MUST abortar sin copiar información

### Requirement: Dataset sintético coherente e idempotente

El seed MUST poder ejecutarse repetidas veces con el mismo resultado lógico y MUST mantener íntegras todas las referencias entre `identity` y `designaciones`.

#### Scenario: Segunda ejecución del seed

- **GIVEN** una base ya sembrada sin cambios posteriores
- **WHEN** el seed se ejecuta nuevamente
- **THEN** no se duplican registros y el conjunto lógico resultante permanece igual

#### Scenario: Referencias entre módulos

- **GIVEN** pedidos y designaciones que refieren a personas, materias, roles, cargos y períodos sembrados
- **WHEN** finaliza la siembra
- **THEN** todas las claves referenciadas MUST existir y satisfacer las restricciones de la base

### Requirement: Cobertura representativa del producto

El dataset SHALL incluir usuarios activos e inactivos, todos los roles de sistema, ámbitos globales, por carrera y por materia, personas con y sin cuenta, roles con permisos, carreras, materias, cargos, designaciones, períodos y pedidos representativos de cada estado soportado.

#### Scenario: Validación de cobertura

- **GIVEN** una base vacía con las migraciones aplicadas
- **WHEN** se completa la siembra
- **THEN** cada rol, ámbito y estado de pedido soportado cuenta con al menos un registro utilizable por la UI

#### Scenario: Identificación del origen

- **GIVEN** una base sembrada
- **WHEN** se consulta su metadata de seed
- **THEN** el origen MUST figurar como sintético y no productivo

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
