#!/usr/bin/env python3
"""Genera tablas verificables del JSON real; no inventa ni ejecuta mediciones."""
import hashlib
import json
from pathlib import Path

FIXTURE = Path(__file__).resolve().parent
RAIZ = FIXTURE.parents[3]
INFORME = RAIZ / 'openspec/changes/corregir-integridad-y-rendimiento-post-auditoria/evidence/indices-candidatos-implementacion.md'

MOTIVOS = {
    'pedidos-materia-orden': ('DESCARTADO', 'Reduce 22 a 13 bloques, pero el acceso existente ya está acotado por índices simples/BitmapAnd en ambos tamaños; sort de sólo diez filas, retenido por el planner en 100000. No compensa mantener un compuesto grande y más WAL por ahorrar nueve bloques en este escenario.'),
    'pedidos-carrera-orden': ('DESCARTADO', 'El ámbito amplio ya cuenta con acceso por carrera/período; no reduce buffers de forma material en ambos volúmenes. No justificar otro índice sólo por variación temporal.'),
    'pedidos-periodo-orden': ('DESCARTADO', 'La grilla completa necesita las 10000 filas del período; buffers similares y mantenimiento adicional. No se agrega paginación para aparentar beneficio.'),
    'pedidos-estado-parcial': ('DESCARTADO', 'La consulta real incluye estados terminales; no implica el predicado parcial y el planner no usa el candidato. No trasladar el filtro de presentación a SQL alterando el contrato.'),
    'tareas-responsable-orden': ('DESCARTADO', '50 visibles constantes; índice responsable existente y sort pequeño. Buffers prácticamente constantes; mayor tamaño/WAL para beneficio no material.'),
    'tareas-grilla-orden': ('DESCARTADO', 'Grilla administrativa completa: todos los registros y su ancho. Ordenar no reduce cardinalidad; no demuestra beneficio estructural consistente.'),
    'proyectos-grilla-orden': ('DESCARTADO', '1000 proyectos completos; el ahorro de un sort chico no compensa otro índice sin beneficio de buffers. No extrapolar tiempos de este catálogo.'),
    'tareas-padre-duplicado': ('DESCARTADO', 'FK y paso recursivo ya cubiertos por tareas_padre_idx; se midió el duplicado sin aportar acceso nuevo.'),
    'tareas-proyecto-duplicado': ('DESCARTADO', 'FK ya cubierta por tareas_proyecto_idx; igualdad medida antes/después no necesita duplicación.'),
    'relaciones-inversa-duplicado': ('DESCARTADO', 'OR en ambos sentidos cubierto por PK(tarea_id,relacionada_id) y tarea_relaciones_relacionada_idx; duplicado no reduce trabajo.'),
    'designaciones-persona-gist': ('DESCARTADO', 'EXCLUDE GiST se usa realmente para igualdad de persona histórica; no marcar la FK como descubierta por ignorar GiST.'),
    'designaciones-materia-gist': ('JUSTIFICADO', 'GiST sí cubre igualdad en la segunda clave, pero con 100000 filas históricas lee 1804 bloques frente a 102 del B-tree elegido naturalmente. A 20000 conserva GiST y 381 bloques; no atribuir la fluctuación temporal a una mejora de plan. Justificado por crecimiento de historia sin regresión estructural en el tamaño menor; mantener EXCLUDE y el parcial de vigencias.'),
    'designaciones-origen-parcial': ('DESCARTADO', 'Igualdad con UUID implica IS NOT NULL: designaciones_origen_pedido_idx cubre la FK. El candidato total añade tamaño/WAL sin acceso necesario.'),
    'designaciones-materia-vigente': ('DESCARTADO', 'Predicado coincide con índice parcial existente; no duplicar por un scanner que excluye parciales. También puede elegirse GiST según costos.'),
    'historial-actor-fk': ('JUSTIFICADO', 'Elimina scan global para obtener referencias por actor, selectividad 1/2000. Sonda de mantenimiento/FK, no supuesto endpoint paginado. Proponer incremental tras integración.'),
    'historial-rol-fk': ('JUSTIFICADO', 'Rol raro 1%: elimina filtrado de 99% del historial; buffers bajan menos que el tiempo porque las referencias están dispersas. Proponer incremental tras integración; no asumir la misma mejora para el rol común.'),
    'idempotencia-pedido-fk': ('JUSTIFICADO', 'pedido_id no es prefijo del UNIQUE(actor,ruta,clave); lookup/cascada evita recorrer todos los comandos. Una referencia por pedido en ambos volúmenes. Proponer incremental tras integración.'),
    'idempotencia-actor-cubierto': ('DESCARTADO', 'actor_id ya es prefijo de idempotencia_actor_ruta_clave; el plan existente resuelve igualdad selectiva.'),
    'pedidos-cargo-fk': ('DESCARTADO', '100% de pedidos usan este cargo en el escenario concentrado; seq scan es razonable y candidato no reduce trabajo. No universalizar este descarte a otra distribución.'),
    'pedidos-dedicacion-fk': ('DESCARTADO', 'Dedicación no selectiva (100%); no reduce buffers. Índice no justificado para la distribución ensayada.'),
    'designaciones-cargo-fk': ('DESCARTADO', 'Catálogo concentrado: 100% de filas coinciden. Variación temporal con buffers iguales no justifica índice.'),
    'designaciones-dedicacion-fk': ('DESCARTADO', 'Catálogo concentrado: 100% de filas coinciden; costo adicional sin acceso selectivo material.'),
}


