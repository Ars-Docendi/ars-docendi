## MODIFIED Requirements

### Requirement: Migraciones one-shot idempotentes vía --migrate

El `ArsDocendi.Host` SHALL reconocer `--migrate` y aplicar las migraciones pendientes de Identity/Audit, Storage y los módulos Designaciones, Aulas, Portal y Tareas que tengan migraciones disponibles. MUST respetar dependencias y terminar con exit code 0 sin levantar listener sólo cuando todas las operaciones terminen correctamente. Re-ejecutar sobre historia compatible ya actualizada MUST no alterar datos/schema. El Host SHALL coordinar mediante un contrato transversal puro sin referenciar DbContexts ni implementaciones internas de otros módulos. Ante historia incompatible o falla MUST devolver exit no cero sin aparentar éxito; MUST NOT prometer atomicidad global entre contextos.

#### Scenario: Migrar una base nueva

- **WHEN** se ejecuta el backend con `--migrate` contra una base vacía
- **THEN** MUST aplicarse Identity/Audit y Storage antes de sus consumidores
- **AND** MUST terminar sin abrir el listener HTTP

#### Scenario: Re-ejecutar migraciones es idempotente

- **WHEN** se ejecuta `--migrate` por segunda vez sobre una base compatible ya migrada
- **THEN** no se aplica ninguna migración nueva ni se modifica el dataset
- **AND** el proceso termina con exit 0

#### Scenario: Arranque normal sin el argumento

- **WHEN** el backend arranca sin modo de migraciones
- **THEN** levanta el web server normalmente y NO aplica migraciones automáticamente

#### Scenario: La migración respeta la frontera de módulos

- **WHEN** se inspeccionan las referencias del Host
- **THEN** las operaciones de migración MUST cruzar sólo contratos públicos sin usar DbContexts internos

#### Scenario: Falla en un contexto

- **GIVEN** un contexto falla después de que otro ya confirmó una migración
- **WHEN** finaliza el runner
- **THEN** MUST devolver error identificando el contexto sin registrar éxito global ni asumir rollback de los anteriores

## ADDED Requirements

### Requirement: Estado de migraciones consultable sin escritura

El backend SHALL ofrecer un modo one-shot `--estado-migraciones` con JSON estable que identifique contexto, migraciones disponibles/aplicadas/pendientes, destino seguro y compatibilidad. MUST no escribir en la base, crear histories/schemas ni abrir listener. MUST no incluir credenciales, connection strings ni secretos de storage. Estado incompatible MUST devolver error no cero.

#### Scenario: Consulta sobre base vacía o actualizada

- **GIVEN** una base vacía o compatible completamente migrada
- **WHEN** se consulta su estado
- **THEN** MUST informar respectivamente el baseline pendiente o ausencia de pendientes sin efectos sobre la base

#### Scenario: Historial incompatible

- **GIVEN** identificadores aplicados no reconocidos o un historial discontinuo
- **WHEN** se consulta el estado
- **THEN** MUST indicarse incompatibilidad y terminar con error sin writes

### Requirement: Preview SQL ligado al estado observado

El backend SHALL ofrecer `--script-migraciones <directorio>` para generar SQL exclusivamente del intervalo pendiente por contexto y un manifiesto con orden, identidad de release y estado observado. MUST usar recursos embebidos, no escribir en la base ni abrir listener, y rechazar rutas inseguras. MUST identificar ausencia de cambios y conservar las fronteras entre scripts/histories de contextos. El preview MUST NOT presentarse como comparación automática del schema real ni como script válido para cualquier estado posterior.

#### Scenario: Generación con cambios pendientes

- **GIVEN** una base compatible con migraciones pendientes
- **WHEN** se genera el preview
- **THEN** los archivos MUST representar sólo esas migraciones y el manifiesto MUST ordenar sus dependencias

#### Scenario: Base ya actualizada

- **GIVEN** ninguna migración pendiente
- **WHEN** se genera el preview
- **THEN** el manifiesto MUST indicar no-op sin inventar SQL de actualización

#### Scenario: Cambio de estado antes de aplicar

- **GIVEN** un preview generado para un historial observado
- **WHEN** el historial cambia antes de la aplicación
- **THEN** el despliegue MUST revalidar y no ejecutar ciegamente el artefacto anterior
