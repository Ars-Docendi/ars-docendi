# Ensayos de instalación, upgrade y recuperación — implementación

## Estado de aceptación

**8.1/8.2 verificadas con el candidato actual**, compilado en Release desde el working tree y ejecutado en recursos desechables. Esto no es una imagen oficial ni un despliegue. El ensayo baseline-only previo (1 pass, 1 skip) no se cuenta; la corrida actual incluyó el candidato y terminó sin skips.

## Harness durable

- `scripts/tests/upgrade-post-auditoria.test.mjs` y `scripts/tests/fixtures/upgrade-post-auditoria/{baseline.mjs,harness.mjs,candidato.mjs,cleanup.test.mjs,compatible.sql,README.md}`.
- SHA histórico exacto: `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`; checkout desechable mediante `git archive` dentro de `.artifacts/upgrade-post-auditoria/<uid>/anterior`.
- Compilación/publish Release y ejecución de `ArsDocendi.Host.dll` en imagen Docker propia, identificada por su ID. No se usan mocks ni replay de archivos SQL como sustituto del runner.
- PostgreSQL 18.4 y SeaweedFS reales; SQL/objetos/dumps/build context por stdio, sin bind mounts, puertos expuestos ni acceso a una base compartida. Credenciales generadas exclusivamente para el fixture; no se lee `.env` ni SGA.
- Comparación de todas las tablas originales, histories y eventos legacy mediante JSON ordenado, conteos y hashes. Se verifican IDs/vínculos OID/TID/persona, roles/permisos, snapshot del pedido, referencias compartidas al CV y bytes/tamaño/MIME/hash del PDF sintético.

## Resultado final ejecutado

Corrida actual `UPGRADE_DOCKER_TESTS=1 UPGRADE_CANDIDATE_IMAGE=arsdocendi-post-auditoria-candidate:local-20261014 node --test scripts/tests/upgrade-post-auditoria.test.mjs`: **2 tests efectivos, 2 passed, 0 failed, 0 skipped**, exit 0 (234,2 s). La salida TAP fue observada en el proceso ejecutado; evidencia detallada persistida en `.artifacts/upgrade-post-auditoria/36fbc65b1cd06c56/resultado.json`. El ensayo baseline-only anterior, con un subtest candidato omitido, queda sustituido y no se usa para esta aceptación.

Registro real: `.artifacts/upgrade-post-auditoria/36fbc65b1cd06c56/resultado.json`:

- Publish histórico del SHA `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b` y `--validar-recursos`: exit 0; ID de imagen `sha256:89d37128d9f39bc9a3ae6e30f185166daea2623404d69000b201e95be5b5a2c2`.
- El candidato actual se compiló con `dotnet publish -c Release` y construyó localmente como `arsdocendi-post-auditoria-candidate:local-20261014`, ID `sha256:1a4aac635af8cbfa496a7d2cadd063974ea97629b77140e05334d4df2b090b37`. Su CLI real pasó `--validar-recursos`, estado, preview binario, diagnóstico read-only, upgrade incremental desde `f10e0a5`, no-op y migración en base nueva.
- Tooling/historia: `node --test scripts/tests/migrations.test.mjs` **38/38 PASS**; `node scripts/migrations/validate.mjs --base-ref f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b` reportó **24 migraciones, 67 archivos, protección Git activa**. Comparación byte-a-byte de los **37 SQL/wrappers históricos** contra el SHA confiable: cero diferencias. El publish Release del candidato terminó exit 0.
- La instalación/ensayo histórico preservó **45 tablas** e **28 eventos de auditoría legacy**; el candidato comparó filas/columnas anteriores, snapshots y secuencias antes/después del upgrade, y el estado completo entre aplicaciones no-op.
- PDF conservado: **63 bytes**, `application/pdf`, SHA-256 `45fe9e4a0ea3765131a3b124ed7447bc048135f0aeac073d0327a69d513d45b3`.
- Backup conjunto `20261014T152153-481dcda9fe5f9c60` verificado y recuperación a `arsdocendi_recovery_36fbc65b1cd06c56`: estado/runner histórico compatible con cero pendientes y no-op; filas, eventos, objeto y secuencias conservados.
- Historia alpha sintética desconocida: estado y migrate devuelven exit 1, sin adopción/stamp/reset ni cambios en tablas.
- Bases incompatibles separadas: UPN normalizado duplicado, materia huérfana, scope incoherente y ciclo jerárquico. El diagnóstico expuso categorías/conteos, el migrate abortó con exit 1 y las comparaciones read-only confirmaron que las filas/historia previas no fueron saneadas ni reescritas.
- Tras el upgrade, SQL directo rechazó duplicado UPN (`23505`), materia sin destino (`23503`), scope vivo incoherente, ciclo y solución ausente (`23514`); cada operación tuvo rollback y cero filas/auditoría parcial.
- Incremental sintético generado por el scaffolder, publicado y ejecutado desde un assembly real: fallo **SQLSTATE 22012**, exit 1; DDL/INSERT testigo e history revertidos. Se conservan todos los hashes previos, bytes del objeto y backup verificable.
- Los **60 outputs CLI** `.stdout/.stderr` se cotejaron contra sus hashes SHA-256 de `resultado.json`: cero discrepancias.
- Restore repetido sobre destino poblado: exit 1, sin sobrescritura. Fallo de proveedor de backup: exit 1, registro incompleto retenido y respaldo anterior conservado/verificado.
- Teardown explícito: **8/8 recursos propios** (volumen backup, dos contenedores, dos volúmenes anónimos, red y dos imágenes de ensayo) eliminados; inexistencia exacta y cero errores verificadas. La imagen candidata local se conserva aparte hasta terminar los gates finales; no se publicó. No se promete recuperar tras destruir esos recursos desechables.
- Regresión durable `cleanup.test.mjs`: **1 test, 1 passed, 0 failed, 0 skipped**, exit 0; verifica eliminación e inexistencia del volumen anónimo real. Sin restos `.tmp-spin-up-*`.
- Gate de publicación separado: `node --test infra/tests/spin-up.test.mjs infra/tests/deploy-routes.test.mjs`, **16/16 PASS**. La simulación de fallo de migración verifica que el backend queda detenido y `up -d`/recibo exitoso no ocurren; las rutas se ejercitan con fronteras Docker/CLI sintéticas, no es despliegue real.
- Sintaxis de módulos, ESLint `no-undef` sin archivos ignorados y Prettier se verifican por separado; no sustituyen la ejecución del candidato.

