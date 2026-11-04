# Implementación Storage — sección 2

## Alcance y coordinación

Trabajo exclusivo en `ArsDocendi.Storage*`, nuevos SQL/wrapper de `database/storage`, tests `Storage` y este archivo. No se modifican Host/Program, Portal, Designaciones, Shared, documentación compartida ni `tasks.md`; parent integra consumidores y marca tareas solamente tras comprobar evidencia completa.

### Contratos listos para integrar

`Storage.Contracts/ReferenciasArchivos.cs`:

- `IConsultasReferenciasArchivos`: `string Consumidor`, `Task<bool> TieneReferenciasAsync(Guid archivoId, CancellationToken ct)`. Consumidores requeridos exactos: `Portal`, `Designaciones`. La consulta es selectiva en el schema local. Error, ausencia o duplicación de consumidor falla cerrado en eliminación.
- `IProteccionArchivos.EjecutarAsync<T>(IReadOnlyCollection<Guid> archivoIds, Func<CancellationToken, Task<T>> operacion, CancellationToken ct)`. Protege todos los IDs involucrados al asociar, reemplazar o desvincular, ordena/deduplica los IDs y confirma el callback en transacción local PostgreSQL. No ejecutar stream/antivirus/retención remota dentro del callback.
- `IParticipanteTransaccionArchivos.EnlistarAsync(DbConnection, DbTransaction, ct)` / `DesenlistarAsync(ct)`. Implementación local establece conexión compartida (`SetDbConnection`) y `UseTransactionAsync`; Storage inicia y confirma/revierte. Desenlistar y limpiar tracker tras rollback. No `TransactionScope` ni MSDTC.
- `IRetencionArchivos.EliminarSiSinReferenciasAsync(Guid archivoId, Guid propietarioId, ct)` tras confirmar la desvinculación: conserva y devuelve false si hay referencias; sin ellas reserva y elimina. Un fallo remoto es conflicto explícito reintentable.
- `IConsultasArchivos.ObtenerPorIdsAsync(ids,ct)` devuelve diccionario por lote, con deduplicación; no nuevas búsquedas globales/N+1.

DI local de Storage registra protección, retención, batch y `DiagnosticoIntegridadAlmacenamiento` (`Contexto=Storage`, registros readonly `destino`/`archivo`). Host debe componer consultas y participantes de los dos consumidores. Crear/subir/confirmar no exige consultas de consumidores; eliminar sí.

## Mecanismo

Nueva migración por scaffolder: `20261013165221_ReservasInmutablesStorage`, SQL y wrapper asociados. Se añaden `intento_id`, `reserva_id`, `reserva_hasta`, `fase_carga`; no se reescriben SQL/wrappers históricos ni manifiesto. Snapshot EF actualizado.

Cada PUT reserva atómicamente antes de leer cuerpo y usa `archivos/<archivoId>/intentos/<intentoId>`; nunca sobrescribe una clave utilizada. La transición subiendo→cargado y la publicación revisando→disponible usan CAS persistido. Confirmación valida el tamaño exacto de bytes descargados, MIME y SHA, analiza esa versión y verifica token de revisión al publicar. Doble confirmación en curso puede devolver conflicto seguro; reintentar misma publicación es idempotente, pero un hash/tamaño distinto no reutiliza análisis.

Upload/review usan lease recuperable menor que la vida restante de la sesión (`DuracionReservaSegundos`, default 30, limitado a la mitad del tiempo restante). El vencimiento invalida el token; un resultado tardío no publica otra versión. Una excepción observada libera para retry, una caída real deja lease hasta vencimiento. No hay transacciones de DB ni locks durante stream/antivirus.

Borrado protege ID, consulta todos los consumidores, reserva cuarentena/fase eliminando y confirma esa indisponibilidad ANTES del DELETE remoto. Fallo del proveedor: `archivo-delete-pending`, reintentable sin resucitar disponible. Referenciado: `archivo-referenced`. Consumidor faltante/fallido: `archivo-references-unavailable`. Limpieza conserva leases activos y objetos ajenos/no reconocidos; elimina temporales propios obsoletos y reintenta borrados reservados.

Trigger real protege clave/bucket/ambiente/intento/hash/MIME/tamaño de publicaciones, incluido legado disponible, e impide republicar la reserva de eliminación. Los archivos disponibles anteriores conservan claves y bytes; una sesión anterior ambigua no se confirma sin nueva carga verificable. El runtime owner/SQL externo puede omitir el protocolo de asociaciones; no se prometen FKs intermodulares ni aislamiento frente a DBA.

## Evidencia ejecutada

Comandos desde raíz. Builds aislados: `dotnet build backend/tests/ArsDocendi.IntegrationTests -c Release --artifacts-path /tmp/ars-storage-artifacts`.

Runner efectivo: `dotnet vstest /tmp/ars-storage-artifacts/bin/ArsDocendi.IntegrationTests/release/ArsDocendi.IntegrationTests.dll --TestAdapterPath:/tmp/ars-storage-artifacts/bin/ArsDocendi.IntegrationTests/release --TestCaseFilter:<filtro> --logger:trx;LogFileName=<nombre>`.

