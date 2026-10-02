## ADDED Requirements

### Requirement: CI de backend selecciona cambios relevantes y conserva la suite completa

El pipeline de CI MUST ejecutar el job Backend cuando cambie código bajo `backend/**`, SQL o migraciones bajo `database/**`, o la configuración del SDK definida por `global.json`. Un cambio compuesto exclusivamente por archivos de pnpm, documentación Markdown o archivos del frontend MUST NOT activar el job Backend. Cuando se active, el job MUST ejecutar el conjunto completo de tests backend descubierto por el proyecto, sin omitir casos para reducir el tiempo.

#### Scenario: Cambio de backend o database

- **WHEN** un commit modifica un archivo de `backend/**` o `database/**`
- **THEN** el job Backend se ejecuta
- **AND** la ejecución incluye todos los tests backend descubiertos por el proyecto

#### Scenario: Cambio exclusivo de frontend o pnpm

- **WHEN** un commit modifica únicamente archivos bajo `frontend/**`, `package.json`, `pnpm-workspace.yaml` o `pnpm-lock.yaml`
- **THEN** el job Backend no se ejecuta
- **AND** el job Frontend conserva su selección existente

#### Scenario: Cambio documental de backend

- **WHEN** un commit modifica únicamente documentación Markdown bajo `backend/**` o `database/**`
- **THEN** el job Backend no se ejecuta

### Requirement: La suite de integración aprovecha el aislamiento de datos

La ejecución de integración MUST permitir concurrencia entre grupos de tests que usan bases aisladas, sin depender de un orden global. MUST conservar el aislamiento entre bases y la cobertura completa de la suite. En el entorno de referencia usado para comparar el cambio, la mediana de tres ejecuciones de la fase de tests MUST reducirse al menos 25% respecto de la línea base de 3m29s, manteniendo el mismo conjunto de tests exitosos.

#### Scenario: Clases con bases aisladas se ejecutan en paralelo

- **GIVEN** dos grupos de tests que crean y destruyen bases independientes en el mismo PostgreSQL de pruebas
- **WHEN** el CI ejecuta la suite backend
- **THEN** ambos grupos pueden avanzar concurrentemente
- **AND** ningún grupo observa datos, migraciones o conexiones pertenecientes al otro

#### Scenario: La optimización no elimina cobertura

- **GIVEN** la suite backend que constituye la línea base
- **WHEN** se ejecutan tres mediciones comparables después del cambio
- **THEN** la mediana de la fase de tests es al menos 25% menor que la línea base
- **AND** todos los tests descubiertos en la línea base siguen ejecutándose y aprobando

### Requirement: CI reutiliza dependencias NuGet de forma segura

El job Backend MUST reutilizar los paquetes NuGet restaurados cuando no cambien el sistema operativo del runner, el SDK ni los manifiestos de proyectos o lockfiles. La clave del cache MUST incorporar esos inputs y un miss, una clave obsoleta o un fallo de restauración del cache MUST permitir que `dotnet restore` continúe normalmente. El cache MUST estar referenciado mediante acciones fijadas a commits completos.

#### Scenario: Cache válido de dependencias

- **GIVEN** una ejecución con el mismo SDK y los mismos manifiestos de proyectos que una ejecución anterior
- **WHEN** comienza el restore del backend
- **THEN** el job puede reutilizar los paquetes NuGet previamente restaurados
- **AND** no descarga nuevamente los paquetes que siguen siendo válidos

#### Scenario: Cache miss o inválido

- **GIVEN** que cambió un manifiesto, un lockfile o no existe una entrada compatible
- **WHEN** comienza el restore del backend
- **THEN** se ejecuta un restore completo
- **AND** el job no falla sólo por la ausencia o invalidez del cache

## MODIFIED Requirements

### Requirement: Deploy de prod y staging por rama

GitHub Actions SHALL deployar automáticamente el ambiente `prod` al hacer push/merge a `main`, y el ambiente `staging` al hacer push/merge a `develop`, siempre que el cambio incluya código, infraestructura, base de datos o configuración de dependencias que pueda modificar el ambiente. Un cambio compuesto exclusivamente por documentación Markdown MUST NOT iniciar un build ni un deploy. Cada workflow SHALL construir las imágenes del frontend y backend, taggearlas de forma determinística (p. ej. por SHA), y materializar el ambiente correspondiente con su hostname, tag, nombre de ambiente y connection string. Un deploy de `prod` MUST NOT alterar `staging` ni viceversa.

#### Scenario: Merge a main deploya prod

- **WHEN** se mergea a `main` con un cambio desplegable
- **THEN** el workflow construye las imágenes, las taggea por SHA y actualiza el ambiente `prod`
- **AND** `staging` y los ambientes `pr-N` quedan intactos

#### Scenario: Merge a develop deploya staging

- **WHEN** se mergea a `develop` con un cambio desplegable
- **THEN** el workflow actualiza el ambiente `staging` con las imágenes recién construidas
- **AND** `prod` queda intacto

#### Scenario: Cambio sólo documental no deploya

- **WHEN** se hace push o merge a `main` o `develop` modificando únicamente documentación Markdown
- **THEN** no se construyen imágenes ni se actualiza `prod` o `staging`

### Requirement: Deploy de ambiente pr-N gated por maintainer

Al abrir o sincronizar (open/synchronize) un pull request con cambios desplegables, el sistema SHALL poder construir y deployar el ambiente `pr-N` correspondiente, pero ese deploy MUST estar gated detrás de una acción explícita de un maintainer (label o aprobación). Un pull request que modifique únicamente documentación Markdown MUST NOT iniciar el build ni el deploy de `pr-N`. El código de un PR de fork MUST NOT ejecutarse en el runner self-hosted con acceso a secrets hasta que un maintainer habilite el gate. El workflow MUST NOT usar el patrón `pull_request_target` para exponer secrets a código no confiable de forks.

#### Scenario: PR sin gate no deploya

- **WHEN** se abre un PR (especialmente desde un fork) y ningún maintainer aplicó el label/aprobación de deploy
- **THEN** el ambiente `pr-N` no se construye ni deploya con secrets
- **AND** no se ejecuta código no confiable del PR en el runner con acceso a secrets

#### Scenario: PR habilitado por maintainer deploya

- **WHEN** un maintainer aplica el label/aprobación de deploy sobre el PR número N con cambios desplegables
- **THEN** el workflow construye las imágenes del PR y materializa el ambiente `pr-N` accesible en `pr-N.example.net`
- **AND** re-sincronizar el PR (nuevo push) re-deploya el mismo `pr-N`

#### Scenario: PR sólo documental no deploya

- **WHEN** un PR modifica únicamente documentación Markdown
- **THEN** el workflow no construye imágenes ni materializa `pr-N`
- **AND** el gate de maintainer y las protecciones contra código no confiable permanecen vigentes para PRs con cambios desplegables
