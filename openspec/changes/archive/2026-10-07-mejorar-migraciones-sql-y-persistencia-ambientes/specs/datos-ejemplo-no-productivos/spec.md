## ADDED Requirements

### Requirement: Inicialización sintética única en ambientes persistentes

El deploy SHALL sembrar staging/pr-N únicamente durante la inicialización autorizada de su base y registrar dataset e inicialización completada en la misma transacción que los datos. Los redeploys SHALL conservar las ediciones posteriores y MUST NOT ejecutar reseed por un cambio de versión del dataset. Una base poblada sin marcador reconocido MUST NOT tratarse como vacía ni sobrescribirse. Un seed inicial fallido SHALL poder reintentarse sólo cuando la inicialización incompleta sea verificable y no existan datos de uso posterior. El reset del dataset SHALL requerir una operación explícita no productiva, distinta del deploy. Las prohibiciones de prod y de copiar datos reales SHALL mantenerse.

#### Scenario: Primera inicialización

- **GIVEN** una base no productiva creada y migrada por el proceso de inicialización
- **WHEN** finaliza correctamente la siembra
- **THEN** MUST existir el dataset sintético y su marcador completo confirmado atómicamente

#### Scenario: Redeploy después de ediciones

- **GIVEN** una base sembrada con modificaciones desde la aplicación
- **WHEN** se despliega una versión nueva, incluso con un dataset de otra versión
- **THEN** MUST conservarse las modificaciones y MUST NOT ejecutarse la siembra otra vez

#### Scenario: Seed inicial fallido

- **GIVEN** una inicialización reconocida sin datos de uso posterior
- **WHEN** falla una operación del seed
- **THEN** MUST revertirse la transacción sin marcador de éxito ni dataset parcial
- **AND** un reintento autorizado MUST poder completar la inicialización

#### Scenario: Base poblada sin marcador

- **GIVEN** datos existentes sin marcador de inicialización reconocido
- **WHEN** el deploy evalúa sembrar
- **THEN** MUST rechazar la siembra automática sin modificar esas filas

#### Scenario: Protección de datos reales y prod

- **WHEN** se intenta inicializar prod con fixtures o usar datos SGA/productivos fuera del desarrollo local autorizado
- **THEN** MUST rechazarse la operación antes de copiar o escribir datos
