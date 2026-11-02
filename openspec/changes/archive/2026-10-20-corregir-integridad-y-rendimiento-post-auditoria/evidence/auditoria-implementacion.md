# Auditoría precisa: implementación y evidencia

Change: `corregir-integridad-y-rendimiento-post-auditoria`, sección 7. Ensayo inicial del 13 y re-verificación del 14 de octubre de 2026 en PostgreSQL 18-alpine desechable (Testcontainers), sin binds, SGA, conexiones compartidas, commit ni despliegue. Este documento conserva evidencia de Auditoría; el runbook operativo enlaza clasificación, límites y decisión de índices.

## 7.1 Identidad completa y compatibilidad

Migración nueva: `20261013165425_AuditoriaPrecisaIdentity`, generada junto con su SQL por `node scripts/migrations/scaffold.mjs Identity AuditoriaPrecisa`. No modifica SQL, wrappers ni manifiesto histórico. Su wrapper declara el recurso incremental de Identity que instala infraestructura Audit.

Añade `audit.change_log.row_key jsonb` y `row_pk_format` (`legacy`, `simple-v1`, `composite-v1`). El valor por defecto etiqueta eventos anteriores como legacy **sin actualizar sus snapshots ni sus columnas anteriores**. No hay backfill.

- Las PK simples mantienen su `row_pk` textual y la firma `audit.attach(regclass,text DEFAULT 'id')`.
- La sobrecarga `audit.attach(regclass,text[])` exige todas las columnas en orden de catálogo. La representación de claves compuestas es un objeto JSONB determinístico, no una concatenación ambigua.
- Los triggers antiguos con argumento parcial descubren la PK real; los nuevos enganches simples también descubren esa PK. Esto permite aplicar Identity antes de instalar Portal en una base nueva. En upgrade se reenganchan explícitamente `identity.rol_permisos` y, si ya existe, `portal.docente_habilidades`.
- `rol_permisos` distingue `rol_id` y `permiso_id`; `docente_habilidades` distingue `perfil_id`, `habilidad_id` y `tipo`. La captura también conserva PK compuestas reales en otras tablas auditadas.
- `audit.resolved_key` reconstruye en lectura las relaciones históricas conocidas solo cuando todos los componentes están presentes, no nulos y consistentes entre snapshots anterior/nuevo. Un cambio de PK legado es ambiguo y no se adjudica a una relación precisa.
- El filtro administrativo `rowPk` admite JSON completo, compara semánticamente JSONB (orden/espaciado de propiedades no importa) e incluye solo legado recuperable. Las claves parciales siguen consultando grupos históricos sin confundirse con relaciones exactas.

La API conserva su DTO existente: `rowPk` devuelve la clave completa para nuevos eventos compuestos y legado recuperado; el resumen declara «Clave legada recuperada en lectura» o «Agrupación histórica (clave parcial; no es una relación precisa)». La metadata adicional se proyecta por lote acotado a los IDs de la página, sin modificar otras entidades/mappings Identity. La UI explica la diferencia y transmite el JSON sin alterarlo.

RED comprobado: dos permisos del mismo rol produjeron una sola clave distinta (`esperado 2`, `actual 1`); un filtro preciso sobre legado recuperable devolvió cero (`esperado 1`). GREEN verifica dos permisos, habilidad/interés, filtros con JSON reordenado y exclusión de eventos sin componentes o con PK cambiada.

## 7.2 Historia determinística

`audit.row_history(text,text,text)` mantiene firma y semántica de lectura por clave textual, pero selecciona creación, último evento y eliminación una sola vez cada uno, ordenando por `(changed_at,id)` ascendente o descendente según corresponda. Cada fecha y actor provienen de la misma fila seleccionada. «Actualizado» mantiene la semántica anterior de último evento, incluida eliminación.

Para relaciones exactas se expone `audit.row_history_precise(text,text,jsonb)` con `key_format='precise'`. Para una agrupación deliberada, `audit.row_history_group(text,text,text)` devuelve `key_format='legacy-group'`. No usar la firma textual con una clave parcial de rol/perfil como si representara una sola relación.

