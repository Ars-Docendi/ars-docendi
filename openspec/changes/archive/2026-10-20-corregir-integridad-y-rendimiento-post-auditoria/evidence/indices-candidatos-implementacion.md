# Índices candidatos: implementación de tarea 6.6

## Alcance y reproducción

- PostgreSQL 18.4 real, contenedor Docker propio sin red, puertos publicados ni binds; SQL y resultados por stdin/stdout. Limpieza comprobada por ausencia del nombre exacto.
- Baseline estructural fijado a `f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b`; recursos originales y SHA-256 individuales en `resultado.json`. No se ejecutó el runner EF ni se simularon histories: este ensayo mide DDL y consultas, no un upgrade de release.
- Consultas v1: traducción SQL explícita de predicados/orden de `RepositorioPedidos.ListarPorMateriasAsync`, `ListarPorCarrerasAsync`, `ListarDelPeriodoAsync`; `RepositorioTareas.ListarAsync`, `ListarProyectosAsync`, `ListarRelacionesDeAsync`, y paso de hijos de `DescendientesAsync`. Localización inicial con CodeGraph. No se presenta este SQL como captura de EF ni benchmark HTTP; joins de catálogos/colecciones no están incluidos en la medida de acceso a la tabla principal.
- Las sondas FK consultan todas las filas referenciantes mediante igualdad. No son un DELETE administrativo ni una medida exacta de la consulta interna RI (que puede parar al primer match para RESTRICT); la cascada por pedido sí necesita encontrar todas sus referencias. No se ensayó borrado de catálogos ni se agregaron FKs intermodulares.
- Dataset v1: 20000 y 100000 pedidos/Tareas/designaciones/historiales/comandos; 1000 materias, 20 carreras, 10 períodos, 2000 usuarios sintéticos, 1000 proyectos, diez roles sintéticos además del catálogo. Pedidos: 5% en revisión y resto cancelados/rechazados, 1/7 prioritarios. El período consultado conserva 10000 filas, una materia diez pedidos, una carrera 500. Tareas del responsable: 50 constantes al crecer datos ajenos. Vigencias: una y cinco etapas por 20000 personas; 20000 abiertas. Catálogos cargo/dedicación deliberadamente concentrados (100%) para control no selectivo.
- Antes/después pareados para cada índice, un candidato a la vez. `VACUUM ANALYZE` tras preparar datos y writes revertidos; no se fuerza `enable_seqscan` ni se ocultan planes que rechazan el candidato. No hay LIMIT visible ni cambios de contrato.
- Cada fase: reinicio PostgreSQL, primer acceso con shared_buffers frío, luego cinco lecturas warm. Page cache del host no controlado: **no es cold disk**. Se conservan todos los planes ANALYZE/BUFFERS/WAL, tiempos y rangos; el reinicio no elimina cachés del daemon/host.
- Escrituras: tres repeticiones reales de INSERT de 500 filas y UPDATE de 500 filas por fase, cada una con ROLLBACK. Triggers, FKs e índices originales activos; volumen de WAL y tiempos medidos. El UPDATE toca created_at (permite HOT cuando no está indexado), no pretende representar todos los cambios de FK/estado. Inserción de pedidos cancelados evita colisión de cupo; designaciones insertadas cambian a otra materia válida para respetar EXCLUDE. El costo de las secuencias puede dejar gaps, sólo dentro del contenedor desechable.
- Cardinalidad y hash de todas las columnas conservados antes/después (hash ordenado por contenido para consultas sin orden). Los tiempos son observaciones de esta máquina concurrente, no SLA ni factor productivo.

```bash
python3 scripts/tests/fixtures/rendimiento-candidatos/medir.py
python3 scripts/tests/fixtures/rendimiento-candidatos/informar.py
node --test scripts/tests/rendimiento-candidatos.test.mjs
```

Requiere Docker accesible, imagen `postgres:18.4`, Python 3 y Git con el SHA auditado. No admite una URL de base externa. El benchmark tarda varios minutos; un timeout del cliente no implica que terminó el proceso SSH: consultar su PID y JSON de progreso, no lanzar otro en paralelo.

## Criterio de decisión

Justificar un índice sólo si agrega acceso eficiente necesario y se comparan ambos tamaños: beneficio de bloques/filtrado global u orden útil en el volumen representativo, sin regresión estructural en el menor, cardinalidad íntegra y costo de escritura/tamaño explícitos. Cobertura ya existente, candidato no elegido, baja selectividad o fluctuación temporal sin reducir trabajo motivan descarte. No se exige un umbral de milisegundos absoluto. JUSTIFICADO significa propuesta al integrador: **no se agregó ningún índice productivo en esta tarea**.

