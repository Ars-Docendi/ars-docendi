-- 001_asistente_grants.sql
--
-- Privilegios de LECTURA del asistente conversacional, enumerados columna por
-- columna. Fuente de verdad: database/asistente/manifiesto-privilegios.json.
-- Un test compara este resultado contra ese manifiesto en tres direcciones y
-- falla si divergen (ManifiestoPrivilegiosTests).
--
-- POR QUÉ ACÁ Y NO EN EL PROVISIONING
-- El alta de los roles vive en infra/scripts/provision-db.sh, que corre en el
-- paso 1 de spin-up.sh sobre una base VACÍA. Los GRANT tienen que correr con las
-- tablas ya creadas, o no otorgan nada. Un GRANT masivo por schema escrito en el
-- provisioning ni siquiera falla: otorga exactamente nada, el asistente arranca,
-- y PostgreSQL devuelve permission denied en cada consulta.
--
-- PROHIBIDO el GRANT masivo que abarca todas las tablas de un schema de una
-- vez, en cualquier forma. Es la sentencia que entregaría cada tabla nueva por
-- default y en silencio: alcanza con que alguien agregue una tabla con una
-- columna personal para que el asistente la lea sin que nadie lo haya decidido. La lista explícita obliga a
-- pasar por el manifiesto, y el test falla si aparece una tabla sin clasificar.
--
-- NOMBRES DE ROL POR AMBIENTE
-- Los roles llevan sufijo de ambiente (asistente_ro_prod, asistente_ro_pr_123),
-- así que no se pueden escribir literales acá. Llegan por GUC de transacción,
-- que fija el migrador antes de ejecutar este archivo, y se interpolan con
-- format(%I), que cita el identificador y no admite inyección.
--
-- Idempotente: CREATE EXTENSION lleva IF NOT EXISTS y un GRANT repetido es un
-- no-op. Re-ejecutar converge, que es lo que IMigradorModulo exige.

-- unaccent: normaliza tildes para que la búsqueda por nombre no dependa de cómo
-- se escribió. Es una extensión "trusted" desde PostgreSQL 13, así que la puede
-- instalar el dueño de la base sin ser superusuario.
CREATE EXTENSION IF NOT EXISTS unaccent;

DO $asistente_grants$
DECLARE
  rol_basico text := current_setting('app.asistente_rol_basico', true);
  rol_pii    text := current_setting('app.asistente_rol_pii', true);