RED comprobado: tres eventos empatados devolvían como último actor el primero (`1111…`), en vez del tercero (`3333…`). GREEN cubre creación, modificación, eliminación y una transacción real de INSERT + dos UPDATE con `set_config` de actores distintos y timestamp idéntico. La página desempata además por ID descendente.

## 7.3 Minimización y redacción

`audit.safe_snapshot(schema,tabla,jsonb)` es una allowlist **por tabla y campo**, revisable en el SQL incremental. Una tabla/columna nueva no clasificada conserva snapshot `{}` o carece de ese campo; no existen reglas automáticas por sufijo `_id`. Se calcula primero `changed_columns` sobre la fila completa, por lo que cambiar teléfono/documento o una columna no clasificada sigue dejando constancia del cambio sin copiar su valor.

Clases permitidas: claves/referencias técnicas expresamente enumeradas, estados/códigos controlados, flags y fechas/cargas necesarias. En `designaciones.designaciones` se conservan cargo, dedicación por ID, las tres cargas, vigencia y `origen_pedido_id` anterior/nuevo. No se duplica `pedidos.snapshot`, justificación, comentario del trámite, nombres/documentos, UPN/OID/TID, teléfono/mail, secretos, texto de comentarios/soluciones, URI/bucket/clave de objetos o texto documental no clasificado.

`PoliticaValoresAuditoria` aplica una allowlist de exposición también por tabla. Es más restrictiva que la captura para algunas referencias técnicas; conserva la trazabilidad de cargo/dedicación/origen. Datos personales, campos desconocidos y objetos/arrays permanecen ocultos en API, incluso en eventos antiguos. Que `code` esté permitido en roles no lo autoriza en personas ni tablas futuras. No se entregan snapshots crudos ni `client_ip`. El actor legible se resuelve desde identidad bajo el permiso administrativo, no desde correo en snapshots. Un `display_name` legado que contiene correo no se expone: devuelve «Actor no identificado»; su regresión tuvo RED causal y GREEN.

RED comprobado: persistencia de persona copiaba PII; API exponía `code` en una tabla no clasificada y ocultaba IDs técnicos necesarios de vigencia. GREEN verifica captura de columnas sensibles sin valores, secretos/columna desconocida en una tabla nueva, redacción legada y los valores técnicos anterior/nuevo de Designaciones.

## 7.4 Clasificación, acceso y tratamiento histórico

| Conjunto                          | Contenido y riesgo                                                                              | Acceso y tratamiento                                                                                         |
| --------------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Metadata de eventos/identidades   | IDs técnicos, actor, operación, fecha y correlación; siguen siendo datos vinculables a personas | Consulta administrativa autenticada con `auditoria.ver`; no es información pública ni anonimizada            |
| Snapshots nuevos                  | Solo valores aprobados por tabla; referencias técnicas siguen siendo vinculables                | Captura SQL minimizada y API con política de exposición más restrictiva                                      |
| Snapshots anteriores              | Pueden contener documento, nombres, contacto, identidad Microsoft u otros valores sensibles     | API redactada; acceso SQL directo restringido operacionalmente; **no** se han eliminado los valores en BD    |
| Historial de trámites             | Eventos de negocio y comentarios fuente, distintos de las copias de auditoría                   | Conservado sin purga/rewrite; autorización propia del módulo                                                 |
| Backups/dumps anteriores y nuevos | Conservan snapshots históricos y posiblemente datos personales fuente                           | Material privado del ambiente; controles y recuperación aislada del runbook existente, no artifacts públicos |

**Límite expreso:** el usuario runtime es owner de la DB en la topología actual. Puede leer snapshots anteriores y cambiar objetos/privilegios; una vista o un `REVOKE` no ofrece aislamiento frente al owner/DBA. Esta entrega no separa identidades de migración/runtime, no introduce secretos ni promete cifrado/WORM frente al administrador.

No hay política institucional de retención aprobada en este cambio. No se asigna plazo legal, purga, anonimización retrospectiva ni reasignación/fusión de eventos. Redacción API no equivale a borrado en DB ni en respaldos.

Procedimiento para cualquier tratamiento futuro, **fuera de esta autorización**:

