-- 20261020000950_RevocacionEnSnapshotDeRolesIdentity (Identity).
--
-- La allowlist de snapshots de AuditoriaPrecisaIdentity nombra `revocado_at` para
-- identity.user_roles, una columna que no existe: la revocación de una asignación
-- es `deleted_at`. Sin ella el snapshot no dice si un UPDATE revocó o restituyó el
-- rol, y la auditoría deja de poder contarlo ("Rol X quitado a Y"). La fecha de
-- revocación es un valor técnico necesario, no un dato personal.
--
-- Mismo cuerpo que la versión publicada salvo ese nombre de columna. Los eventos
-- ya registrados no se reescriben.
CREATE OR REPLACE FUNCTION audit.safe_snapshot(p_schema text,p_table text,p_row jsonb) RETURNS jsonb
LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE WHEN p_row IS NULL THEN NULL ELSE COALESCE((
        SELECT jsonb_object_agg(key,value) FROM jsonb_each(p_row)
        WHERE key = ANY(CASE p_schema||'.'||p_table
            WHEN 'identity.users' THEN ARRAY['id','persona_id','is_active','created_at']
            WHEN 'identity.personas' THEN ARRAY['id','activo','estado','created_at']
            WHEN 'identity.roles' THEN ARRAY['id','code','scope','es_sistema','is_active','created_at']
            WHEN 'identity.permisos' THEN ARRAY['id','code','created_at']
            WHEN 'identity.rol_permisos' THEN ARRAY['rol_id','permiso_id','created_at']
            WHEN 'identity.user_roles' THEN ARRAY['id','user_id','role_id','materia_id','carrera_id','deleted_at','created_at']
            WHEN 'identity.materias' THEN ARRAY['id','codigo','activo','created_at']
            WHEN 'identity.carreras' THEN ARRAY['id','codigo','activo','created_at']
            WHEN 'identity.planes' THEN ARRAY['id','carrera_id','activo','created_at']
            WHEN 'identity.materias_plan' THEN ARRAY['id','materia_id','plan_id','created_at']
            WHEN 'designaciones.designaciones' THEN ARRAY['id','persona_id','materia_id','carrera_id','cargo_id','dedicacion_id','horas','horas_investigacion','horas_externas','vigente_desde','vigente_hasta','origen_pedido_id','created_at']
            WHEN 'designaciones.pedidos' THEN ARRAY['id','numero','periodo_id','persona_id','materia_id','carrera_id','novedad','estado','prioritario','cargo_solicitado_id','dedicacion_solicitada_id','horas','horas_investigacion','horas_externas','tipo_baja','etapa_retorno','propietario_actual','created_at']
            WHEN 'designaciones.periodos' THEN ARRAY['id','carga_desde','carga_hasta','impacto_desde','impacto_hasta','activo','created_at']
            WHEN 'designaciones.cargos' THEN ARRAY['id','codigo','orden','activo','created_at']
            WHEN 'designaciones.dedicaciones' THEN ARRAY['id','codigo','orden','activo','created_at']
            WHEN 'designaciones.pedido_adjuntos' THEN ARRAY['id','pedido_id','archivo_id','tipo','created_at']
            WHEN 'designaciones.pedido_historial' THEN ARRAY['id','pedido_id','accion','rol_id','actor_id','etapa','created_at']
            WHEN 'portal.perfiles' THEN ARRAY['id','persona_id','created_at']
            WHEN 'portal.contactos' THEN ARRAY['id','perfil_id','created_at']
            WHEN 'portal.cvs' THEN ARRAY['id','perfil_id','archivo_id','fecha_carga','created_at']
            WHEN 'portal.experiencias' THEN ARRAY['id','perfil_id','desde','hasta','created_at']
            WHEN 'portal.educaciones' THEN ARRAY['id','perfil_id','desde','hasta','created_at']
            WHEN 'portal.certificaciones' THEN ARRAY['id','perfil_id','fecha','vencimiento','created_at']
            WHEN 'portal.proyectos' THEN ARRAY['id','perfil_id','desde','hasta','created_at']
            WHEN 'portal.proyecto_documentos' THEN ARRAY['id','proyecto_id','archivo_id','fecha_carga','created_at']
            WHEN 'portal.habilidades' THEN ARRAY['id','canonica_id','sugerido','usos','created_at']
            WHEN 'portal.docente_habilidades' THEN ARRAY['perfil_id','habilidad_id','tipo','created_at']
            WHEN 'storage.archivos' THEN ARRAY['id','proposito','estado','propietario_id','tamano_bytes','expira_en','confirmado_at','revisado_at','eliminado_at','created_at']
            WHEN 'tareas.proyectos' THEN ARRAY['id','numero','fecha_inicio','fecha_fin','estado_id','responsable_id','created_at']
            WHEN 'tareas.tareas' THEN ARRAY['id','numero','fecha_inicio','fecha_fin','prioridad_id','tipo_id','estado_id','porcentaje_avance','responsable_id','creado_por_id','proyecto_id','tarea_padre_id','created_at']
            WHEN 'tareas.tarea_relaciones' THEN ARRAY['tarea_id','relacionada_id','created_at']
            WHEN 'tareas.tarea_comentarios' THEN ARRAY['id','tarea_id','autor_id','autor_rol','created_at']
            WHEN 'tareas.tarea_historial' THEN ARRAY['id','tarea_id','accion','actor_id','actor_rol','estado','estado_id','created_at']
            ELSE ARRAY[]::text[] END)
    ),'{}'::jsonb) END;
$$;
