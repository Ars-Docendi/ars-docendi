## Purpose

Define un conjunto sintético, seguro y repetible que permite validar integralmente la aplicación en ambientes no productivos sin copiar información real.

## ADDED Requirements

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
