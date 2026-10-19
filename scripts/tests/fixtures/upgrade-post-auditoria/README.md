# Ensayo de upgrade post auditoría

Fixture exclusivamente sintética, sin SGA, PII real, conexiones externas ni credenciales del workspace. El harness no lee `.env`. Usa PostgreSQL 18 y SeaweedFS reales, redes/contenedores/volúmenes propios de nombre aleatorio; transmite SQL, builds, dumps y objetos por stdin/stdout, sin bind mounts ni puertos publicados.

## Ejecutar

```bash
# Baseline histórico real + fallo incremental compilado + recuperación compatible.
UPGRADE_DOCKER_TESTS=1 node --test scripts/tests/upgrade-post-auditoria.test.mjs

# Cuando el candidato esté consolidado y construido por su responsable:
UPGRADE_DOCKER_TESTS=1 \
  UPGRADE_CANDIDATE_IMAGE='<imagen-local-o-ID-inmutable>' \
  node --test scripts/tests/upgrade-post-auditoria.test.mjs
```

El harness compila un `git archive` de `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b` dentro de `.artifacts/upgrade-post-auditoria/<uid>/anterior`, nunca sobre los `bin/obj` del árbol compartido. La imagen histórica se crea con el publish Release y se ejecuta por su ID Docker. `--validar-recursos`, estado, preview tar y migrate corresponden al CLI compilado; no se sustituyen por replay SQL ni mocks. Una segunda imagen desechable incorpora mediante el scaffolder una migración que crea/inserta un testigo y falla con `22012`: debe revertir tabla, filas e history, conservar backup/objeto y permitir recuperación mediante la imagen histórica compatible.

La ejecución sin `UPGRADE_CANDIDATE_IMAGE` omite explícitamente el subtest del candidato: **no completa 8.1/8.2**. El candidato suministrado se resuelve a un ID Docker inmutable y esa identidad se registra; no se presupone que el HEAD o working tree correspondan a la imagen. El campo CLI `RELEASE_SHA` para ese candidato identifica el ID de imagen, no inventa un commit consolidado.

## Datos y verificaciones

- Cuenta/persona con UUID estables, OID/TID Microsoft ya vinculados, rol personalizado y dos permisos. Cuenta inactiva con referencias históricas válidas.
- Pedido con snapshot JSON legacy, vigencia, perfil, CV y adjunto que comparten el archivo disponible anterior; dos tareas raíz/hija coherentes con proyecto NULL.
- PDF sintético: bytes, SHA-256, tamaño, MIME y metadata S3 comparados tras instalación/no-op y restore.
- Tablas/histories/auditoría comparadas por conteos y hashes de JSON ordenado. Las lecturas se agrupan en una transacción read-only. Al comparar upgrade, se proyectan las columnas anteriores y se fija el corte de IDs de eventos legacy; histories pueden crecer, pero no se acepta rewrite de negocio/eventos previos. El no-op compara el estado completo. Se conserva el estado de las tres secuencias de negocio.
- Recovery mediante los scripts reales `backup-storage.sh`, `backup-volume.sh` y `restore-storage.sh`, base nueva autorizada sin ingress. Restore sobre destino poblado debe rechazarse. Fallo real de conexión a puerto cerrado debe dejar backup incompleto retenido y conservar el respaldo completo anterior.
- Candidato: estado/preview/diagnóstico sin writes, upgrade desde f10e0a5, instalación nueva, catálogo real y escrituras inválidas rechazadas por SQLSTATE/constraint o trigger. Bases incompatibles separadas para UPN equivalentes, materia huérfana, scope incoherente y ciclo. No se sanea ni se resetea para obtener éxito.

## Evidencia y límites

`resultado.json`, outputs CLI `.stdout/.stderr`, logs de publish/build, previews tar y catálogos reales quedan en el directorio aleatorio. Los backups se verifican mientras existen y se eliminan **solo en el teardown explícito del fixture desechable**, después del ensayo; no se conserva una falsa promesa de recovery posterior a la limpieza. La limpieza exige eliminación e inexistencia del nombre exacto de cada recurso propio; nunca `prune`.

El ensayo no despliega, no publica tráfico, no prueba por sí mismo el gate de publicación de `spin-up.sh`, no certifica pings/descargas HTTP de la aplicación, antivirus ni compatibilidad de un backend antiguo sobre el schema actualizado. La recuperación compatible se verifica contra un backup anterior con el runner anterior, no mediante `Down`. Los nombres de buckets persistidos se conservan: comprobar los objetos en el bucket aislado recuperado no autoriza reenrutar la aplicación ni adaptar las referencias automáticamente.

Esta suite es Node, no xUnit: sus totales efectivos y skips son los de `node --test`. La suite backend xUnit v3 debe ejecutarse separadamente con discovery efectivo mediante `dotnet vstest` y TRX; este harness no sustituye ese gate.
