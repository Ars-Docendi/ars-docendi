## ADDED Requirements

### Requirement: Reinicio de bases descartables en cada despliegue

Cada despliegue efectivo de staging o pr-N SHALL reconstruir exclusivamente la base de ese ambiente desde cero, ejecutar las migraciones versionadas y sembrar los datos sintéticos antes de habilitar la aplicación. MUST eliminar datos dejados por usos anteriores, incluyendo auditoría e idempotencia. Producción y otros ambientes MUST permanecer intactos. Los gates existentes de PR SHALL conservarse.

#### Scenario: Segundo despliegue sobre el mismo ambiente

- **GIVEN** staging o pr-N desplegado y modificado por un usuario
- **WHEN** se despliega de nuevo
- **THEN** MUST quedar el schema y dataset de la versión desplegada sin los cambios de la sesión anterior

#### Scenario: Protección de producción y ambientes vecinos

- **GIVEN** bases de prod, staging y dos PRs
- **WHEN** se reconstruye uno de los PRs o se intenta pasar prod al proceso de reinicio
- **THEN** sólo el PR indicado MUST poder reconstruirse y el intento sobre prod MUST abortar antes de eliminar datos

#### Scenario: Falla de migración o seed

- **GIVEN** un ambiente descartable en reconstrucción
- **WHEN** falla una migración o la siembra
- **THEN** MUST fallar el despliegue sin servir la aplicación sobre datos parciales y un reintento MUST poder reconstruir desde cero

#### Scenario: Despliegues concurrentes del mismo ambiente

- **GIVEN** dos ejecuciones sobre el mismo staging o PR
- **WHEN** alcanzan el reinicio
- **THEN** MUST serializarse la reconstrucción sin intercalar eliminación, migraciones y seed; otros ambientes MUST mantener aislamiento
