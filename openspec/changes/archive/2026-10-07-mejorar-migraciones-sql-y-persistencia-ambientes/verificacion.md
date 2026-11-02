# Verificación de implementación

## Resultado y alcance

Implementación local de `mejorar-migraciones-sql-y-persistencia-ambientes`, sin push, commits, despliegues compartidos ni resets de bases reales. La aprobación de apply fue explícita; el usuario pidió continuar sin preguntas. Sync/archive y cortes de ambientes reales no se ejecutaron dentro de esta fase.

Se conservan EF Core y SQL embebido. Historial alpha: 22 migraciones/35 SQL → cuatro baselines/23 recursos. Se consolidó el modelo realmente instalado, no un modelo inferido de documentación. Designaciones/Portal pasan a histories propios; Aulas/Tareas no reciben baselines ficticios. El adaptador común vive en `ArsDocendi.Migraciones`, fuera de Shared, evitando ciclos e I/O adicional de módulos en Shared.

## Evidencia por fase

| Fase               | Verificación real                                                                                                                                                                                                   |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Baseline           | Instalaciones antigua/nueva en PostgreSQL 18 desechable; dumps normalizados y cinco catálogos iguales. Inventario en `.artifacts/baseline/inventario.md`.                                                           |
| Autoría/integridad | 35 tests de tooling incluidos en la ejecución final Node; manifiesto de cuatro migraciones, 23 SQL y 27 archivos históricos. Scaffolder compilado en checkout desechable e imagen Docker de upgrade.                |
| Estado/preview     | Tests de histories desconocidos/discontinuos, schema previo sin historial, consulta sin writes, preview/no-op/orden/rutas; tar generado por imagen real.                                                            |
| Backup/recovery    | Ensayos reales PostgreSQL+SeaweedFS de filas, bytes, hashes, metadata, corrupción, destino poblado, retención de exitosos/fallidos y supervivencia al cliente; locks entre procesos.                                |
| Persistencia       | Tests de orquestación para prod/staging/PR, fallas backup/migrate/smoke, PR cerrado, seed único y rotación. Imagen candidata demuestra conservación de edición y adjunto después de una migración incremental real. |
| Integración        | Suite backend completa, Node completo, tres ensayos Docker, build imagen, Bash/Compose, formato, actionlint y OpenSpec estrictos.                                                                                   |

### Equivalencia del squash

`cmp -s .artifacts/baseline/schema-antiguo-normalizado.sql .artifacts/baseline/schema-baseline-normalizado.sql`: exit 0. SHA256 de ambos:

```text
739d4d2f65873e59f53c84b88e993f707523c90c0b7b381e4b9398027e02247e
```

Se verificó además igualdad de `catalogos-antiguos.json` y `catalogos-baseline.json`. La normalización excluye histories y ruido de pg_dump, no constraints/campos de negocio. Referencias históricas a `storage.archivos` eran lógicas, sin FK: no se inventaron FKs. Se preservó igualmente la ausencia histórica de FK simple en `designaciones.designaciones.materia_id`. El catálogo actual tiene 24 permisos. Los dos contenedores del ensayo de squash se eliminaron por nombre exacto después de conservar la evidencia; los fixtures posteriores limpian sólo sus propios recursos registrados.

### Resultados finales