BEGIN
  IF coalesce(rol_basico, '') = '' OR coalesce(rol_pii, '') = '' THEN
    RAISE EXCEPTION
      'Faltan los GUC app.asistente_rol_basico y app.asistente_rol_pii. Los fija el migrador del módulo antes de ejecutar este archivo; sin ellos no se sabe a qué roles conceder.';
  END IF;

  -- USAGE sobre los dos schemas expuestos. Sin esto, cada GRANT SELECT de abajo
  -- sería inalcanzable: el motor rechaza antes de llegar a la tabla.
  EXECUTE format('GRANT USAGE ON SCHEMA identity TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT USAGE ON SCHEMA designaciones TO %I, %I', rol_basico, rol_pii);

  -- El schema audit queda FUERA DE ALCANCE, entero. change_log.old_row y
  -- change_log.new_row guardan la fila completa en JSON: cualquier dato personal
  -- escrito en cualquier tabla auditada reaparece ahí, y un JSONB no admite
  -- GRANT por columna sobre su contenido. El REVOKE es redundante hoy —nunca se
  -- concedió USAGE— y está igual para que la denegación sea explícita y para que
  -- re-ejecutar converja si alguien la concedió a mano.
  EXECUTE format('REVOKE ALL ON SCHEMA audit FROM %I, %I', rol_basico, rol_pii);

  -- ------------------------------------------------------------------
  -- identity
  -- ------------------------------------------------------------------
  -- personas: documento, cuil, fecha_nacimiento y telefono van SOLO al rol con
  -- datos personales. Con el rol básico, un SELECT * sobre esta tabla falla con
  -- permission denied, que es el comportamiento buscado: la restricción la impone
  -- el motor, no el código.
  -- users.azure_oid no se concede a ninguno: identificador opaco del directorio
  -- externo, sin valor de consulta. users.upn es el correo institucional, o sea
  -- dato de contacto, y va con las personales.
  -- user_roles.granted_by no se concede: rastro de una acción administrativa
  -- sobre otra persona, no dato de dominio.
  EXECUTE format('GRANT SELECT (id, code, name, is_active, created_at) ON identity.carreras TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, code, name, is_active, created_at) ON identity.carreras TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, code, name, carrera_id, is_active, created_at) ON identity.materias TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, code, name, carrera_id, is_active, created_at) ON identity.materias TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, legajo, nombre, apellido, created_at) ON identity.personas TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, legajo, nombre, apellido, created_at, documento, cuil, fecha_nacimiento, telefono) ON identity.personas TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, display_name, is_active, created_at, last_login_at, persona_id) ON identity.users TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, display_name, is_active, created_at, last_login_at, persona_id, upn) ON identity.users TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, code, name, description, scope, es_sistema, is_active, created_at) ON identity.roles TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, code, name, description, scope, es_sistema, is_active, created_at) ON identity.roles TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, user_id, role_id, materia_id, carrera_id, granted_at, created_at, deleted_at) ON identity.user_roles TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, user_id, role_id, materia_id, carrera_id, granted_at, created_at, deleted_at) ON identity.user_roles TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, code, nombre, descripcion, created_at) ON identity.permisos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, code, nombre, descripcion, created_at) ON identity.permisos TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (rol_id, permiso_id, created_at) ON identity.rol_permisos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (rol_id, permiso_id, created_at) ON identity.rol_permisos TO %I', rol_pii);

  -- ------------------------------------------------------------------
  -- funciones de resolución del actor (migración identity/012)
  -- ------------------------------------------------------------------
  -- Las cuatro son SECURITY DEFINER y PUBLIC no las ejecuta. El GRANT va acá y no
  -- en la migración de identity porque los nombres de rol llevan sufijo de
  -- ambiente, y esta es la migración que los conoce.
  EXECUTE format('GRANT EXECUTE ON FUNCTION identity.asistente_actor() TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT EXECUTE ON FUNCTION identity.asistente_es_global() TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT EXECUTE ON FUNCTION identity.asistente_materias_visibles() TO %I, %I', rol_basico, rol_pii);
  EXECUTE format('GRANT EXECUTE ON FUNCTION identity.asistente_tiene_permiso(TEXT) TO %I, %I', rol_basico, rol_pii);

  -- ------------------------------------------------------------------
  -- designaciones
  -- ------------------------------------------------------------------
  -- pedidos.snapshot no se concede: JSONB de forma arbitraria que puede cambiar
  -- sin que nadie revise el manifiesto. Los mismos datos están en columnas propias.
  -- pedido_adjuntos.uri no se concede: es la ubicación del archivo. El asistente
  -- puede decir que existe un adjunto y de qué tipo; entregarlo es de la interfaz.
  -- designaciones.idempotencia_comandos no aparece: denegada entera, porque
  -- response_body guarda el cuerpo HTTP completo de cada comando.
  EXECUTE format('GRANT SELECT (id, codigo, nombre, abreviatura, orden, activo, created_at) ON designaciones.cargos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, codigo, nombre, abreviatura, orden, activo, created_at) ON designaciones.cargos TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta, activo, created_at) ON designaciones.periodos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, nombre, carga_desde, carga_hasta, impacto_desde, impacto_hasta, activo, created_at) ON designaciones.periodos TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, numero, periodo_id, persona_id, materia_id, novedad, estado, prioritario, cargo_solicitado_id, dedicacion_solicitada, horas, horas_investigacion, horas_externas, justificacion, tipo_baja, tipo_baja_detalle, etapa_retorno, propietario_actual, created_at) ON designaciones.pedidos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, numero, periodo_id, persona_id, materia_id, novedad, estado, prioritario, cargo_solicitado_id, dedicacion_solicitada, horas, horas_investigacion, horas_externas, justificacion, tipo_baja, tipo_baja_detalle, etapa_retorno, propietario_actual, created_at) ON designaciones.pedidos TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, pedido_id, tipo, nombre, created_at) ON designaciones.pedido_adjuntos TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, pedido_id, tipo, nombre, created_at) ON designaciones.pedido_adjuntos TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, pedido_id, accion, rol_id, actor_id, etapa, comentario, created_at) ON designaciones.pedido_historial TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, pedido_id, accion, rol_id, actor_id, etapa, comentario, created_at) ON designaciones.pedido_historial TO %I', rol_pii);
  EXECUTE format('GRANT SELECT (id, persona_id, materia_id, cargo_id, dedicacion, horas, vigente_desde, vigente_hasta, origen_pedido_id, created_at) ON designaciones.designaciones TO %I', rol_basico);
  EXECUTE format('GRANT SELECT (id, persona_id, materia_id, cargo_id, dedicacion, horas, vigente_desde, vigente_hasta, origen_pedido_id, created_at) ON designaciones.designaciones TO %I', rol_pii);
END
$asistente_grants$;
