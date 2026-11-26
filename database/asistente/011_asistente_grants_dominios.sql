-- 011_asistente_grants_dominios.sql
--
-- Privilegios de LECTURA del asistente sobre los dominios que se sumaron después de
-- 001: comisiones, aulas y portal.proyectos. Mismo contrato que 001: columna por
-- columna, fuente de verdad en database/asistente/manifiesto-privilegios.json, que
-- ManifiestoPrivilegiosTests compara contra la base efectiva en tres direcciones.
--
-- POR QUÉ ES UN ARCHIVO APARTE
-- 001_asistente_grants.sql está en database/migraciones-protegidas.json: su hash
-- quedó congelado cuando se publicó y la validación de integridad de migraciones
-- (scripts/migrations/validate.mjs) rechaza cualquier edición posterior. Todo
-- privilegio nuevo entra por un archivo numerado nuevo, igual que 009 y 010. El
-- orden convergente es 001 -> 011: MigradorAsistente aplica 001 (GUC de roles y los
-- GRANT originales) y después este archivo, en la misma transacción y con los mismos
-- GUC. 001 no revoca nada sobre estos schemas, así que no hay nada que re-conceder.
--
-- CORRE DESPUÉS DEL RLS. El módulo asistente se aplica después de Designaciones,
-- Aulas y Portal, que son quienes habilitan RLS y crean las policies sobre estas
-- tablas (RlsAsistenteComisiones, RlsAsistenteSolicitudes, RlsAsistenteProyectosPortal).
-- Conceder antes abriría el snapshot entero en la ventana entre los dos merges; por
-- eso el orden es parte de la frontera y no un detalle.
--
-- Idempotente: un GRANT repetido es un no-op. Re-ejecutar converge, que es lo que
-- IMigradorModulo exige.

DO $asistente_grants_dominios$
DECLARE
  rol_basico text := current_setting('app.asistente_rol_basico', true);
  rol_pii    text := current_setting('app.asistente_rol_pii', true);