| Comando/check                                                                                                    | Resultado                                                               |
| ---------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `dotnet build backend/ArsDocendi.slnx --no-restore --verbosity quiet`                                            | 0 errores, 0 warnings                                                   |
| `dotnet vstest .../ArsDocendi.IntegrationTests.dll --TestAdapterPath:...`                                        | 207 ejecutados, 207 aprobados, 0 fallidos/omitidos                      |
| `node --test scripts/tests/migrations.test.mjs infra/tests/*.test.mjs`                                           | 79 ejecutados/aprobados, 0 omitidos                                     |
| `INFRA_DOCKER_TESTS=1 node --test infra/tests/storage-recovery.integration.mjs infra/tests/lock.integration.mjs` | 2 ensayos reales aprobados                                              |
| `docker build -t arsdocendi-migraciones:verificacion -f backend/Dockerfile .`                                    | exit 0                                                                  |
| `INFRA_DOCKER_TESTS=1 node --test infra/tests/migration-candidate.integration.mjs`                               | 1 ensayo real aprobado                                                  |
| `--validar-recursos` desde Host e imágenes                                                                       | recursos compilados verificados, sin conexión DB                        |
| `node scripts/migrations/validate.mjs`                                                                           | cuatro migraciones/27 archivos históricos válidos                       |
| `pnpm format:check`                                                                                              | aprobado sobre repositorio, artefactos generados excluidos              |
| `bash -n infra/scripts/*.sh scripts/setup.sh`                                                                    | aprobado                                                                |
| Compose base/storage con env de ejemplo                                                                          | config exit 0                                                           |
| actionlint 1.7.7                                                                                                 | aprobado                                                                |
| `git diff --check`                                                                                               | aprobado                                                                |
| OpenSpec cambio estricto                                                                                         | aprobado                                                                |
| `openspec validate --all --strict`                                                                               | 48 aprobados, 0 fallidos; avisos informativos de longitud de requisitos |

Logs: `.artifacts/migraciones/{backend-final.log,backend-final.trx,node-final.log,infra-docker-final.log,candidato-e2e.log,docker-build.log,format-final.log,actionlint-final.log,openspec-final.log}`. Estos son resultados locales, no ejecución remota de GitHub Actions.

El ensayo candidato crea una migración adicional mediante el scaffolder en un checkout temporal: instala cuatro baselines, genera preview tar sin writes, siembra una vez, conserva una edición, obtiene backup conjunto, compila y aplica un índice nuevo, verifica datos/bytes, ejecuta no-op y consulta cuatro pings HTTP reales. No agrega esa migración al repositorio ni amplía la matriz de upgrades históricos. Las fallas backup/migración/smoke se verifican en las pruebas de orquestación; no se simula que esas pruebas desplegaron servidores reales.

## Correcciones durante integración

- El merge de develop dejaba el fake `IdentityDePrueba` sin `ListarMateriasPlanAsync`; se completó el contrato de prueba para permitir build. No se cambió la API de negocio.
- Dos tests retenían el contrato anterior de reset y mensajes de seguridad: se actualizó el ensayo al deploy persistente y se mantuvo la denegación explícita `PROHIBIDO`.
- Una preparación nueva intentó renombrar un rol de sistema inmutable. Se sustituyó por una carrera sintética válida; no se debilitó el trigger.
- `.artifacts/` queda excluido de Git/Prettier para no versionar ni formatear dumps, herramientas y checkouts temporales.

## Activación operativa pendiente, fuera del apply local

1. El baseline requiere instancias nuevas. Histories anteriores o schemas poblados no reconocidos se rechazan sin reset/stamp; cualquier recuperación/recreación requiere operación explícita separada.
2. Para el primer corte de historia, configurar Repository variable `CORTE_MIGRACIONES_REVISADO` con el SHA completo del commit revisado que contenga el baseline/manifiesto. El tooling verifica sus bytes contra Git confiable. Retirar la variable una vez integrado el corte; no admite repetirlo sobre una referencia que ya tenga manifiesto. No se inventó un SHA ni se creó un commit para rellenarlo.
3. Variables opcionales no secretas por Environment `prod`, `staging`, `pr-preview`: `BACKUP_VOLUME_PREFIX=arsdocendi-backups` y `BACKUP_RETENTION_DAYS=7` (defaults). Los scopes/nombres de `PG*`, `APP_DB_PASSWORD_*` y `SEAWEEDFS_ROOT_*` permanecen sin cambios.
4. No hay separación de privilegios, reaper, replicación, failover ni restore productivo automático. El volumen local de backup no sustituye una copia institucional fuera del host ni garantiza recuperar escrituras posteriores.
