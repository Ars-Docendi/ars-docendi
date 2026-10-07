## MODIFIED Requirements

### Requirement: Operación, backup y recuperación

La infraestructura SHALL provisionar SeaweedFS con volumen persistente, credenciales inyectadas en runtime y procedimientos versionados para crear buckets, políticas y backups. Deploys ordinarios de staging/pr-N MUST conservar buckets, objetos y metadata; su reconstrucción SHALL quedar reservada a inicialización o reset explícito autorizado. Antes de migrar una base existente SHALL crearse un backup conjunto verificable de PostgreSQL y objetos, con escritores conocidos detenidos. Producción MUST tener un backup verificable y una prueba documentada de recuperación antes de considerarse operativa. La recuperación SHALL mantener correspondencia metadata/bytes y aislamiento por ambiente/host; MUST NOT restaurar automáticamente sobre un destino activo o llevar datos productivos a Proxmox/no-prod.

#### Scenario: Provisionamiento repetible

- **WHEN** se aprovisiona un ambiente nuevo
- **THEN** se crean su bucket, política y credenciales de mínimo privilegio sin almacenar secretos en el repositorio

#### Scenario: Restauración de un backup

- **GIVEN** un backup válido de objetos y metadata del ambiente
- **WHEN** se restaura sobre un destino de recuperación aislado autorizado
- **THEN** los archivos disponibles vuelven a ser descargables con metadata e integridad verificables

#### Scenario: Fallo de almacenamiento

- **GIVEN** SeaweedFS no está disponible durante una mutación
- **WHEN** el backend intenta confirmar o asociar un archivo
- **THEN** la operación falla explícitamente, no confirma la mutación ni deja el pedido aparentando documentación disponible

#### Scenario: Redeploy conserva adjuntos

- **GIVEN** un preview o staging con adjuntos disponibles
- **WHEN** se actualiza su aplicación o se rota su credencial durante mantenimiento
- **THEN** MUST conservarse su bucket/objetos y la nueva aplicación MUST poder descargar los adjuntos autorizados

#### Scenario: Backup incompleto

- **GIVEN** un snapshot cuyo dump, objetos o checksums no pudieron completarse
- **WHEN** se verifica antes de migrar
- **THEN** MUST rechazarse como recuperación válida y bloquearse el deploy

## ADDED Requirements

### Requirement: Persistencia privada y retención de backups de despliegue

Los backups SHALL persistir en almacenamiento privado separado del workspace efímero, repo e imágenes, con identificación de ambiente, release e integridad. MUST no publicarse como artefactos/comentarios de PR ni exponerse mediante hostnames de usuario. SHALL aplicarse retención configurable sólo a backups completos de despliegues exitosos vencidos del mismo ambiente. Backups de fallas MUST conservarse hasta eliminación explícita; el teardown del preview MUST NOT borrar implícitamente sus respaldos. Un fallo de almacenamiento o de verificación MUST no producir un recibo de backup completo.

#### Scenario: Runner descartado

- **GIVEN** un deploy produjo un backup completo
- **WHEN** se descarta su runner efímero
- **THEN** el backup MUST seguir disponible para recuperación autorizada en el host correspondiente

#### Scenario: Retención después de éxito

- **GIVEN** backups exitosos vencidos, recientes y fallidos
- **WHEN** se aplica la política de retención
- **THEN** MUST eliminar sólo los exitosos vencidos del ambiente indicado
- **AND** MUST conservar los recientes, fallidos y de otros ambientes

#### Scenario: Teardown del preview

- **GIVEN** un preview con respaldos retenidos
- **WHEN** se elimina su aplicación, base y bucket activo
- **THEN** MUST preservarse el almacenamiento de backups hasta su retención autorizada
