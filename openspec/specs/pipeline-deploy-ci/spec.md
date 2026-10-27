## Purpose

Pipeline de CI/CD en GitHub Actions que deploya los ambientes de la aplicación (prod, staging y pr-N efímeros), gestiona su teardown y maneja secrets de forma segura. Define cómo cada rama y pull request materializa, actualiza y destruye su ambiente, con énfasis en aislamiento, gating de código no confiable y datos no productivos en ambientes no-prod.

## Requirements

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

### Requirement: Deploy de prod y staging por rama

GitHub Actions SHALL desplegar `prod` al hacer push/merge a `main` únicamente en Debian y `staging` al hacer push/merge a `develop` únicamente en Proxmox cuando haya código, infraestructura, SQL o dependencias desplegables. Un cambio exclusivamente documental Markdown MUST NOT iniciar build ni deploy. Cada workflow SHALL construir/publicar frontend y backend etiquetados por SHA y ejecutar un único deploy en el host autorizado; MUST NOT distribuir el mismo ambiente a ambos hosts. Producción SHALL publicarse bajo el dominio raíz conservando el Environment `prod`. Un deploy de producción MUST NOT alterar staging/previews y viceversa.

#### Scenario: Merge a main deploya prod

- **WHEN** se mergea a `main` con un cambio desplegable
- **THEN** se construyen las imágenes por SHA y se despliega `prod` únicamente en Debian para el dominio raíz
- **AND** staging y previews de Proxmox quedan intactos

#### Scenario: Merge a develop deploya staging

- **WHEN** se mergea a `develop` con un cambio desplegable
- **THEN** staging se actualiza únicamente en Proxmox
- **AND** producción Debian queda intacta

#### Scenario: Cambio sólo documental no deploya

- **WHEN** se hace push/merge a `main` o `develop` modificando sólo Markdown
- **THEN** no se construyen imágenes ni se despliega producción o staging

#### Scenario: Destino indisponible

- **WHEN** el runner principal no está disponible para un deploy de producción
- **THEN** el job no se ejecuta accidentalmente en el runner secundario
- **AND** no se modifica la ubicación de producción como mecanismo de recuperación automática

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

### Requirement: Teardown de ambiente pr-N al cerrar el PR

Al cerrarse un PR, el sistema SHALL destruir en Proxmox sus contenedores, base/schema y objetos/credenciales asociados, usando scripts de la rama base confiable. El teardown MUST ser idempotente y MUST NOT ejecutarse en Debian ni borrar recursos productivos.

#### Scenario: Cierre de PR destruye el ambiente y su base

- **WHEN** se cierra o mergea el PR número N
- **THEN** el runner confiable secundario elimina su ambiente, base y objetos en Proxmox
- **AND** `pr-N.example.net` deja de resolver a un contenedor
- **AND** producción Debian no se modifica

#### Scenario: Teardown idempotente

- **WHEN** se solicita eliminar un preview ya inexistente
- **THEN** termina sin error ni recursos residuales
- **AND** no altera producción, staging u otros previews

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

### Requirement: Actualización incremental de ambientes persistentes

El despliegue ordinario de staging y pr-N SHALL conservar su base, datos, auditoría, idempotencia, bucket y objetos mientras exista el ambiente. SHALL aprovisionar sólo recursos faltantes y aplicar migraciones pendientes con el mismo mecanismo que prod. MUST NOT ejecutar drop, purge, eliminación de volúmenes ni reseed para actualizar la aplicación. Un reset SHALL requerir una operación explícita protegida distinta del deploy y MUST NOT admitir prod. El teardown al cierre del PR SHALL mantener su destrucción completa y aislamiento existentes; MUST NOT reintroducirse reaper. Los gates, topología Debian/Proxmox, runners efímeros y scopes existentes MUST conservarse.

#### Scenario: Segundo despliegue sobre el mismo ambiente

- **GIVEN** staging o pr-N con datos y adjuntos modificados desde la aplicación
- **WHEN** se despliega nuevamente
- **THEN** MUST conservarse esos cambios y objetos, salvo transformaciones explícitas de migraciones nuevas
- **AND** MUST NOT reinicializarse el dataset

#### Scenario: Protección de producción y ambientes vecinos

- **GIVEN** prod, staging y varios previews
- **WHEN** se actualiza o resetea explícitamente un preview
- **THEN** sólo ese destino MUST ser afectado y un intento de reset de prod MUST rechazarse antes de eliminar recursos

#### Scenario: Falla de migración o seed

- **GIVEN** un ambiente que está actualizándose o inicializándose
- **WHEN** falla una migración o la siembra inicial
- **THEN** MUST fallar el despliegue sin servir la versión nueva sobre estado parcial
- **AND** MUST conservarse recuperación/diagnóstico sin reset automático en el reintento

#### Scenario: Despliegues concurrentes del mismo ambiente

- **GIVEN** ejecuciones de deploy, teardown o mantenimiento sobre el mismo ambiente
- **WHEN** alcanzan su sección crítica
- **THEN** MUST serializarse sin cancelar ni intercalar una migración/backup en curso
- **AND** otros ambientes MUST mantener aislamiento

