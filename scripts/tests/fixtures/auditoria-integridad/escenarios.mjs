// Reproducciones del DDL anterior, no pruebas de corrección del candidato.
const persona = "'10000000-0000-4000-8000-000000000001'";
const carrera = "'10000000-0000-4000-8000-000000000003'";
const materia = "'10000000-0000-4000-8000-000000000004'";
const rol = "'10000000-0000-4000-8000-000000000005'";
const periodo = "'10000000-0000-4000-8000-000000000006'";
const perfil = "'10000000-0000-4000-8000-000000000007'";
const tarea = "'10000000-0000-4000-8000-000000000008'";
const designacion = "'10000000-0000-4000-8000-000000000009'";
const inexistente = "'ffffffff-ffff-4fff-8fff-ffffffffffff'";
const catalogo = (tabla) =>
  `(SELECT id FROM designaciones.${tabla} WHERE activo ORDER BY id LIMIT 1)`;

export const escenarios = [
  {
    id: "numeracion",
    hallazgos: ["A03"],
    clasificacion: "SQL",
    limite: "Función real; no alta HTTP, concurrencia ni gaps tras rollback.",
    esperado: { sufijos: ["1000", "9999", "1000", "1000"], distintos: 2 },
    sql: `CREATE TEMP TABLE numeros (orden int, numero text);
      SELECT setval('designaciones.pedidos_numero_seq', 1000, false);
      INSERT INTO numeros VALUES (1, designaciones.siguiente_numero_pedido());
      SELECT setval('designaciones.pedidos_numero_seq', 9999, false);
      INSERT INTO numeros VALUES (2, designaciones.siguiente_numero_pedido());
      SELECT setval('designaciones.pedidos_numero_seq', 10000, false);
      INSERT INTO numeros VALUES (3, designaciones.siguiente_numero_pedido());
      SELECT setval('designaciones.pedidos_numero_seq', 10001, false);
      INSERT INTO numeros VALUES (4, designaciones.siguiente_numero_pedido());
      SELECT json_build_object('sufijos', array_agg(split_part(numero, '-', 2) ORDER BY orden),
        'distintos', count(DISTINCT numero)) FROM numeros;`,
  },
  {
    id: "materia-huerfana",
    hallazgos: ["A09"],
    clasificacion: "SQL",
    limite: "SQL directo; administración sí valida materia. No demuestra bypass de API.",
    esperado: { huerfanas: 1 },
    sql: `INSERT INTO designaciones.designaciones
      (persona_id, materia_id, carrera_id, cargo_id, dedicacion_id, horas, vigente_desde)
      VALUES (${persona}, ${inexistente}, ${carrera}, ${catalogo("cargos")}, ${catalogo("dedicaciones")}, 10, '2026-10-13');
      SELECT json_build_object('huerfanas', count(*)) FROM designaciones.designaciones d
      LEFT JOIN identity.materias m ON m.id = d.materia_id WHERE m.id IS NULL;`,
  },
  {
    id: "persona-perfil-huerfana",
    hallazgos: ["A09"],
    clasificacion: "SQL",
    limite: "Referencia lógica deliberada; no alta arbitraria por API demostrada.",
    esperado: { huerfanos: 1 },
    sql: `INSERT INTO portal.perfiles (persona_id) VALUES (${inexistente});
      SELECT json_build_object('huerfanos', count(*)) FROM portal.perfiles p
      LEFT JOIN identity.personas i ON i.id = p.persona_id WHERE i.id IS NULL;`,
  },
  {
    id: "upn-equivalente",
    hallazgos: ["A07"],
    clasificacion: "SQL",
    limite: "SQL case y espacios; alta administrativa normaliza. Sin carrera de login/alta.",
    esperado: { lower: 2, normalizados: 3 },
    sql: `INSERT INTO identity.users (upn, display_name) VALUES
      ('PROBE@example.invalid', 'Sintetico'), (' probe@example.invalid ', 'Sintetico');
      SELECT json_build_object('lower', count(*) FILTER (WHERE lower(upn) = 'probe@example.invalid'),
        'normalizados', count(*) FILTER (WHERE lower(btrim(upn)) = 'probe@example.invalid')) FROM identity.users;`,
  },
  {
    id: "scope-asignacion-viva",
    hallazgos: ["A10"],
    clasificacion: "SQL",
    limite: "Cambio SQL del rol; no edición HTTP ni intercalación grant-vs-scope.",
    esperado: { incoherentes: 1 },
    sql: `UPDATE identity.roles SET scope = 'materia' WHERE id = ${rol};
      SELECT json_build_object('incoherentes', count(*)) FROM identity.user_roles ur
      JOIN identity.roles r ON r.id = ur.role_id WHERE r.id = ${rol}
      AND ur.deleted_at IS NULL AND r.scope = 'materia' AND ur.materia_id IS NULL;`,
  },
  {
    id: "usuario-tarea-huerfano",
    hallazgos: ["A09"],
    clasificacion: "SQL",
    limite:
      "Referencia lógica deliberada; servicio valida responsable. No API ni borrado concurrente.",
    esperado: { huerfanos: 1 },
    sql: `INSERT INTO tareas.tareas
      (titulo, fecha_inicio, fecha_fin, prioridad_id, tipo_id, estado_id, responsable_id, creado_por_id)
      SELECT 'Tarea huerfana sintetica', fecha_inicio, fecha_fin, prioridad_id, tipo_id, estado_id,
        ${inexistente}, creado_por_id FROM tareas.tareas WHERE id = ${tarea};
      SELECT json_build_object('huerfanos', count(*)) FROM tareas.tareas t
      LEFT JOIN identity.users u ON u.id = t.responsable_id WHERE u.id IS NULL;`,
  },
  {
    id: "resuelta-sin-solucion",
    hallazgos: ["A13"],
    clasificacion: "SQL",
    limite: "DDL con catálogo numérico; API exige solución. No transición HTTP.",
    esperado: { invalidas: 1 },
    sql: `UPDATE tareas.tareas SET estado_id = (SELECT id FROM tareas.estados_tarea WHERE codigo = 'resuelta'),
      solucion = NULL WHERE id = ${tarea};
      SELECT json_build_object('invalidas', count(*)) FROM tareas.tareas t
      JOIN tareas.estados_tarea e ON e.id = t.estado_id
      WHERE e.codigo = 'resuelta' AND btrim(coalesce(t.solucion, '')) = '';`,
  },
  {
    id: "ciclo-y-null-heredado",
    hallazgos: ["A05"],
    clasificacion: "SQL",
    limite: "API no expone reparenting. NULL heredado es válido, no un hallazgo.",
    esperado: { ciclo: 2, nullHeredadoValido: true },
    sql: `INSERT INTO tareas.tareas
      (id, titulo, fecha_inicio, fecha_fin, prioridad_id, tipo_id, estado_id, responsable_id, creado_por_id, tarea_padre_id)
      SELECT ${inexistente}, 'Hija sintetica', fecha_inicio, fecha_fin, prioridad_id, tipo_id, estado_id,
        responsable_id, creado_por_id, id FROM tareas.tareas WHERE id = ${tarea};
      CREATE TEMP TABLE control AS SELECT t.proyecto_id IS NULL AND h.proyecto_id IS NULL AS valido
      FROM tareas.tareas t JOIN tareas.tareas h ON h.tarea_padre_id = t.id WHERE t.id = ${tarea};
      UPDATE tareas.tareas SET tarea_padre_id = ${inexistente} WHERE id = ${tarea};
      SELECT json_build_object('ciclo', (SELECT count(*) FROM tareas.tareas a JOIN tareas.tareas b
        ON a.tarea_padre_id = b.id AND b.tarea_padre_id = a.id),
        'nullHeredadoValido', (SELECT valido FROM control));`,
  },
  {
    id: "clave-parcial-permisos",
    hallazgos: ["A14"],
    clasificacion: "SQL",
    limite: "Historia legacy de grupo; no API de auditoría ni corrección de formato nuevo.",
    esperado: { relaciones: 2, eventos: 2, claves: 1, permisos: 2 },
    sql: `INSERT INTO identity.rol_permisos (rol_id, permiso_id)
      SELECT ${rol}, id FROM identity.permisos ORDER BY id LIMIT 2;
      SELECT json_build_object('relaciones', (SELECT count(*) FROM identity.rol_permisos WHERE rol_id = ${rol}),
        'eventos', count(*), 'claves', count(DISTINCT row_pk),
        'permisos', count(DISTINCT new_row->>'permiso_id')) FROM audit.change_log
      WHERE schema_name = 'identity' AND table_name = 'rol_permisos' AND row_pk = ${rol};`,
  },
  {
    id: "exclude-cambio-intradia",
    hallazgos: ["A04"],
    clasificacion: "SQL",
    limite: "Escrituras equivalentes al materializador; no circuito C# ni aprobación HTTP.",
    esperado: {
      sqlstate: "23P01",
      constraint: "designaciones_sin_solapamiento",
      vigentePreservada: true,
      auditoriaParcial: 0,
    },
    sql: `CREATE TEMP TABLE resultado (estado text, restriccion text);
      CREATE TEMP TABLE antes AS SELECT count(*) AS cantidad FROM audit.change_log;
      DO $$ DECLARE codigo text; restriccion text;
      BEGIN
        BEGIN
          UPDATE designaciones.designaciones SET vigente_hasta = DATE '2026-10-14' WHERE id = ${designacion};
          INSERT INTO designaciones.designaciones
            (persona_id, materia_id, carrera_id, cargo_id, dedicacion_id, horas, vigente_desde)
          SELECT persona_id, materia_id, carrera_id, cargo_id, dedicacion_id, 20, DATE '2026-10-13'
            FROM designaciones.designaciones WHERE id = ${designacion};
        EXCEPTION WHEN exclusion_violation THEN
          GET STACKED DIAGNOSTICS codigo = RETURNED_SQLSTATE, restriccion = CONSTRAINT_NAME;
          INSERT INTO resultado VALUES (codigo, restriccion);
        END;
      END $$;
      SELECT json_build_object('sqlstate', (SELECT estado FROM resultado),
        'constraint', (SELECT restriccion FROM resultado),
        'vigentePreservada', (SELECT vigente_hasta IS NULL AND horas = 10 FROM designaciones.designaciones WHERE id = ${designacion}),
        'auditoriaParcial', (SELECT count(*) - (SELECT cantidad FROM antes) FROM audit.change_log));`,
  },
  {
    id: "alta-incompleta-en-revision",
    hallazgos: ["A08"],
    clasificacion: "SQL",
    limite: "DDL admite cargo/horas NULL con dedicación válida; no envío/aprobación HTTP.",
    esperado: { incompletas: 1 },
    sql: `INSERT INTO designaciones.pedidos
      (numero, periodo_id, persona_id, materia_id, carrera_id, novedad, estado, dedicacion_solicitada_id)
      VALUES ('PROBE-ALTA', ${periodo}, ${persona}, ${materia}, ${carrera}, 'Alta',
        'en_revision_coordinador', ${catalogo("dedicaciones")});
      SELECT json_build_object('incompletas', count(*)) FROM designaciones.pedidos
      WHERE estado = 'en_revision_coordinador' AND cargo_solicitado_id IS NULL AND horas IS NULL;`,
  },
  {
    id: "clave-parcial-habilidad-interes",
    hallazgos: ["A14"],
    clasificacion: "SQL",
    limite: "Extensión sintética del probe de PK parcial a Portal; no consulta HTTP.",
    esperado: { relaciones: 2, eventos: 2, claves: 1, tipos: 2 },
    sql: `INSERT INTO portal.habilidades (id, termino, termino_norm) VALUES (${inexistente}, 'Probe', 'probe');
      INSERT INTO portal.docente_habilidades (perfil_id, habilidad_id, tipo)
      VALUES (${perfil}, ${inexistente}, 'habilidad'), (${perfil}, ${inexistente}, 'interes');
      SELECT json_build_object('relaciones', (SELECT count(*) FROM portal.docente_habilidades WHERE perfil_id = ${perfil}),
        'eventos', count(*), 'claves', count(DISTINCT row_pk), 'tipos', count(DISTINCT new_row->>'tipo'))
      FROM audit.change_log WHERE schema_name = 'portal' AND table_name = 'docente_habilidades' AND row_pk = ${perfil};`,
  },
  {
    id: "perfil-multicoleccion-sql-analogo",
    hallazgos: ["P01"],
    clasificacion: "SQL",
    limite:
      "JOIN SQL sintético equivalente en cardinalidad; NO SQL capturado de EF ni tiempo productivo.",
    esperado: { hijos: 15, combinaciones: 216 },
    sql: `INSERT INTO portal.experiencias (perfil_id, puesto, organizacion, desde)
      SELECT ${perfil}, 'Probe', 'Sintetica', '2026-01-01' FROM generate_series(1, 3);
      INSERT INTO portal.educaciones (perfil_id, nivel, carrera, institucion, desde)
      SELECT ${perfil}, 'Probe', 'Sintetica', 'Sintetica', '2026-01-01' FROM generate_series(1, 3);
      INSERT INTO portal.certificaciones (perfil_id, nombre, emisor, fecha)
      SELECT ${perfil}, 'Probe', 'Sintetico', '2026-01-01' FROM generate_series(1, 3);
      INSERT INTO portal.proyectos (perfil_id, nombre, rol, desde)
      SELECT ${perfil}, 'Probe', 'Sintetico', '2026-01-01' FROM generate_series(1, 2);
      INSERT INTO portal.habilidades (termino, termino_norm) SELECT 'Probe ' || g, 'probe-' || g FROM generate_series(1, 4) g;
      INSERT INTO portal.docente_habilidades (perfil_id, habilidad_id, tipo) SELECT ${perfil}, id, 'habilidad' FROM portal.habilidades;
      SELECT json_build_object('hijos', (SELECT count(*) FROM portal.experiencias) + (SELECT count(*) FROM portal.educaciones)
        + (SELECT count(*) FROM portal.certificaciones) + (SELECT count(*) FROM portal.proyectos)
        + (SELECT count(*) FROM portal.docente_habilidades), 'combinaciones', count(*))
      FROM portal.perfiles p LEFT JOIN portal.experiencias e ON e.perfil_id = p.id
      LEFT JOIN portal.educaciones d ON d.perfil_id = p.id LEFT JOIN portal.certificaciones c ON c.perfil_id = p.id
      LEFT JOIN portal.proyectos r ON r.perfil_id = p.id LEFT JOIN portal.docente_habilidades h ON h.perfil_id = p.id
      WHERE p.id = ${perfil};`,
  },
];