## Lecturas: medianas de cinco repeticiones

| Candidato                     |      N |  Filas | ms antes → después | buffers antes → después | KiB candidato | Decisión    |
| ----------------------------- | -----: | -----: | -----------------: | ----------------------: | ------------: | ----------- |
| pedidos-materia-orden         |  20000 |     10 |      0.133 → 0.053 |                 22 → 13 |          1472 | DESCARTADO  |
| pedidos-carrera-orden         |  20000 |    500 |      3.998 → 2.181 |               489 → 489 |          1520 | DESCARTADO  |
| pedidos-periodo-orden         |  20000 |  10000 |    47.043 → 39.289 |               487 → 487 |          1056 | DESCARTADO  |
| pedidos-estado-parcial        |  20000 |     10 |      0.161 → 0.141 |                 22 → 22 |            96 | DESCARTADO  |
| tareas-responsable-orden      |  20000 |     50 |      0.115 → 0.108 |                   3 → 3 |           824 | DESCARTADO  |
| tareas-grilla-orden           |  20000 |  20000 |    34.270 → 35.358 |               400 → 400 |           512 | DESCARTADO  |
| proyectos-grilla-orden        |  20000 |   1000 |      0.581 → 0.374 |                 14 → 18 |            96 | DESCARTADO  |
| tareas-padre-duplicado        |  20000 |      9 |      0.057 → 0.060 |                   3 → 3 |           240 | DESCARTADO  |
| tareas-proyecto-duplicado     |  20000 |     20 |      0.101 → 0.087 |                 22 → 22 |           336 | DESCARTADO  |
| relaciones-inversa-duplicado  |  20000 |      1 |      0.252 → 0.058 |                   5 → 5 |           544 | DESCARTADO  |
| designaciones-persona-gist    |  20000 |      1 |      0.045 → 0.054 |                   4 → 4 |           632 | DESCARTADO  |
| designaciones-materia-gist    |  20000 |     20 |      4.229 → 1.312 |               381 → 381 |           232 | JUSTIFICADO |
| designaciones-origen-parcial  |  20000 |      1 |      0.037 → 0.048 |                   3 → 3 |           256 | DESCARTADO  |
| designaciones-materia-vigente |  20000 |     20 |      1.486 → 1.579 |               381 → 381 |           232 | DESCARTADO  |
| historial-actor-fk            |  20000 |     10 |      1.663 → 0.050 |                286 → 12 |           232 | JUSTIFICADO |
| historial-rol-fk              |  20000 |    200 |      1.396 → 0.211 |               286 → 202 |           168 | JUSTIFICADO |
| idempotencia-pedido-fk        |  20000 |      1 |      1.724 → 0.047 |                 328 → 3 |           632 | JUSTIFICADO |
| idempotencia-actor-cubierto   |  20000 |     10 |      0.077 → 0.074 |                 12 → 12 |           248 | DESCARTADO  |
| pedidos-cargo-fk              |  20000 |  20000 |      4.318 → 5.685 |               488 → 488 |           168 | DESCARTADO  |
| pedidos-dedicacion-fk         |  20000 |  20000 |      4.749 → 5.487 |               488 → 488 |           168 | DESCARTADO  |
| designaciones-cargo-fk        |  20000 |  20000 |      8.129 → 4.389 |               372 → 372 |           168 | DESCARTADO  |
| designaciones-dedicacion-fk   |  20000 |  20000 |      6.488 → 6.335 |               372 → 372 |           168 | DESCARTADO  |
| pedidos-materia-orden         | 100000 |     10 |      0.507 → 0.124 |                 22 → 13 |          6728 | DESCARTADO  |
| pedidos-carrera-orden         | 100000 |    500 |      6.103 → 1.917 |               258 → 250 |          6824 | DESCARTADO  |
| pedidos-periodo-orden         | 100000 |  10000 |    39.104 → 39.561 |               255 → 255 |          4944 | DESCARTADO  |
| pedidos-estado-parcial        | 100000 |     10 |      1.317 → 0.419 |                 24 → 24 |           352 | DESCARTADO  |
| tareas-responsable-orden      | 100000 |     50 |      0.116 → 0.111 |                   3 → 3 |          4000 | DESCARTADO  |
| tareas-grilla-orden           | 100000 | 100000 |  286.898 → 124.374 |           2000 → 100275 |          2208 | DESCARTADO  |
| proyectos-grilla-orden        | 100000 |   1000 |      0.620 → 0.416 |                 14 → 18 |            96 | DESCARTADO  |
| tareas-padre-duplicado        | 100000 |      9 |      0.102 → 0.046 |                   3 → 3 |          1008 | DESCARTADO  |
| tareas-proyecto-duplicado     | 100000 |    100 |      0.487 → 0.188 |               102 → 102 |           744 | DESCARTADO  |
| relaciones-inversa-duplicado  | 100000 |      1 |      0.058 → 0.070 |                   7 → 7 |          2656 | DESCARTADO  |
| designaciones-persona-gist    | 100000 |      5 |      0.095 → 0.082 |                   8 → 8 |          1328 | DESCARTADO  |
| designaciones-materia-gist    | 100000 |    100 |     10.147 → 0.144 |              1804 → 102 |           776 | JUSTIFICADO |
| designaciones-origen-parcial  | 100000 |      1 |      0.059 → 0.047 |                   3 → 3 |          1176 | DESCARTADO  |
| designaciones-materia-vigente | 100000 |     20 |      0.077 → 0.089 |                 22 → 22 |           776 | DESCARTADO  |
| historial-actor-fk            | 100000 |     50 |      9.249 → 0.100 |               1429 → 52 |           744 | JUSTIFICADO |
| historial-rol-fk              | 100000 |   1000 |     18.537 → 2.493 |             1472 → 1002 |           712 | JUSTIFICADO |
| idempotencia-pedido-fk        | 100000 |      1 |     14.360 → 0.042 |                1640 → 4 |          3104 | JUSTIFICADO |
| idempotencia-actor-cubierto   | 100000 |     50 |      0.164 → 0.134 |                 53 → 52 |           744 | DESCARTADO  |
| pedidos-cargo-fk              | 100000 | 100000 |    38.619 → 31.861 |             2511 → 2511 |           712 | DESCARTADO  |
| pedidos-dedicacion-fk         | 100000 | 100000 |    87.362 → 65.529 |             2511 → 2511 |           712 | DESCARTADO  |
| designaciones-cargo-fk        | 100000 | 100000 |    26.223 → 22.623 |             1940 → 1940 |           712 | DESCARTADO  |
| designaciones-dedicacion-fk   | 100000 | 100000 |    28.030 → 31.806 |             1940 → 1940 |           712 | DESCARTADO  |