Fixtures: PostgreSQL 18 y SeaweedFS real con imagen fijada por digest; recursos enviados por API/stdio de Testcontainers, sin bind mounts ni bases compartidas. Antivirus/cuerpo/fallos de transporte se detienen/inyectan en la frontera; no se mockea el protocolo completo ni la persistencia/objetos.

- `storage-red.trx`: 1 FAIL esperado. PUT B detenido tras lectura inicial; A confirmado; B completado: bytes de descarga B difieren de A en posición 17.
- `storage-inmutable-green.trx`: 1 PASS; mismas intercalaciones ya no sustituyen A, comparando bytes, MIME, tamaño y hash.
- `storage-referencias-red.trx`: 1 FAIL esperado; borrado sin consumidores no lanzó excepción.
- `storage-referencias-green.trx`: 1 PASS; misma operación falla cerrado y conserva disponibilidad.
- `storage-concurrencia-green.trx`: 5 PASS: antivirus tardío-vs-delete, doble confirmación, fallo remoto/retry, error SQL real de consumidor y archivo efectivamente referenciado por Portal.
- `storage-recuperacion-red.trx`: 1 FAIL esperado: lease de intento vencía después de sesión e impedía recuperación.
- `storage-recuperacion-transacciones.trx`: 7 PASS / 1 FAIL. Fallo real adicional del proveedor AWS SDK 4 al listar bucket vacío (`S3Objects=null`); corregido a colección vacía, no evidencia inventada.
- `storage-suite.trx`: 32 PASS / 1 FAIL / 0 skipped, 33 descubiertos. Incluye recuperación, rollback multicontexto real, asociación-vs-delete verificando espera Lock en `pg_stat_activity`, orden inverso de IDs, upgrade Storage desde baseline real de `f10e0a5`, preview sin writes/no-op sin auditoría nueva, trigger real y batch/diagnóstico. Único fallo: fixture de reemplazo Portal sin versión tras modificación concurrente 6.3; se adaptó a versión retornada.
- `storage-portal-retencion.trx`: reemplazo Portal normal PASS. Test nuevo compartido con pedido falló inicialmente por fixture sin dedicación de catálogo; fixture corregida antes de evaluar el comportamiento.
- `storage-retencion-compartida-red.trx`: 1 FAIL de comportamiento esperado, después de corregir fixtures: reemplazo Portal lanza `archivo-referenced` porque todavía llama `EliminarAsync` incondicional. Corrida con `BuildProjectReferences=false` sobre los assemblies de consumidores previamente compilados para aislar bloqueo ajeno; no constituye GREEN de consumidores.
- `storage-suite-final-aislada.trx`: **33/33 PASS, 0 skipped**, excluyendo únicamente el test de integración pendiente `Cv_compartido`. Totales comprobados leyendo `Counters` del TRX con Python. Incluye fixture Portal normal corregida para aportar versiones; no se omiten tests Storage previamente existentes. La llamada de herramienta excedió 420 segundos, pero el proceso real continuó, terminó y produjo el TRX; se verificó el archivo final, sin inventar salida de ejecución.
- Build Release actual de `backend/src/ArsDocendi.Storage` con artefactos aislados: PASS, 0 warnings / 0 errores.
- `dotnet /tmp/ars-storage-artifacts/bin/ArsDocendi.Host/release/ArsDocendi.Host.dll --validar-recursos`: PASS (`compatible=true`, recursos verificados) sobre assembly aislado.
- `node --test scripts/tests/migrations.test.mjs`: **38/38 PASS**, cero omitidos/cancelados.
- `node scripts/migrations/validate.mjs --base-ref $(git rev-parse f10e0a5)` PASS: `proteccionGit=true`, revisión confiable `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`; recursos/SQL/wrappers anteriores intactos.
- `git diff --check` PASS. Escaneo de fuentes Storage: sin debug prints, imports de internals consumidores ni `ExecuteSqlRawAsync` interpolado. Grafo de los 13 proyectos backend verificado programáticamente: sin ciclos; Storage depende únicamente de Shared, sus Contracts y Migraciones.

Los archivos TRX son resultados locales regenerables, no dependencia de aceptación sobre `.artifacts` ignorados. Esta evidencia distingue corridas parciales y finales; no marca tareas automáticamente.

## Pendiente de cierre coordinado

La aceptación completa 2.4–2.6 también exige que todos los consumidores reales usen los contratos, pruebas de descarga autorizada por pedido/frontend, documentación API/dominio/runbook y grafo. Son ownership de parent/agentes consumidores. Test persistente `Cv_compartido_con_pedido_sobrevive_reemplazo_y_delete_directo_es_conflicto` verifica que reemplazo Portal conserve bytes requeridos por un pedido real y DELETE directo devuelva conflicto; parent debe completar integración y ejecutar GREEN. No declarar sección 2 entera completa sólo porque Storage individual pase.

Último intento de build conjunto se bloqueó transitoriamente por edición concurrente `RepositorioPortal.cs` (CS0173/CS1061); no se modificó archivo ajeno para resolverlo.
