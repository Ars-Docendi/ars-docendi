## Purpose

Pipeline de CI/CD en GitHub Actions que deploya los ambientes de la aplicación (prod, staging y pr-N efímeros), gestiona su teardown y maneja secrets de forma segura. Define cómo cada rama y pull request materializa, actualiza y destruye su ambiente, con énfasis en aislamiento, gating de código no confiable y datos no productivos en ambientes no-prod.

## Requirements

### Requirement: Deploy de prod y staging por rama

GitHub Actions SHALL deployar automáticamente el ambiente `prod` al hacer push/merge a `main`, y el ambiente `staging` al hacer push/merge a `develop`. Cada workflow SHALL construir las imágenes del frontend y backend, taggearlas de forma determinística (p. ej. por SHA), y materializar el ambiente correspondiente con su hostname, tag, nombre de ambiente y connection string. Un deploy de `prod` MUST NOT alterar `staging` ni viceversa.

#### Scenario: Merge a main deploya prod

- **WHEN** se mergea a `main`
- **THEN** el workflow construye las imágenes, las taggea por SHA y actualiza el ambiente `prod`
- **AND** `staging` y los ambientes `pr-N` quedan intactos

#### Scenario: Merge a develop deploya staging

- **WHEN** se mergea a `develop`
- **THEN** el workflow actualiza el ambiente `staging` con las imágenes recién construidas
- **AND** `prod` queda intacto

### Requirement: Deploy de ambiente pr-N gated por maintainer

Al abrir o sincronizar (open/synchronize) un pull request, el sistema SHALL poder construir y deployar el ambiente `pr-N` correspondiente, pero ese deploy MUST estar gated detrás de una acción explícita de un maintainer (label o aprobación). El código de un PR de fork MUST NOT ejecutarse en el runner self-hosted con acceso a secrets hasta que un maintainer habilite el gate. El workflow MUST NOT usar el patrón `pull_request_target` para exponer secrets a código no confiable de forks.

#### Scenario: PR sin gate no deploya

- **WHEN** se abre un PR (especialmente desde un fork) y ningún maintainer aplicó el label/aprobación de deploy
- **THEN** el ambiente `pr-N` no se construye ni deploya con secrets
- **AND** no se ejecuta código no confiable del PR en el runner con acceso a secrets

#### Scenario: PR habilitado por maintainer deploya

- **WHEN** un maintainer aplica el label/aprobación de deploy sobre el PR número N
- **THEN** el workflow construye las imágenes del PR y materializa el ambiente `pr-N` accesible en `pr-N.example.net`
- **AND** re-sincronizar el PR (nuevo push) re-deploya el mismo `pr-N`

### Requirement: Teardown de ambiente pr-N al cerrar el PR

Al cerrarse (merge o close) un pull request, el sistema SHALL destruir el ambiente `pr-N` por completo: sus contenedores **y** su base/schema. El teardown MUST ser idempotente (cerrar un PR cuyo ambiente ya fue destruido no debe fallar el workflow).

#### Scenario: Cierre de PR destruye el ambiente y su base

- **WHEN** se cierra o mergea el PR número N
- **THEN** el workflow destruye los contenedores de `pr-N` y elimina su base/schema
- **AND** el hostname `pr-N.example.net` deja de resolver a un contenedor

#### Scenario: Teardown idempotente

- **WHEN** se dispara el teardown de un `pr-N` cuyo ambiente ya no existe
- **THEN** el workflow termina con éxito sin error
- **AND** no deja recursos residuales

### Requirement: Runners self-hosted efímeros

Los jobs que ejecutan código de PR SHALL correr en runners self-hosted **efímeros**: cada job MUST partir de un runner de estado limpio y el runner MUST descartarse tras completar el job, sin reutilizar workspace, caché de credenciales ni estado entre PRs. Esto MUST garantizar que código de un PR no pueda observar ni persistir artefactos o secrets de otro.

#### Scenario: Runner limpio por job

- **WHEN** dos PRs distintos disparan deploys gated en secuencia
- **THEN** cada uno corre en un runner efímero de estado limpio
- **AND** el segundo job no tiene acceso al workspace ni a credenciales del primero

### Requirement: Manejo de secrets fuera del repo e imágenes

Ningún secreto (credenciales del Cloudflare Tunnel, credenciales admin de Postgres, registry creds, tokens del runner) SHALL committearse al repositorio ni hornearse en capas de imagen. Los secrets SHALL provenir de GitHub Actions secrets e inyectarse en runtime como variables de entorno. El repositorio SHALL incluir un `.env.example` y la lista documentada de secrets requeridos, sin valores reales.

#### Scenario: Sin secretos en repo ni imagen

- **WHEN** se revisa el repositorio y las imágenes construidas
- **THEN** no hay secretos en archivos versionados ni en capas de imagen
- **AND** los secretos se inyectan en runtime desde GitHub Actions secrets

#### Scenario: .env.example documenta lo requerido

- **WHEN** un operador nuevo prepara el deploy
- **THEN** encuentra en `.env.example` y el runbook la lista completa de variables y secrets requeridos
- **AND** ninguno contiene un valor real

### Requirement: Datos no productivos en ambientes no-prod

Los workflows que aprovisionan bases para `staging` y `pr-N` SHALL sembrarlas con datos sintéticos o un snapshot anonimizado. Un workflow MUST NOT copiar datos productivos reales a un ambiente `staging` o `pr-N`.

#### Scenario: Seed anonimizado en pr-N

- **WHEN** el deploy gated de `pr-N` aprovisiona su base
- **THEN** la base se siembra con datos sintéticos o anonimizados
- **AND** no se ejecuta ninguna copia de la base de `prod`

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

### Requirement: Referencias inmutables de acciones de CI

Todos los workflows de GitHub Actions SHALL referenciar cada acción externa mediante un SHA completo de 40 caracteres. Ningún tag o rama mutable SHALL quedar como referencia ejecutable en CI, deploy o teardown.

#### Scenario: Workflow con acciones fijadas

- **GIVEN** un workflow versionado en el repositorio
- **WHEN** se revisan sus pasos `uses`
- **THEN** cada referencia termina en un SHA completo de 40 caracteres y no en un tag o rama

#### Scenario: Actualización de una acción

- **GIVEN** que se decide actualizar una acción
- **WHEN** se modifica su referencia
- **THEN** el cambio conserva una referencia completa a un commit verificable y no reintroduce un tag mutable

### Requirement: Política de instalación de dependencias del pipeline

La configuración de pnpm del workspace SHALL bloquear subdependencias exóticas, esperar al menos siete días antes de aceptar una versión recién publicada y rechazar downgrades de la política de confianza. Los valores SHALL ser `blockExoticSubdeps: true`, `minimumReleaseAge: 10080` y `trustPolicy: no-downgrade`.

#### Scenario: Configuración de supply chain presente

- **WHEN** el pipeline instala dependencias del workspace
- **THEN** la configuración efectiva contiene las tres políticas con esos valores

#### Scenario: Dependencia recientemente publicada

- **GIVEN** una versión publicada hace menos de 10080 minutos
- **WHEN** pnpm resuelve la instalación
- **THEN** la versión no se acepta por la política de antigüedad mínima