#### Scenario: PR cerrado durante un despliegue pendiente

- **GIVEN** un PR cerrado antes de comenzar el deploy efectivo
- **WHEN** se revalida su ciclo de vida
- **THEN** MUST omitirse la publicación y mantenerse el teardown como operación final autorizada

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

### Requirement: Asignación determinística de runners por host

El deploy de producción SHALL usar `self-hosted, arsdocendi, confiable, principal`; staging y teardown de previews SHALL usar `self-hosted, arsdocendi, confiable, secundaria`. El build/deploy de previews SHALL usar `self-hosted, arsdocendi, efimero, secundaria` y conservar el label de maintainer y la aprobación del Environment `pr-preview`, sin usar `pull_request_target`. Las ubicaciones MUST NOT depender de una selección aleatoria entre runners con etiquetas genéricas.

#### Scenario: Un destino confiable por ambiente

- **WHEN** se solicitan despliegues de producción y staging
- **THEN** producción selecciona sólo Debian y staging sólo Proxmox
- **AND** no hay matriz de hosts para ninguno de los dos ambientes

#### Scenario: Preview gated sólo en Proxmox

- **WHEN** un PR pasa los gates de maintainer y Environment
- **THEN** su build/deploy se ejecuta en el efímero secundario
- **AND** al cerrarse, su teardown usa el confiable secundario

### Requirement: Secretos de GitHub conservados por ambiente

Los jobs SHALL consumir las variables/secrets existentes de GitHub del repositorio y del Environment correspondiente, inyectados en runtime sin un segundo inventario local de credenciales de aplicación. MUST NOT exponerlos en logs, archivos versionados o imágenes. El cambio de hostname productivo MUST NOT renombrar el Environment `prod` ni alterar silenciosamente los scopes autorizados.

#### Scenario: Producción con el scope existente

- **WHEN** se despliega `prod` bajo el dominio raíz de Debian
- **THEN** usa `APP_DB_PASSWORD_PROD` de `prod` y `PG*`/`SEAWEEDFS_ROOT_*` del repositorio
- **AND** su conexión y operaciones de storage se dirigen a las instancias locales de Debian

#### Scenario: Preview sin aprobación

- **WHEN** falta la aprobación de `pr-preview`
- **THEN** el job no accede a sus secretos de Environment ni despliega

### Requirement: Despliegue protegido por backup y verificación

Cada ambiente SHALL consultar compatibilidad y pendientes bajo exclusión antes de migrar. Con pendientes sobre una base existente SHALL detener escritores conocidos y crear un backup conjunto DB/objetos verificable antes del primer cambio. Un backup fallido MUST impedir migración/publicación. Una base nueva SHALL registrar ausencia de estado previo; sin pendientes SHALL omitirse el backup pre-migración. El éxito SHALL requerir la imagen esperada por SHA, pings de módulos, comprobación DB sólo lectura y ausencia de pendientes. MUST conservarse backup y diagnóstico ante fallas; MUST NOT restaurarse automáticamente sobre una base activa ni reiniciarse una versión vieja sobre schema posiblemente incompatible.

#### Scenario: Backup fallido

- **GIVEN** migraciones pendientes en una base existente
- **WHEN** falla el backup o su verificación
- **THEN** MUST abortarse antes de aplicar migraciones o publicar la nueva versión

#### Scenario: Instalación nueva o redeploy sin pendientes

- **GIVEN** una base nueva o una base existente sin migraciones pendientes
- **WHEN** se prepara el deploy
- **THEN** MUST registrarse ese caso sin exigir un backup ficticio ni una migración nueva

#### Scenario: Smoke test fallido

- **GIVEN** migraciones aplicadas y aplicación candidata iniciada
- **WHEN** falla una comprobación requerida
- **THEN** MUST detenerse el backend candidato, conservar backup y no registrar ni comentar éxito

#### Scenario: Despliegue exitoso

- **GIVEN** todas las comprobaciones requeridas aprobadas
- **WHEN** se registra el resultado
- **THEN** MUST vincularse ambiente, SHA, historial resultante y backup si existe
- **AND** la retención MUST operar sólo sobre respaldos exitosos vencidos del mismo ambiente

### Requirement: Preview e integridad como artefactos de despliegue

CI SHALL verificar asociación SQL/migración, empaquetado e inmutabilidad del inventario protegido. El deploy SHALL generar estado y preview desde la imagen candidata bajo lock y mantener artefactos identificados por release/estado. MUST no publicar backups, credenciales ni datos de usuarios en artefactos o comentarios de PR. Las referencias de acciones SHALL seguir fijadas por SHA y el acceso a secrets SHALL mantenerse detrás de los gates actuales.

#### Scenario: Historia alterada

- **WHEN** CI detecta un recurso protegido modificado o un script sin consumidor explícito
- **THEN** MUST fallar antes de desplegar

#### Scenario: Preview revisable

- **GIVEN** una imagen candidata autorizada
- **WHEN** se genera su SQL pendiente
- **THEN** MUST poder revisarse con su manifiesto sin exponer backup ni secretos
