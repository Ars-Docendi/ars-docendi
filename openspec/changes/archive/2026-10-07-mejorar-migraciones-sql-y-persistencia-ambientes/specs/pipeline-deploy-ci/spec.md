## RENAMED Requirements

- FROM: `### Requirement: Reinicio de bases descartables en cada despliegue`
- TO: `### Requirement: Actualización incremental de ambientes persistentes`

## MODIFIED Requirements

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

## ADDED Requirements

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