1. Obtener política institucional y autorización operativa específica que identifiquen ambientes, clases de datos, alcance sobre respaldos y obligaciones de conservación.
2. Inventariar en modo read-only categorías/conteos/IDs técnicos, evitando volcar PII o snapshots crudos a tickets/logs/evidencia pública.
3. Detener escritores conocidos, obtener backup verificable del ambiente y ensayar la operación aprobada en destino aislado; no copiar datos productivos a staging/PR.
4. Revisar explícitamente impactos sobre referencias/actores y el historial de trámites. La corrección de copias de auditoría no autoriza eliminar historia fuente.
5. Ejecutar solamente el procedimiento de datos aprobado, con recibo y comparación posterior. Recuperar en destino aislado con versión compatible; no hacer reset/stamp ni `Down` destructivo.

`AuditoriaUpgradeTests` instala los baselines anteriores e incrementales publicados necesarios, siembra exclusivamente `sintetico.sql`, conserva auditoría con snapshots legacy y las dos relaciones de Portal, aplica el incremental y lo reejecuta. La huella de todas las columnas anteriores de los eventos y del historial de trámites permanece idéntica. Solo las escrituras posteriores generan snapshots minimizados; se verifica también cargo/tres horas/vigencia/origen y un DELETE nuevo con PK completa. Es un upgrade de Auditoría; no sustituye el ensayo integral BD+objetos desde `f10e0a5` de la sección 8.

## 7.5 Medición reproducible de página y COUNT

`AuditoriaRendimientoTests` captura los comandos y parámetros **reales de EF** emitidos por `RepositorioAuditoria`, incluida proyección de actor, y ejecuta cada página y COUNT por separado mediante `EXPLAIN (ANALYZE,BUFFERS,WAL,FORMAT JSON)`. Mantiene los índices vigentes (BRIN de fecha, historia por fila y actor), 200.000 eventos sintéticos más los eventos de instalación, página de 50, 20.000 eventos del filtro de tabla y una fila exacta. El rango desde `2026-05-19T00:00:00Z` contiene 1.281 eventos generados más los de instalación.

Cuatro variantes: vigentes solamente, B-tree `(changed_at DESC,id DESC)`, B-tree `(schema_name,table_name,changed_at DESC,id DESC)` y ambos. Por consulta se registra primera observación y tres repeticiones calientes; no se afirma cache fría del host/daemon. No se limpian caches globales ni se configura un SLA. Inserciones de 10.000 filas se miden tres veces por variante dentro de transacciones que hacen rollback en la **DB desechable**; no se actualizan ni purgan eventos de negocio. El benchmark elimina/recrea únicamente sus índices candidatos en su DB aislada.

Re-verificación del 14-oct-2026: 140 planes (128 lecturas y 12 escrituras), más 20 observaciones de tamaño individual de índices, en `.artifacts/apply-post-auditoria/auditoria-current/auditoria-mediciones.json`. TRX VSTest `.artifacts/apply-post-auditoria/auditoria-current/auditoria-current.trx` registra 32/32 PASS, cero fallos/omisiones; incluye `AuditoriaRendimientoTests`, `AuditoriaUpgradeTests` y las pruebas HTTP de límites/permisos/timeout. Medianas calientes de repeticiones 1–3; `hits` son bloques compartidos del nodo raíz de EXPLAIN. Los nodos agregan el trabajo de sus hijos: no se suman de nuevo.

| Consulta        | Vigente ms / hits | Fecha+ID ms / hits | Tabla+fecha+ID ms / hits | Ambos ms / hits |
| --------------- | ----------------- | ------------------ | ------------------------ | --------------- |
| Página reciente | 181,970 / 3213    | 0,172 / 6          | 112,306 / 3213           | 0,172 / 6       |
| COUNT total     | 53,479 / 3179     | 62,291 / 3179      | 57,191 / 3179            | 43,469 / 3179   |
| Página tabla    | 38,548 / 3421     | 0,273 / 14         | 0,251 / 12               | 0,220 / 12      |
| COUNT tabla     | 19,212 / 3415     | 26,902 / 3415      | 19,150 / 3324            | 19,166 / 3324   |
| Página rango    | 4,383 / 246       | 0,161 / 6          | 1,688 / 73               | 0,157 / 6       |
| COUNT rango     | 4,723 / 240       | 0,551 / 30         | 0,725 / 70               | 0,486 / 30      |
| Página fila     | 0,296 / 13        | 0,360 / 13         | 0,268 / 13               | 0,291 / 13      |
| COUNT fila      | 0,076 / 4         | 0,078 / 4          | 0,074 / 4                | 0,131 / 4       |