## Incidentes de preparación (no pruebas de la aplicación)

1. Primer intento: opción incorrecta `-volumeSizeLimitMB` en SeaweedFS mini. Se comprobó `weed mini -h` y se corrigió a `-master.volumeSizeLimitMB=128`. Limpieza del intento fallido verificada.
2. `ensayo-baseline-3.tap`: error PostgreSQL 18 al tratar una secuencia como tipo compuesto en `row_to_json(s)`. Se sustituyó por `json_build_object('last_value',last_value,'is_called',is_called)`, leyendo campos directamente. Ese intento no cuenta como upgrade ni como regresión de la aplicación.
3. Se agruparon las comparaciones de tablas en una única lectura SQL read-only para reducir llamadas Docker, y se añadieron outputs CLI reales verificables junto a sus hashes.

## Comando de continuación para el responsable del candidato

```bash
UPGRADE_DOCKER_TESTS=1 \
  UPGRADE_CANDIDATE_IMAGE='<imagen local consolidada o ID inmutable>' \
  node --test scripts/tests/upgrade-post-auditoria.test.mjs
```

El harness no compila el working tree mutable de otros agentes. Con candidato, verifica instalación nueva y upgrade desde f10e0a5, diagnóstico/estado/preview sin writes, constraints/índices/triggers efectivos, rechazo SQL directo y no-op sin auditoría nueva. Crea bases incompatibles independientes para UPN normalizado, materia huérfana, scope incoherente y ciclos; exige rechazo del diagnóstico y del migrate sin saneamiento/rewrite de negocio o eventos anteriores. Histories parciales entre contextos se distinguen del rollback de una migración: el runbook no promete una transacción global.

## Límites explícitos

- El candidato se construyó del working tree actual, sin SHA Git de release consolidado. El test acredita este artefacto local exacto, no una imagen publicada ni un futuro commit distinto.
- El fallo `22012` demuestra rollback del runner/recursos compilados con una migración deliberadamente inválida de ensayo; no demuestra que los incrementales reales del candidato estén correctos.
- El gate `spin-up.sh` se probó por separado con dependencias sintéticas; no se desplegó ni probó contra un ingress real. Tampoco cubre pings/descarga por API/antivirus ni compatibilidad del backend antiguo sobre el schema nuevo. El restore compatible usa backup previo y runner histórico, no `Down`.
- El restore conserva referencias/buckets originales en PostgreSQL; verificar bytes del bucket recovery no adapta referencias ni autoriza reenrutar tráfico.
- Estos son tests Node con totales/skips efectivos. No sustituyen discovery xUnit v3 mediante `dotnet vstest` y TRX, que corresponde al gate backend separado.
- Sin commit, push, deploy, seed de datos reales, reset de ambiente compartido ni modificación de SQL/wrappers históricos.
