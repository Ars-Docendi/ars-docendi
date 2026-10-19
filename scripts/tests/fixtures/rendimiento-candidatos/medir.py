#!/usr/bin/env python3
"""Benchmark real aislado PostgreSQL 18; exclusivamente stdin/stdout Docker."""
import hashlib
import json
import sys
from pathlib import Path
import statistics
import subprocess
import time
import uuid

RAIZ = Path(__file__).resolve().parents[4]
FIXTURE = Path(__file__).resolve().parent
SHA = 'f10e0a5ba2a20e14a83be0dea3355b1bf1d4601b'
IMAGEN = 'postgres:18.4'
REPETICIONES = 5


def comando(args, entrada=None, comprobar=True):
    p = subprocess.run(args, input=entrada, text=True, capture_output=True, cwd=RAIZ)
    if comprobar and p.returncode:
        raise RuntimeError(f'{args[0:3]}: {p.stderr[-3000:]}')
    return p


def objetos(texto):
    decoder = json.JSONDecoder()
    salida = []
    while texto.strip():
        texto = texto.lstrip()
        valor, fin = decoder.raw_decode(texto)
        salida.append(valor)
        texto = texto[fin:]
    return salida


def git(ruta):
    return comando(['git', 'show', f'{SHA}:{ruta}']).stdout


def resumen(planes):
    return {
        'ms_mediana': statistics.median(p['Execution Time'] for p in planes),
        'ms_min': min(p['Execution Time'] for p in planes),
        'ms_max': max(p['Execution Time'] for p in planes),
        'buffers_mediana': statistics.median(
            p['Plan'].get('Shared Hit Blocks', 0) + p['Plan'].get('Shared Read Blocks', 0)
            for p in planes),
        'wal_bytes_mediana': statistics.median(p['Plan'].get('WAL Bytes', 0) for p in planes),
    }