Planes observados:

- Página reciente vigente: scan paralelo de todas las filas, joins y top-N sort; con fecha+ID, index scan que entrega 50 filas y 6 bloques sin ordenar todo el conjunto.
- COUNT total: scan paralelo del mismo conjunto en todas las variantes; **no hay evidencia de optimización de COUNT global** por añadir fecha+ID.
- Tabla vigente: bitmap por `change_log_row_history_idx`, 20.000 filas y sort de página. Fecha+ID entrega 50 coincidencias descartando 450 filas, 14 bloques; tabla+fecha+ID entrega 50 sin descartar y 12 bloques.
- Rango vigente: bitmap BRIN, 1.290 filas resultantes en este ensayo (1.281 generadas + 9 de instalación). Fecha+ID permite acceso selectivo de rango y evita sort para la página.
- Fila exacta: índice de historia existente; la mejora temporal no altera sus bloques de lectura.

| Variante       | INSERT 10.000 mediana ms | WAL bytes mediana | Tamaño de candidato(s), bytes |
| -------------- | ------------------------ | ----------------- | ----------------------------- |
| Vigentes       | 163,890                  | 3612549           | sin candidato                 |
| Fecha+ID       | 295,970                  | 4588885           | 8019968                       |
| Tabla+fecha+ID | 212,608                  | 4930944           | 12812288                      |
| Ambos          | 249,351                  | 5903171           | 8019968 + 12812288            |

BRIN vigente mide 24.576 bytes; se conserva. El índice por actor también se conserva, y el índice de historia ya cubre la consulta exacta por schema/tabla/fila. Los tamaños de candidatos se midieron después de las inserciones revertidas: incluyen su trabajo/páginas; los otros índices pueden crecer entre variantes por esas inserciones. No comparar la suma global de tamaños como si las cuatro variantes fueran instalaciones físicas idénticas. Los tiempos de máquina compartida son observaciones, no factores de aceleración extrapolables; planes/bloques y WAL respaldan la decisión.

**Decisión:** crear solo `change_log_recientes_idx(changed_at DESC,id DESC)` en el incremental. Mejora página reciente y página/COUNT de rango. Descartar tabla+fecha+ID adicional en esta carga: frente al índice temporal ya adoptado solo evita 450 descartes/2 bloques de la página selectiva, COUNT sigue leyendo las 20.000 filas y agrega unos 12,8 MB/costo de WAL. No duplicar índice de fila ni actor sin otra carga medida. BRIN no se sustituye: sigue siendo pequeño y útil para rangos amplios, pero no entrega el orden de página.

SQL real capturado de COUNT/página recientes (sin reemplazar la consulta por una simplificación de benchmark):

```sql
SELECT count(*)
FROM audit.change_log AS c
LEFT JOIN identity.users AS u ON c.changed_by = u.id
LEFT JOIN identity.personas AS p ON u.persona_id = p.id;

SELECT c.id, c.action, c.changed_at, c.changed_by, c.row_pk,
       c.changed_columns, c.old_row, c.new_row, c.schema_name,
       c.table_name, c.request_id, u.display_name, p.nombre, p.apellido
FROM audit.change_log AS c
LEFT JOIN identity.users AS u ON c.changed_by = u.id
LEFT JOIN identity.personas AS p ON u.persona_id = p.id
ORDER BY c.changed_at DESC, c.id DESC
LIMIT @p1 OFFSET @p;
-- @p1=50, @p=0
```