## Writes: 100000 filas de base; tres repeticiones, 500 filas por operación

| Candidato                     | INSERT ms antes → después | INSERT WAL bytes antes → después | UPDATE ms antes → después | UPDATE WAL bytes antes → después |
| ----------------------------- | ------------------------: | -------------------------------: | ------------------------: | -------------------------------: |
| pedidos-materia-orden         |         256.308 → 243.361 |                  357578 → 402639 |         123.033 → 120.420 |                  477407 → 582987 |
| pedidos-carrera-orden         |         449.058 → 448.352 |                  357578 → 402639 |         260.652 → 125.591 |                  477407 → 546395 |
| pedidos-periodo-orden         |         392.890 → 394.021 |                  350271 → 394219 |         325.532 → 161.499 |                  480298 → 520836 |
| pedidos-estado-parcial        |         242.277 → 384.096 |                  350219 → 350219 |         124.662 → 187.104 |                  475416 → 484389 |
| tareas-responsable-orden      |         220.644 → 362.239 |                  283344 → 311088 |          366.718 → 98.592 |                  320399 → 351062 |
| tareas-grilla-orden           |         386.297 → 202.618 |                  271088 → 303088 |         142.792 → 295.139 |                  311062 → 343062 |
| proyectos-grilla-orden        |         252.863 → 331.441 |                  135260 → 171680 |          94.074 → 117.294 |                  175038 → 210315 |
| tareas-padre-duplicado        |         178.635 → 165.405 |                  271088 → 306816 |         112.004 → 135.095 |                  311062 → 348582 |
| tareas-proyecto-duplicado     |         244.472 → 346.433 |                  271088 → 307088 |         128.414 → 155.364 |                  311062 → 348071 |
| relaciones-inversa-duplicado  |           38.348 → 75.004 |                  144786 → 163806 |          188.377 → 65.569 |                  191839 → 201803 |
| designaciones-persona-gist    |         260.395 → 187.768 |                  345837 → 313750 |         231.658 → 256.349 |                  394459 → 420927 |
| designaciones-materia-gist    |         271.378 → 324.769 |                  280619 → 340130 |         383.193 → 211.647 |                  376693 → 418962 |
| designaciones-origen-parcial  |         301.197 → 374.170 |                  288340 → 313521 |         149.838 → 125.013 |                  377395 → 408633 |
| designaciones-materia-vigente |         183.058 → 344.412 |                  284209 → 322228 |         195.609 → 336.540 |                  374265 → 401788 |
| historial-actor-fk            |         554.260 → 171.593 |                  148306 → 184310 |         208.632 → 225.645 |                  188336 → 223013 |
| historial-rol-fk              |         261.904 → 268.720 |                  148138 → 184358 |          147.541 → 88.694 |                  184676 → 223660 |
| idempotencia-pedido-fk        |         121.570 → 131.152 |                  202859 → 235984 |           30.308 → 54.920 |                  242307 → 280580 |
| idempotencia-actor-cubierto   |           84.833 → 72.012 |                  199984 → 236128 |           36.497 → 46.049 |                  252872 → 290875 |
| pedidos-cargo-fk              |         389.209 → 474.784 |                  350219 → 386439 |         300.432 → 282.380 |                  475416 → 514269 |
| pedidos-dedicacion-fk         |         314.416 → 324.303 |                  350219 → 386439 |         218.661 → 214.393 |                  475416 → 514269 |
| designaciones-cargo-fk        |         334.227 → 385.143 |                  282743 → 331879 |         141.794 → 219.579 |                  384943 → 420314 |
| designaciones-dedicacion-fk   |         420.659 → 304.908 |                  277204 → 307618 |         139.406 → 146.119 |                  380110 → 408427 |