def medir():
    candidatos = json.loads((FIXTURE / 'candidatos.json').read_text())
    nombre = 'ars-bench-candidatos-' + uuid.uuid4().hex[:12]
    resultado = {'version_fixture': 1, 'sha_baseline': SHA, 'imagen': IMAGEN,
                 'repeticiones_warm': REPETICIONES, 'resultados': [], 'recursos_sql': []}
    contenedor = None
    salida = FIXTURE / 'resultado.json'
    if '--continuar' in sys.argv:
        previo = json.loads(salida.read_text())
        assert previo['sha_baseline'] == SHA and previo.get('contenedor_eliminado')
        assert all(r['candidato'] in candidatos for r in previo['resultados'])
        resultado['resultados'] = previo['resultados']
        resultado['catalogos'] = previo.get('catalogos', [])
        resultado['continuado_desde_pares'] = len(previo['resultados'])
    completos = {(r['n'], r['candidato']['id']) for r in resultado['resultados']}
    try:
        contenedor = comando(['docker', 'run', '-d', '--name', nombre,
                              '--network', 'none', '-e', 'POSTGRES_HOST_AUTH_METHOD=trust',
                              IMAGEN, '-c', 'shared_buffers=128MB', '-c', 'jit=off',
                              '-c', 'max_parallel_workers_per_gather=0']).stdout.strip()

        def esperar():
            for _ in range(120):
                if comando(['docker', 'exec', contenedor, 'pg_isready', '-U', 'postgres'],
                           comprobar=False).returncode == 0:
                    return
                time.sleep(.25)
            raise RuntimeError('PostgreSQL desechable no inició')

        def sql(texto, base='postgres'):
            return comando(['docker', 'exec', '-i', contenedor, 'psql', '-X', '-qAt',
                            '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', base], texto).stdout

        def explicar(consulta, rep=REPETICIONES, transaccion=False):
            fragmentos = []
            for _ in range(rep):
                sentencia = f'EXPLAIN (ANALYZE, BUFFERS, WAL, FORMAT JSON) {consulta};'
                fragmentos.append(('BEGIN; ' + sentencia + ' ROLLBACK;') if transaccion else sentencia)
            return [v[0] for v in objetos(sql('\n'.join(fragmentos)))]

        esperar()
        resultado['postgres'] = sql('SELECT version();').strip()
        resultado['imagen_id'] = comando(['docker', 'inspect', '-f', '{{.Image}}', contenedor]).stdout.strip()
        recursos = ['database/identity/001_identity_users.sql', 'database/audit/001_audit_schema.sql']
        recursos += [f'database/identity/{p}' for p in [
            '002_identity_autorizacion.sql', '003_identity_catalogo_academico.sql',
            '004_identity_personas_ambitos.sql', '005_identity_integridad.sql',
            '006_identity_catalogos.sql', '007_identity_audit_attach.sql',
            '012_identity_permisos_tareas.sql', '013_identity_users_cuenta_microsoft.sql']]
        for esquema in ['storage', 'designaciones', 'portal', 'tareas']:
            recursos += comando(['git', 'ls-tree', '-r', '--name-only', SHA,
                                  f'database/{esquema}']).stdout.splitlines()
        for ruta in recursos:
            contenido = git(ruta)
            sql(contenido)
            resultado['recursos_sql'].append({'ruta': ruta, 'sha256': hashlib.sha256(contenido.encode()).hexdigest()})
        sql('CREATE DATABASE plantilla_bench WITH TEMPLATE postgres;')
        for n in [20000, 100000]:
            if all((n, c['id']) in completos for c in candidatos):
                continue
            resultado['catalogos'] = [v for v in resultado.get('catalogos', []) if v['n'] != n]
            sql('DROP DATABASE postgres WITH (FORCE);', 'template1')
            sql('CREATE DATABASE postgres WITH TEMPLATE plantilla_bench;', 'template1')
            sql((FIXTURE / 'datos.sql').read_text().replace(':n', str(n)))
            resultado.setdefault('catalogos', []).append({'n': n, 'indices': objetos(sql(
                "SELECT coalesce(json_agg(json_build_object('schema',schemaname,'tabla',tablename,"
                "'nombre',indexname,'ddl',indexdef) ORDER BY schemaname,indexname),'[]') FROM pg_indexes "
                "WHERE schemaname IN ('identity','designaciones','tareas');"))[0]})
            for c in candidatos:
                if (n, c['id']) in completos:
                    continue
                consulta = f"SELECT * FROM {c['tabla']} WHERE {c['filtro']}"
                if c.get('orden'):
                    consulta += ' ORDER BY ' + c['orden']
                registro = {'n': n, 'candidato': c, 'consulta': consulta, 'fases': {}}
                for fase in ['antes', 'despues']:
                    if fase == 'despues':
                        ddl = f"CREATE INDEX bench_candidato ON {c['tabla']} ({c['columnas']})"
                        if c.get('parcial'):
                            ddl += ' WHERE ' + c['parcial']
                        sql(ddl + ';')
                        registro['ddl'] = ddl
                    comando(['docker', 'restart', '--time', '2', contenedor])
                    esperar()
                    # Reinicia shared_buffers; NO vacía page cache del host.
                    primero = explicar(consulta, 1)[0]
                    warm = explicar(consulta)
                    contenido = objetos(sql(
                        "SELECT json_build_object('filas',count(*),'hash',md5(coalesce("
                        f"string_agg(row_to_json(q)::text,'|' ORDER BY row_to_json(q)::text),''))) FROM ({consulta}) q;"))[0]
                    schema, tabla = c['tabla'].split('.')
                    columnas = sql(f"SELECT string_agg(column_name,',' ORDER BY ordinal_position) "
                                   f"FROM information_schema.columns WHERE table_schema='{schema}' "
                                   f"AND table_name='{tabla}' AND is_generated='NEVER';").strip().split(',')
                    proyeccion = []
                    for columna in columnas:
                        if columna in ['id', 'clave']:
                            proyeccion.append(f"md5('{columna}:'||s.id::text)::uuid")
                        elif columna == 'estado' and tabla == 'pedidos':
                            proyeccion.append("'cancelado'")
                        elif columna == 'materia_id' and tabla == 'designaciones':
                            proyeccion.append("CASE WHEN s.materia_id=bench_id('materia',1) THEN bench_id('materia',2) ELSE bench_id('materia',1) END")
                        elif columna == 'numero':
                            proyeccion.append("'write-'||s.numero" if tabla == 'pedidos' else '-s.numero')
                        else:
                            proyeccion.append('s.' + columna)
                    if 'id' in columnas:
                        insertar = (f"INSERT INTO {c['tabla']} ({','.join(columnas)}) SELECT "
                                    f"{','.join(proyeccion)} FROM {c['tabla']} s ORDER BY s.id LIMIT 500")
                    else:
                        # Pares canónicos existentes: medir inserción de 500 relaciones nuevas válidas.
                        insertar = ("INSERT INTO tareas.tarea_relaciones(tarea_id,relacionada_id) SELECT "
                                    "least(bench_id('tarea',i),bench_id('tarea',i+2)),"
                                    "greatest(bench_id('tarea',i),bench_id('tarea',i+2)) "
                                    "FROM generate_series(1,500) i")
                    inserciones = explicar(insertar, 3, True)
                    # Mutación ordinaria no indexada: incluye triggers reales, permite HOT.
                    actualizar = f"UPDATE {c['tabla']} SET created_at=created_at+interval '1 second' "
                    actualizar += f"WHERE ctid IN (SELECT ctid FROM {c['tabla']} LIMIT 500)"
                    updates = explicar(actualizar, 3, True)
                    tamano = objetos(sql(f"SELECT json_build_object('tabla_bytes',pg_table_size('{c['tabla']}'),"
                        f"'indices_bytes',pg_indexes_size('{c['tabla']}'),'candidato_bytes',"
                        + (f"pg_relation_size('{schema}.bench_candidato')" if fase == 'despues' else '0') + ');'))[0]
                    registro['fases'][fase] = {'shared_cold_os_no_controlado': primero,
                        'warm': warm, 'resumen_lectura': resumen(warm), 'contenido': contenido,
                        'insert_sql': insertar, 'inserciones': inserciones,
                        'resumen_insercion': resumen(inserciones), 'update_sql': actualizar,
                        'updates': updates, 'resumen_update': resumen(updates), 'tamano': tamano}
                    sql(f"VACUUM ANALYZE {c['tabla']}; VACUUM audit.change_log;")
                assert registro['fases']['antes']['contenido'] == registro['fases']['despues']['contenido'], c['id']
                sql(f"DROP INDEX {schema}.bench_candidato;")
                resultado['resultados'].append(registro)
                salida.write_text(json.dumps(resultado, ensure_ascii=False, indent=2) + '\n')
                print(f"{n} {c['id']}: " + json.dumps({k:v['resumen_lectura'] for k,v in registro['fases'].items()}), flush=True)
        resultado['completo'] = len(resultado['resultados']) == len(candidatos) * 2
    except Exception as error:
        resultado['error'] = str(error)
        raise
    finally:
        if contenedor:
            comando(['docker', 'rm', '-f', contenedor])
            comprobacion = comando(['docker', 'inspect', nombre], comprobar=False)
            resultado['contenedor_eliminado'] = comprobacion.returncode != 0 and 'no such' in comprobacion.stderr.lower()
            if not resultado['contenedor_eliminado']:
                raise RuntimeError('No se pudo verificar limpieza del contenedor propio')
        salida.write_text(json.dumps(resultado, ensure_ascii=False, indent=2) + '\n')
    return resultado


if __name__ == '__main__':
    medir()
