# Proposal

## Why

El historial SQL/EF de alpha conserva estructuras transitorias que ya no son necesarias para instalar el modelo vigente. Además, reconstruir bases y purgar objetos en cada deploy de staging/PR impide conservar sesiones de prueba; faltan controles de integridad, visibilidad de cambios pendientes y un backup integrado al despliegue.

## What Changes

- Mantener EF Core, PostgreSQL 18, SQL versionado embebido y el arranque one-shot `--migrate`; no incorporar motores de diff, servicios pagos ni otro gestor de migraciones.
- **BREAKING**: sustituir el historial de alpha por un baseline coherente del modelo final de Identity/Audit, Storage, Designaciones y Portal. Rechazar bases con historial anterior; no migrarlas ni destruirlas automáticamente. El usuario confirma que las instancias de despliegue aún deben crearse.
- Proveer scaffolding de SQL y wrapper EF mínimo, un catálogo verificable de recursos y controles de CI de referencias, empaquetado e inmutabilidad posterior al corte.
- Incorporar estado de migraciones y preview SQL de cambios pendientes, sin escritura ni listener HTTP, consumibles por workflows y operadores.
- **BREAKING**: preservar bases, buckets, adjuntos y datos de staging/PR entre redeploys; sembrar datos sintéticos sólo durante la inicialización y conservar teardown explícito/al cierre del PR.
- Integrar backup verificable antes de migrar una base existente, mantenimiento durante cambios pendientes, smoke tests y registro de éxito; retener backups privados fuera del runner efímero y conservarlos ante fallas.
- Serializar la sección de despliegue y teardown de cada ambiente, sin cancelar migraciones en curso. Conservar gates y acciones fijadas por SHA.
- Aprovechar `backup-storage.sh`/`restore-storage.sh` y adaptar sus límites en vez de duplicar procedimientos PostgreSQL/SeaweedFS. No reintroducir el reaper retirado por `develop`.
- Excluir separación de privilegios y ampliación general de la matriz de upgrades entre releases. Incluir sólo pruebas proporcionales de lo modificado y adaptación de tests afectados por el squash.

## Capabilities

### New Capabilities

Ninguna: se extienden contratos existentes.

### Modified Capabilities

- `migraciones-sql-versionado`: baseline consolidado, autoría asistida, trazabilidad de recursos e integridad del historial.
- `empaquetado-imagenes-app`: runner que incluye Storage, consulta de estado y generación de preview por contexto.
- `pipeline-deploy-ci`: despliegues incrementales con persistencia, backup, mantenimiento, verificación y serialización.
- `datos-ejemplo-no-productivos`: inicialización sintética única y conservación de modificaciones posteriores.
- `almacenamiento-adjuntos`: conservación de objetos en redeploy y recuperación consistente con PostgreSQL.

## Impact

- Afecta `database/`, migraciones/snapshots y registraciones de Shared, Storage, Designaciones y Portal; Aulas/Tareas conservan su participación sin baselines ficticios.
- Extiende el contrato transversal de migración sin exponer DbContexts al Host ni crear referencias entre implementaciones de módulos. El grafo permanece acíclico; no cambia la API HTTP ni la normativa institucional.
- Afecta CI, deploy prod/staging/PR, coordinación con teardown, scripts/Makefile de infraestructura, setup local, fixtures y documentación. El scope es exclusivamente el nuevo cambio; no se implementa ni se sincroniza todavía `rediseno-modelo-academico`.
- Se toma como referencia el estado posterior a `d267144`: producción Debian/principal en dominio raíz, staging/PR Proxmox/secundaria, SeaweedFS privado por host y ausencia de reaper.
- Conservar `PG*`, `APP_DB_PASSWORD_PROD`, `APP_DB_PASSWORD_STAGING`, `APP_DB_PASSWORD_PREVIEW`, `SEAWEEDFS_ROOT_ACCESS_KEY` y `SEAWEEDFS_ROOT_SECRET_KEY`, sus scopes actuales y credenciales aisladas de previews. No introducir otro inventario de secretos.
- Rollback del corte: revertir artefactos conjuntamente y recrear sólo instancias vacías o expresamente autorizadas. Tras iniciar persistencia, fallas se recuperan mediante backup y versión compatible; nunca con un reset automático ni suponiendo atomicidad global entre contextos.