El JSON conserva también costo de writes a 20000 filas, primer acceso, min/max de lecturas, planes con nodos/índices concretos, tamaños totales de tabla/índices y todos los SQL ejecutados.

## Motivo por candidato

- **pedidos-materia-orden — DESCARTADO:** Reduce 22 a 13 bloques, pero el acceso existente ya está acotado por índices simples/BitmapAnd en ambos tamaños; sort de sólo diez filas, retenido por el planner en 100000. No compensa mantener un compuesto grande y más WAL por ahorrar nueve bloques en este escenario.
- **pedidos-carrera-orden — DESCARTADO:** El ámbito amplio ya cuenta con acceso por carrera/período; no reduce buffers de forma material en ambos volúmenes. No justificar otro índice sólo por variación temporal.
- **pedidos-periodo-orden — DESCARTADO:** La grilla completa necesita las 10000 filas del período; buffers similares y mantenimiento adicional. No se agrega paginación para aparentar beneficio.
- **pedidos-estado-parcial — DESCARTADO:** La consulta real incluye estados terminales; no implica el predicado parcial y el planner no usa el candidato. No trasladar el filtro de presentación a SQL alterando el contrato.
- **tareas-responsable-orden — DESCARTADO:** 50 visibles constantes; índice responsable existente y sort pequeño. Buffers prácticamente constantes; mayor tamaño/WAL para beneficio no material.
- **tareas-grilla-orden — DESCARTADO:** Grilla administrativa completa: todos los registros y su ancho. Ordenar no reduce cardinalidad; no demuestra beneficio estructural consistente.
- **proyectos-grilla-orden — DESCARTADO:** 1000 proyectos completos; el ahorro de un sort chico no compensa otro índice sin beneficio de buffers. No extrapolar tiempos de este catálogo.
- **tareas-padre-duplicado — DESCARTADO:** FK y paso recursivo ya cubiertos por tareas_padre_idx; se midió el duplicado sin aportar acceso nuevo.
- **tareas-proyecto-duplicado — DESCARTADO:** FK ya cubierta por tareas_proyecto_idx; igualdad medida antes/después no necesita duplicación.
- **relaciones-inversa-duplicado — DESCARTADO:** OR en ambos sentidos cubierto por PK(tarea_id,relacionada_id) y tarea_relaciones_relacionada_idx; duplicado no reduce trabajo.
- **designaciones-persona-gist — DESCARTADO:** EXCLUDE GiST se usa realmente para igualdad de persona histórica; no marcar la FK como descubierta por ignorar GiST.
- **designaciones-materia-gist — JUSTIFICADO:** GiST sí cubre igualdad en la segunda clave, pero con 100000 filas históricas lee 1804 bloques frente a 102 del B-tree elegido naturalmente. A 20000 conserva GiST y 381 bloques; no atribuir la fluctuación temporal a una mejora de plan. Justificado por crecimiento de historia sin regresión estructural en el tamaño menor; mantener EXCLUDE y el parcial de vigencias.
- **designaciones-origen-parcial — DESCARTADO:** Igualdad con UUID implica IS NOT NULL: designaciones_origen_pedido_idx cubre la FK. El candidato total añade tamaño/WAL sin acceso necesario.
- **designaciones-materia-vigente — DESCARTADO:** Predicado coincide con índice parcial existente; no duplicar por un scanner que excluye parciales. También puede elegirse GiST según costos.
- **historial-actor-fk — JUSTIFICADO:** Elimina scan global para obtener referencias por actor, selectividad 1/2000. Sonda de mantenimiento/FK, no supuesto endpoint paginado. Proponer incremental tras integración.
- **historial-rol-fk — JUSTIFICADO:** Rol raro 1%: elimina filtrado de 99% del historial; buffers bajan menos que el tiempo porque las referencias están dispersas. Proponer incremental tras integración; no asumir la misma mejora para el rol común.
- **idempotencia-pedido-fk — JUSTIFICADO:** pedido_id no es prefijo del UNIQUE(actor,ruta,clave); lookup/cascada evita recorrer todos los comandos. Una referencia por pedido en ambos volúmenes. Proponer incremental tras integración.
- **idempotencia-actor-cubierto — DESCARTADO:** actor_id ya es prefijo de idempotencia_actor_ruta_clave; el plan existente resuelve igualdad selectiva.
- **pedidos-cargo-fk — DESCARTADO:** 100% de pedidos usan este cargo en el escenario concentrado; seq scan es razonable y candidato no reduce trabajo. No universalizar este descarte a otra distribución.
- **pedidos-dedicacion-fk — DESCARTADO:** Dedicación no selectiva (100%); no reduce buffers. Índice no justificado para la distribución ensayada.
- **designaciones-cargo-fk — DESCARTADO:** Catálogo concentrado: 100% de filas coinciden. Variación temporal con buffers iguales no justifica índice.
- **designaciones-dedicacion-fk — DESCARTADO:** Catálogo concentrado: 100% de filas coinciden; costo adicional sin acceso selectivo material.