def generar():
    resultado = json.loads((FIXTURE / 'resultado.json').read_text())
    candidatos = json.loads((FIXTURE / 'candidatos.json').read_text())
    assert resultado.get('completo') and resultado.get('contenedor_eliminado')
    assert len(resultado['resultados']) == len(candidatos) * 2
    assert set(MOTIVOS) == {c['id'] for c in candidatos}
    lineas = [
        '# Índices candidatos: implementación de tarea 6.6', '',
        '## Alcance y reproducción', '',
        '- PostgreSQL 18.4 real, contenedor Docker propio sin red, puertos publicados ni binds; SQL y resultados por stdin/stdout. Limpieza comprobada por ausencia del nombre exacto.',
        f"- Baseline estructural fijado a `{resultado['sha_baseline']}`; recursos originales y SHA-256 individuales en `resultado.json`. No se ejecutó el runner EF ni se simularon histories: este ensayo mide DDL y consultas, no un upgrade de release.",
        '- Consultas v1: traducción SQL explícita de predicados/orden de `RepositorioPedidos.ListarPorMateriasAsync`, `ListarPorCarrerasAsync`, `ListarDelPeriodoAsync`; `RepositorioTareas.ListarAsync`, `ListarProyectosAsync`, `ListarRelacionesDeAsync`, y paso de hijos de `DescendientesAsync`. Localización inicial con CodeGraph. No se presenta este SQL como captura de EF ni benchmark HTTP; joins de catálogos/colecciones no están incluidos en la medida de acceso a la tabla principal.',
        '- Las sondas FK consultan todas las filas referenciantes mediante igualdad. No son un DELETE administrativo ni una medida exacta de la consulta interna RI (que puede parar al primer match para RESTRICT); la cascada por pedido sí necesita encontrar todas sus referencias. No se ensayó borrado de catálogos ni se agregaron FKs intermodulares.',
        '- Dataset v1: 20000 y 100000 pedidos/Tareas/designaciones/historiales/comandos; 1000 materias, 20 carreras, 10 períodos, 2000 usuarios sintéticos, 1000 proyectos, diez roles sintéticos además del catálogo. Pedidos: 5% en revisión y resto cancelados/rechazados, 1/7 prioritarios. El período consultado conserva 10000 filas, una materia diez pedidos, una carrera 500. Tareas del responsable: 50 constantes al crecer datos ajenos. Vigencias: una y cinco etapas por 20000 personas; 20000 abiertas. Catálogos cargo/dedicación deliberadamente concentrados (100%) para control no selectivo.',
        '- Antes/después pareados para cada índice, un candidato a la vez. `VACUUM ANALYZE` tras preparar datos y writes revertidos; no se fuerza `enable_seqscan` ni se ocultan planes que rechazan el candidato. No hay LIMIT visible ni cambios de contrato.',
        '- Cada fase: reinicio PostgreSQL, primer acceso con shared_buffers frío, luego cinco lecturas warm. Page cache del host no controlado: **no es cold disk**. Se conservan todos los planes ANALYZE/BUFFERS/WAL, tiempos y rangos; el reinicio no elimina cachés del daemon/host.',
        '- Escrituras: tres repeticiones reales de INSERT de 500 filas y UPDATE de 500 filas por fase, cada una con ROLLBACK. Triggers, FKs e índices originales activos; volumen de WAL y tiempos medidos. El UPDATE toca created_at (permite HOT cuando no está indexado), no pretende representar todos los cambios de FK/estado. Inserción de pedidos cancelados evita colisión de cupo; designaciones insertadas cambian a otra materia válida para respetar EXCLUDE. El costo de las secuencias puede dejar gaps, sólo dentro del contenedor desechable.',
        '- Cardinalidad y hash de todas las columnas conservados antes/después (hash ordenado por contenido para consultas sin orden). Los tiempos son observaciones de esta máquina concurrente, no SLA ni factor productivo.', '',
        '```bash',
        'python3 scripts/tests/fixtures/rendimiento-candidatos/medir.py',
        'python3 scripts/tests/fixtures/rendimiento-candidatos/informar.py',
        'node --test scripts/tests/rendimiento-candidatos.test.mjs',
        '```', '',
        'Requiere Docker accesible, imagen `postgres:18.4`, Python 3 y Git con el SHA auditado. No admite una URL de base externa. El benchmark tarda varios minutos; un timeout del cliente no implica que terminó el proceso SSH: consultar su PID y JSON de progreso, no lanzar otro en paralelo.', '',
        '## Criterio de decisión', '',
        'Justificar un índice sólo si agrega acceso eficiente necesario y se comparan ambos tamaños: beneficio de bloques/filtrado global u orden útil en el volumen representativo, sin regresión estructural en el menor, cardinalidad íntegra y costo de escritura/tamaño explícitos. Cobertura ya existente, candidato no elegido, baja selectividad o fluctuación temporal sin reducir trabajo motivan descarte. No se exige un umbral de milisegundos absoluto. JUSTIFICADO significa propuesta al integrador: **no se agregó ningún índice productivo en esta tarea**.', '',
        '## Lecturas: medianas de cinco repeticiones', '',
        '| Candidato | N | Filas | ms antes → después | buffers antes → después | KiB candidato | Decisión |',
        '|---|---:|---:|---:|---:|---:|---|',
    ]
    for r in resultado['resultados']:
        a, b = r['fases']['antes'], r['fases']['despues']
        lineas.append(f"| {r['candidato']['id']} | {r['n']} | {a['contenido']['filas']} | {a['resumen_lectura']['ms_mediana']:.3f} → {b['resumen_lectura']['ms_mediana']:.3f} | {a['resumen_lectura']['buffers_mediana']:g} → {b['resumen_lectura']['buffers_mediana']:g} | {b['tamano']['candidato_bytes']/1024:.0f} | {MOTIVOS[r['candidato']['id']][0]} |")
    lineas += ['', '## Writes: 100000 filas de base; tres repeticiones, 500 filas por operación', '',
        '| Candidato | INSERT ms antes → después | INSERT WAL bytes antes → después | UPDATE ms antes → después | UPDATE WAL bytes antes → después |',
        '|---|---:|---:|---:|---:|']
    for r in resultado['resultados']:
        if r['n'] != 100000:
            continue
        a, b = r['fases']['antes'], r['fases']['despues']
        lineas.append(f"| {r['candidato']['id']} | {a['resumen_insercion']['ms_mediana']:.3f} → {b['resumen_insercion']['ms_mediana']:.3f} | {a['resumen_insercion']['wal_bytes_mediana']:g} → {b['resumen_insercion']['wal_bytes_mediana']:g} | {a['resumen_update']['ms_mediana']:.3f} → {b['resumen_update']['ms_mediana']:.3f} | {a['resumen_update']['wal_bytes_mediana']:g} → {b['resumen_update']['wal_bytes_mediana']:g} |")
    lineas += ['', 'El JSON conserva también costo de writes a 20000 filas, primer acceso, min/max de lecturas, planes con nodos/índices concretos, tamaños totales de tabla/índices y todos los SQL ejecutados.', '', '## Motivo por candidato', '']
    for c in candidatos:
        decision, motivo = MOTIVOS[c['id']]
        lineas.append(f"- **{c['id']} — {decision}:** {motivo}")
    lineas += ['', '## Incrementales propuestas al integrador', '', '```sql']
    for c in candidatos:
        if MOTIVOS[c['id']][0] == 'JUSTIFICADO':
            nombre = {
                'designaciones-materia-gist': 'designaciones_materia_idx',
                'historial-actor-fk': 'pedido_historial_actor_idx',
                'historial-rol-fk': 'pedido_historial_rol_idx',
                'idempotencia-pedido-fk': 'idempotencia_pedido_idx',
            }[c['id']]
            lineas.append(f"CREATE INDEX {nombre} ON {c['tabla']} ({c['columnas']});")
    lineas += ['```', '', 'Antes de crear migraciones, comparar el catálogo del candidato integrado con estos DDL para no duplicar índices agregados por otros agentes. La integración debe generar SQL+wrapper incremental y validar instalación/upgrade, recursos y snapshot. La tarea 6.6 no se marca completa por este informe si las propuestas justificadas todavía no fueron integradas o descartadas mediante nueva evidencia.', '', '## Huellas reproducibles', '', f"- Imagen real: `{resultado['imagen_id']}`."]
    for nombre in ['datos.sql', 'candidatos.json', 'resultado.json']:
        lineas.append(f"- `{nombre}` SHA-256: `{hashlib.sha256((FIXTURE / nombre).read_bytes()).hexdigest()}`.")
    INFORME.write_text('\n'.join(lineas) + '\n')
    print(f'Informe generado: {len(candidatos)} candidatos, {len(resultado["resultados"])} pares, limpieza verificada')


if __name__ == '__main__':
    generar()