Los filtros medidos se emitieron antes del orden/límite: `c.schema_name=@schema AND c.table_name=@tabla`; `c.changed_at>=@desde`; y schema/tabla más `c.row_pk=@rowPk`. La consulta adicional de metadata de claves usa exclusivamente IDs de la página (máximo 100); no sustituye ni oculta el costo del COUNT.

## Verificación y reproducción

- RED→GREEN causal guardado en TRX para claves, desempate, snapshots, legado y API; la prueba nueva de UI falló por ausencia de descripción accesible antes del fix.
- Verificación actual con filtro VSTest `FullyQualifiedName~Auditoria`: **32 ejecutadas, 32 aprobadas, 0 fallidas/omitidas**, TRX `.artifacts/apply-post-auditoria/auditoria-current/auditoria-current.trx`. Incluye captura PK simple/compuesta/legacy, historia determinística, `AuditoriaUpgradeTests` (upgrade/no-op sin reescritura de snapshots ni pérdida de historial), `AuditoriaRendimientoTests`, redacción API, permisos, JSON/límites y timeout real de 5 s (consulta con `pg_sleep(8)`). El filtro también descubrió pruebas de otros namespaces que contienen «Auditoria»; el contador no se presenta como suite backend completa.
- Frontend Auditoría: **5/5** con `--testTimeout=30000` por contención del entorno. El default de 5 s había producido timeouts en dos tests anteriores; el RED nuevo fue por el comportamiento ausente, no por timeout. ESLint de los dos archivos y Prettier: OK.
- Build de Host y del harness focal Release: OK. El `CS0103` transitorio por `CamposConValorSeguro` quedó corregido por `PoliticaValoresAuditoria`.
- `--validar-recursos`: `{"compatible":true,"recursos":"verificados"}`.
- `node --test scripts/tests/migrations.test.mjs`: OK. `validate.mjs --base-ref d18f11900e24c2864ed1a93fcb4cb1de71b8b2f7`: protección Git verdadera, sin alteración de historia/manifiesto. Ese SHA es el HEAD base observado del checkout; no sustituye la elección del SHA institucional de CI.
- `pnpm exec openspec validate corregir-integridad-y-rendimiento-post-auditoria --strict`: válido. La entrega focal no ejecutó `status` ni `instructions apply` ni editó checkboxes compartidos. **Corrección del integrador:** la afirmación original de ausencia de esos comandos no corresponde al CLI verificado del workspace: OpenSpec **1.14.1** ejecuta `status --change ... --json` correctamente. El coordinador conserva el workflow y los gates de apply; `status.isComplete` aquí describe artefactos de planificación, no tests ni implementación terminada.

Comandos de reproducción normal, una vez estable el working tree integrado:

```bash
dotnet build backend/tests/ArsDocendi.IntegrationTests -c Release
AUDITORIA_EVIDENCIA_DIR=/tmp/auditoria-evidencia \
  dotnet vstest backend/tests/ArsDocendi.IntegrationTests/bin/Release/net10.0/ArsDocendi.IntegrationTests.dll \
  --TestAdapterPath:backend/tests/ArsDocendi.IntegrationTests/bin/Release/net10.0 \
  --TestCaseFilter:FullyQualifiedName~Auditoria \
  --logger:'trx;LogFileName=auditoria.trx'
pnpm --filter frontend test:run src/features/sistema/pages/AuditoriaPage.test.tsx --testTimeout=30000
```

Durante el trabajo paralelo el build global estuvo transitoriamente bloqueado por contratos Storage/Identity y después por `Portal/PerfilLecturaTests.cs:56` (`CapturaConsultas.Intercalar`, CS1061). No se tocaron esos archivos. Para no falsear discovery se compiló un proyecto temporal `/tmp/ars-auditoria-verificacion/Auditoria.Verificacion.csproj` con **los mismos archivos fuente de pruebas del repo**, `PostgresFixture`, referencia real a Host y las mismas versiones de xUnit v3/VSTest/Testcontainers/Mvc.Testing. Se ejecutó su DLL con `dotnet vstest`, adaptador absoluto, TRX y PostgreSQL real; no son mocks ni tests reimplementados. La suite integral y la integración documental/checklist quedan a cargo del parent, no se declaran verificadas por esta evidencia focal.
