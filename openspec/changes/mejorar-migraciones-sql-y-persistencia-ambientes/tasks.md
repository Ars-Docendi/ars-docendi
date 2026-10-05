# Tasks

## 1. Baseline consolidado del estado post-develop

- [ ] 1.1 Inventariar migraciones, tablas de historial, SQL y dependencias Identity/Audit→Storage→Designaciones/Portal; verificar el inventario contra el código actual y documentar coordinación con `rediseno-modelo-academico` sin modificar ese cambio.
- [ ] 1.2 Construir SQL del modelo final por responsabilidad y un baseline EF por contexto con schema actual; verificar equivalencia antigua/nueva en PostgreSQL 18 desechable, catálogos, funciones, triggers, índices, constraints y referencias a `storage.archivos`.
- [ ] 1.3 Reemplazar migraciones/snapshots alpha y adaptar fixtures/tests que apuntan a IDs retirados; verificar suite existente conservando assertions de dominio vigentes y documentando exclusivamente transiciones retiradas.
- [ ] 1.4 Agregar preflight de histories desconocidos/discontinuos y schemas existentes sin historial; verificar tests de rechazo sin writes y de base vacía compatible antes de aplicar.
- [ ] 1.5 Actualizar `database/README.md`, `data-model.md` y referencias de setup/bootstrap; verificar que documentación y SQL describan el mismo modelo final y no indiquen editar scripts ya aplicados.

## 2. Autoría asistida e integridad en CI

- [ ] 2.1 Declarar metadata única de recursos ordenados por migración y usarla en ejecución/inventario; verificar tests de rutas inexistentes, duplicadas y orden dependiente sin duplicar el DDL.
- [ ] 2.2 Implementar scaffolder con contexto/nombre, dry-run y protección de colisiones; verificar tests antes de implementación y generar/buildar una migración de prueba en workspace desechable sin conectar bases.
- [ ] 2.3 Implementar checks de recursos referenciados/embebidos y SQL huérfano con excepciones explícitas; verificar que fixtures corruptos fallen y la imagen publicada contenga recursos correctos.
- [ ] 2.4 Fijar el nuevo inventario baseline protegido y comparar contra una referencia Git confiable en CI; verificar cambios/eliminaciones históricas rechazados y adiciones aceptadas, sin dejar bypass genérico del squash.
- [ ] 2.5 Integrar checks en selección de CI para cambios SQL, wrappers y tooling; verificar workflow con actionlint/SHA pinning y documentar comandos reales de autoría e integridad.

## 3. Estado y preview de migraciones

- [ ] 3.1 Extender contrato transversal con resultados puros de estado/preview sin exponer DbContexts; verificar build y pruebas de fronteras/dependencias del Host.
- [ ] 3.2 Implementar `--estado-migraciones` con JSON estable y errores incompatibles; verificar consultas de base vacía/migrada, ausencia de writes/listener y redacción de credenciales.
- [ ] 3.3 Implementar `--script-migraciones <directorio>` para intervalos pendientes por contexto y manifiesto ordenado ligado a SHA/estado; verificar base vacía, no-op, rutas inseguras y composición Identity/Audit→Storage→consumidores.
- [ ] 3.4 Generar preview bajo lock desde la misma imagen candidata y revalidar antes de `--migrate`; verificar que estado cambiado o desconocido bloquee publicación y no use un script obsoleto.
- [ ] 3.5 Documentar formatos, exit codes, orden y diferencia con schema diff; verificar ejemplos contra el runner real y que la aplicación normal no migre al arrancar.

## 4. Backup y recuperación integrados

- [ ] 4.1 Adaptar backup conjunto existente para publicación completa verificable y volumen persistente por ambiente mediante stdio; verificar fallas pg_dump/objetos, checksums y ausencia de dependencias de bind mounts.
- [ ] 4.2 Añadir configuración/retención de backups exitosos con `BACKUP_VOLUME_PREFIX` y `BACKUP_RETENTION_DAYS`; verificar validación de inputs, aislamiento, persistencia tras runner efímero y conservación de backups fallidos.
- [ ] 4.3 Integrar backup antes de migraciones pendientes sobre bases existentes con parada de escritores; verificar que backup fallido impida migrar/publicar y que base nueva/no-op no generen backups ficticios.
- [ ] 4.4 Mantener restore directo de prod prohibido y definir recovery en destino aislado del mismo host con corte aprobado; verificar restauración real sintética, hashes/metadata y rechazo de destino poblado o no autorizado.
- [ ] 4.5 Actualizar runbooks, Makefile y ejemplos de vars/scopes sin nuevos secretos; verificar documentación de retención, protección de respaldos, recuperación productiva y credenciales `SEAWEEDFS_ROOT_*` existentes.

## 5. Persistencia de staging/PR e inicialización única

- [ ] 5.1 Retirar drop/down con volúmenes/purge del deploy ordinario conservando provisioning idempotente; verificar `infra/tests/spin-up.test.mjs` actualizado y preservación de filas/bucket/objetos en redeploy aislado.
- [ ] 5.2 Implementar autorización first-run y marcador de seed en la transacción del dataset, transmitiendo SQL por stdin; verificar edición posterior conservada, seed fallido reintentable y base poblada sin marca rechazada sin sobrescritura.
- [ ] 5.3 Coordinar rotación de credenciales SeaweedFS PR con mantenimiento sin purgar bucket; verificar acceso a un adjunto existente tras redeploy y aislamiento entre previews.
- [ ] 5.4 Serializar deploy/teardown/manuales por ambiente y evitar cancelación en curso de PR; verificar workflows, pruebas de exclusión/cierre de PR y que gates/destinos principal/secundaria permanezcan intactos.
- [ ] 5.5 Incorporar smoke de SHA, pings, DB sólo lectura y ausencia de pendientes antes de registrar/comentar éxito; verificar fallas sin éxito aparente, backend detenido y backups conservados.
- [ ] 5.6 Conservar teardown explícito/al cierre y reset manual protegido, sin reintroducir reaper; verificar scripts, prueba de protección prod y documentación de ciclo de vida de previews persistentes.
- [ ] 5.7 Actualizar specs operativas en docs, README/setup y runbook de PR; verificar que no prometan reseed/reset en cada deploy ni copien SGA/PII a staging/PR.

## 6. Verificación integrada y entrega

- [ ] 6.1 Ejecutar suite backend descubierta, pruebas infra/storage pertinentes, build de imagen y checks de formato/workflows; registrar comandos, conteos reales y blockers sin omitir tests para aparentar éxito.
- [ ] 6.2 Ejercitar con recursos aislados el recorrido completo instalación→seed→redeploy conservando datos/adjuntos→no-op y fallas backup/migración/smoke; verificar cleanup limitado a recursos de prueba creados y receipt sólo en éxito.
- [ ] 6.3 Ejecutar `git diff --check`, validación estricta del cambio y validación OpenSpec general; registrar resultados y confirmar que no se implementó separación de privilegios, reaper ni matriz general de upgrades.
- [ ] 6.4 Entregar evidencia y solicitar aprobación independiente para sync/archive y cualquier acción sobre ambientes reales; verificar que no hubo push ni resets de bases durante la implementación local.