## Incrementales propuestas al integrador

```sql
CREATE INDEX designaciones_materia_idx ON designaciones.designaciones (materia_id);
CREATE INDEX pedido_historial_actor_idx ON designaciones.pedido_historial (actor_id);
CREATE INDEX pedido_historial_rol_idx ON designaciones.pedido_historial (rol_id);
CREATE INDEX idempotencia_pedido_idx ON designaciones.idempotencia_comandos (pedido_id);
```

Antes de crear migraciones, comparar el catálogo del candidato integrado con estos DDL para no duplicar índices agregados por otros agentes. La integración debe generar SQL+wrapper incremental y validar instalación/upgrade, recursos y snapshot. La tarea 6.6 no se marca completa por este informe si las propuestas justificadas todavía no fueron integradas o descartadas mediante nueva evidencia.

## Integración verificada

El integrador generó SQL+wrapper incremental
`20261013175617_IndicesSelectivosMedidosDesignaciones` con los cuatro índices
justificados. No modificó SQL/wrappers históricos ni el manifiesto protegido.
`IndicesSelectivosMedidosTests` verificó instalación nueva y upgrade desde el
baseline Designaciones mediante el migrador EF real: **2/2 aprobadas**, cero
fallos/omitidas (`.artifacts/apply-post-auditoria/indices-selectivos.trx`).

El catálogo confirmó los cuatro B-tree presentes, listos y válidos; una vigencia
sintética y los eventos existentes conservaron su huella, y la reejecución fue
no-op. El validador Git devolvió `proteccionGit: true` contra el SHA anterior
fijado. El CLI Release `--validar-recursos` confirmó recursos reales embebidos.
La verificación del upgrade completo de todos los contextos permanece separada.

## Huellas reproducibles

- Imagen real: `sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636`.
- `datos.sql` SHA-256: `ed1061caf97ac3b2c507aa07e3e518793ce55a49c9e9e0e14236972adc3093f4`.
- `candidatos.json` SHA-256: `0d6ec1d0c76eca60238546bcc81a8ba9f5f213c4ba84aa0562ac13636eb90464`.
- `resultado.json` SHA-256: `c06706cc922fc4b51d045f40ec4c5a35dd47fa16ede24e5589b9c849db880f70`.