BEGIN
  IF coalesce(rol_basico, '') = '' OR coalesce(rol_pii, '') = '' THEN
    RAISE EXCEPTION
      'Faltan los GUC app.asistente_rol_basico y app.asistente_rol_pii. Los fija el migrador del módulo antes de ejecutar este archivo; sin ellos no se sabe a qué roles conceder.';
  END IF;

  -- USAGE sobre los schemas nuevos. Sin esto, cada GRANT SELECT de abajo sería
  -- inalcanzable: el motor rechaza antes de llegar a la tabla.
  EXECUTE format('GRANT USAGE ON SCHEMA comisiones TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT USAGE ON SCHEMA aulas TO %I, %I', rol_basico, rol_pii);

  -- ------------------------------------------------------------------
  -- portal
  -- ------------------------------------------------------------------
  -- `proyectos` se suma a las seis tablas del catálogo de preguntas (2026-11-07: el
  -- jefe de cátedra que busca quién puede dictar una materia pide los antecedentes de
  -- investigación). Se concede SIN `descripcion`: es texto libre autodeclarado, el
  -- mismo problema de enmascarado que `experiencias.descripcion`. `cvs` y
  -- `proyecto_documentos` siguen afuera porque ninguna pregunta las pide.
  --
  -- CORRECCIÓN A 001: su comentario del bloque portal dice que
  -- `portal.ver_trayectoria_ajena` está «concedido A NADIE». Ya no es cierto (001 no se
  -- puede editar; vale lo que sigue).
  --
  -- CONCEDER ESTO NO ABRE EL PADRÓN. El predicado de RLS es «mi propio perfil OR
  -- (tengo portal.ver_trayectoria_ajena Y el ámbito alcanza a la persona)». El
  -- permiso lo tiene únicamente el rol `jefe_catedra` (decisión del equipo,
  -- 2026-11-07; migración PermisoPortalTrayectoriaAjenaJefeCatedraIdentity), y su
  -- ámbito son los docentes con designación vigente en sus materias. Cualquier otro
  -- rol sólo ve su propio perfil hasta que alguien le conceda el permiso desde
  -- /roles, que es una acción de administración y no un despliegue. Su policy la crea
  -- RlsAsistenteProyectosPortal.
  --
  -- Las columnas de portal NO concedidas (incluida `proyectos.descripcion`) y el
  -- motivo van en manifiesto-privilegios.json.
  EXECUTE format('GRANT SELECT (id, perfil_id, nombre, rol, desde, hasta, doi, created_at) ON portal.proyectos TO %I, %I', rol_basico, rol_pii);

  -- ------------------------------------------------------------------
  -- comisiones
  -- ------------------------------------------------------------------
  -- CUATRO DE LAS CINCO TABLAS, derivadas de las preguntas (qué comisiones tiene un
  -- docente, quién dicta la comisión 02, en qué días, horarios y aulas, cuántas
  -- carga cada uno). `sga_importaciones` NO se concede: es el registro operativo de
  -- cada importación y nombra a quien la hizo (`usuario_id`); ninguna pregunta del
  -- catálogo lo necesita. El motor lo rechaza por falta de privilegio, no devuelve
  -- cero filas.
  --
  -- LOS DOS ROLES RECIBEN LO MISMO. Ninguna columna concedida es personal:
  -- `sga_comision_docentes.nombre` va al rol básico igual que identity.personas.nombre,
  -- porque `persona_id` es nullable (docente de Guaraní sin persona vinculada) y sin el
  -- nombre ese docente desaparecería de las respuestas. El documento nunca se guarda.
  --
  -- Columnas NO concedidas (el motivo va en manifiesto-privilegios.json):
  -- sga_comisiones.importacion_id (FK hacia el registro denegado),
  -- periodo_lectivo_guarani_id (clave opaca), modalidad_codigo (código de Guaraní) y
  -- created_at (mecánica de importación); sga_comision_horarios.asignacion_guarani_id.
  EXECUTE format('GRANT SELECT (comision_guarani_id, anio_academico, periodo_lectivo_nombre, nombre, materia_codigo, materia_nombre, materia_id, modalidad, ubicacion) ON comisiones.sga_comisiones TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT SELECT (comision_guarani_id, carrera_codigo, carrera_nombre, carrera_id) ON comisiones.sga_comision_carreras TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT SELECT (comision_guarani_id, orden, nombre, responsabilidad, persona_id) ON comisiones.sga_comision_docentes TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT SELECT (comision_guarani_id, dia, desde, hasta, aula) ON comisiones.sga_comision_horarios TO %I, %I', rol_basico, rol_pii);

  -- ------------------------------------------------------------------
  -- aulas
  -- ------------------------------------------------------------------
  -- UNA SOLA TABLA, la única que tiene el schema: las solicitudes de reserva de aula.
  -- Se conceden TODAS las columnas a los dos roles: ninguna es personal (quién pidió se
  -- expresa con docente_id, que se une con identity.personas bajo el GRANT de esa
  -- tabla). motivo_rechazo y motivo_cancelacion son texto libre del trámite y se
  -- clasifican `sensible-texto` en el manifiesto de sensibilidad: el usuario las ve en
  -- la tabla de resultados y no viajan al modelo.
  --
  -- El predicado de RLS (RlsAsistenteSolicitudes) espeja la pantalla: aulas.aprobar
  -- ve todas, aulas.solicitar sólo las propias.
  EXECUTE format('GRANT SELECT (id, docente_id, dia, horario_desde, horario_hasta, cantidad_alumnos_aprox, materia_id, comision, estado, aula_asignada, motivo_rechazo, created_at, motivo_cancelacion, rol_solicitante) ON aulas.solicitudes_reserva TO %I, %I', rol_basico, rol_pii);
END
$asistente_grants_dominios$;
