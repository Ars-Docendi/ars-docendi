-- Metadata aditiva: nunca actualizar snapshots/eventos históricos.
ALTER TABLE audit.change_log
    ADD COLUMN row_key jsonb,
    ADD COLUMN row_pk_format text NOT NULL DEFAULT 'legacy',
    ADD CONSTRAINT change_log_key_format_valid CHECK (row_pk_format IN ('legacy','simple-v1','composite-v1'));

-- Las llamadas simples publicadas conservan su firma. La PK real prevalece
-- sobre el argumento parcial histórico al instalar el trigger.
CREATE FUNCTION audit.primary_key_columns(target_table regclass) RETURNS text[]
LANGUAGE sql STABLE AS $$
    SELECT array_agg(a.attname::text ORDER BY k.ordinality)
    FROM pg_index i CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY k(attnum, ordinality)
    JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum
    WHERE i.indrelid=target_table AND i.indisprimary AND k.ordinality<=i.indnkeyatts;
$$;

CREATE FUNCTION audit.attach(target_table regclass, pk_cols text[]) RETURNS void
LANGUAGE plpgsql AS $$
DECLARE short_name text; actual_cols text[] := audit.primary_key_columns(target_table);
BEGIN
    IF pk_cols IS DISTINCT FROM actual_cols OR cardinality(pk_cols)=0 THEN
        RAISE EXCEPTION 'El enganche de auditoría requiere la PK completa en orden de catálogo';
    END IF;
    SELECT relname INTO short_name FROM pg_class WHERE oid=target_table;
    EXECUTE format('DROP TRIGGER IF EXISTS %I ON %s','trg_'||short_name||'_audit_log',target_table);
    EXECUTE format('CREATE TRIGGER %I AFTER INSERT OR UPDATE OR DELETE ON %s FOR EACH ROW EXECUTE FUNCTION audit.log_change(%L)',
        'trg_'||short_name||'_audit_log', target_table, array_to_json(pk_cols)::text);
END;
$$;

CREATE OR REPLACE FUNCTION audit.attach(target_table regclass, pk_col text DEFAULT 'id') RETURNS void
LANGUAGE plpgsql AS $$
BEGIN
    IF NOT pk_col = ANY(audit.primary_key_columns(target_table)) THEN
        RAISE EXCEPTION 'Columna de clave de auditoría inválida';
    END IF;
    PERFORM audit.attach(target_table,audit.primary_key_columns(target_table));
END;
$$;

-- Allowlist explícita por tabla. Una columna o tabla nueva permanece oculta
-- hasta revisión; no se admiten reglas por sufijo ni snapshots documentales.
CREATE FUNCTION audit.safe_snapshot(p_schema text,p_table text,p_row jsonb) RETURNS jsonb
LANGUAGE sql IMMUTABLE AS $$
    SELECT CASE WHEN p_row IS NULL THEN NULL ELSE COALESCE((
        SELECT jsonb_object_agg(key,value) FROM jsonb_each(p_row)
        WHERE key = ANY(CASE p_schema||'.'||p_table
            WHEN 'identity.users' THEN ARRAY['id','persona_id','is_active','created_at']
            WHEN 'identity.personas' THEN ARRAY['id','activo','estado','created_at']
            WHEN 'identity.roles' THEN ARRAY['id','code','scope','es_sistema','is_active','created_at']
            WHEN 'identity.permisos' THEN ARRAY['id','code','created_at']
            WHEN 'identity.rol_permisos' THEN ARRAY['rol_id','permiso_id','created_at']
            WHEN 'identity.user_roles' THEN ARRAY['id','user_id','role_id','materia_id','carrera_id','revocado_at','created_at']
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

CREATE OR REPLACE FUNCTION audit.log_change() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    pk_cols text[];
    current_user_uuid uuid := NULLIF(current_setting('app.current_user_id',true),'')::uuid;
    request_id_val text := NULLIF(current_setting('app.request_id',true),'');
    new_jsonb jsonb;
    old_jsonb jsonb;
    source_row jsonb;
    full_key jsonb;
    pk_value text;
    changed_keys text[];
BEGIN
    -- También cubre triggers existentes con TG_ARGV parcial y nuevos módulos
    -- instalados después de Identity; no requiere su presencia en este upgrade.
    IF left(COALESCE(TG_ARGV[0],''),1)='[' THEN
        SELECT array_agg(value ORDER BY ordinality) INTO pk_cols
          FROM jsonb_array_elements_text(TG_ARGV[0]::jsonb) WITH ORDINALITY;
    ELSE
        pk_cols := audit.primary_key_columns(TG_RELID);
    END IF;
    IF cardinality(pk_cols) IS NULL OR cardinality(pk_cols)=0 THEN
        RAISE EXCEPTION 'Auditoría sin clave primaria completa';
    END IF;
    IF TG_OP <> 'DELETE' THEN new_jsonb := to_jsonb(NEW); END IF;
    IF TG_OP <> 'INSERT' THEN old_jsonb := to_jsonb(OLD); END IF;
    IF TG_OP='UPDATE' THEN
        changed_keys := ARRAY(SELECT key FROM jsonb_each(new_jsonb)
            WHERE new_jsonb->key IS DISTINCT FROM old_jsonb->key ORDER BY key);
        IF changed_keys='{}'::text[] THEN RETURN NEW; END IF;
    ELSE
        changed_keys := ARRAY(SELECT jsonb_object_keys(COALESCE(new_jsonb,old_jsonb)) ORDER BY 1);
    END IF;
    source_row := COALESCE(new_jsonb,old_jsonb);
    SELECT jsonb_object_agg(key,source_row->key) INTO full_key FROM unnest(pk_cols) key;
    pk_value := CASE WHEN cardinality(pk_cols)=1 THEN source_row->>pk_cols[1] ELSE full_key::text END;
    INSERT INTO audit.change_log(schema_name,table_name,row_pk,row_key,row_pk_format,action,
        old_row,new_row,changed_columns,changed_by,request_id)
    VALUES(TG_TABLE_SCHEMA,TG_TABLE_NAME,pk_value,full_key,
        CASE WHEN cardinality(pk_cols)=1 THEN 'simple-v1' ELSE 'composite-v1' END,TG_OP,
        audit.safe_snapshot(TG_TABLE_SCHEMA,TG_TABLE_NAME,old_jsonb),
        audit.safe_snapshot(TG_TABLE_SCHEMA,TG_TABLE_NAME,new_jsonb),changed_keys,current_user_uuid,request_id_val);
    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;

-- Fecha/actor se proyectan juntos desde un solo evento seleccionado.
CREATE OR REPLACE FUNCTION audit.row_history(p_schema text,p_table text,p_pk text)
RETURNS TABLE(created_at timestamptz,created_by uuid,updated_at timestamptz,updated_by uuid,deleted_at timestamptz,deleted_by uuid)
LANGUAGE sql STABLE AS $$
    WITH events AS MATERIALIZED (
        SELECT id,action,changed_at,changed_by FROM audit.change_log
        WHERE schema_name=p_schema AND table_name=p_table AND row_pk=p_pk
    ), creation AS (SELECT * FROM events WHERE action='INSERT' ORDER BY changed_at,id LIMIT 1),
    last_event AS (SELECT * FROM events ORDER BY changed_at DESC,id DESC LIMIT 1),
    deletion AS (SELECT * FROM events WHERE action='DELETE' ORDER BY changed_at DESC,id DESC LIMIT 1)
    SELECT c.changed_at,c.changed_by,u.changed_at,u.changed_by,d.changed_at,d.changed_by
    FROM (VALUES(1)) anchor(n) LEFT JOIN creation c ON true LEFT JOIN last_event u ON true LEFT JOIN deletion d ON true;
$$;

-- Recuperación de legado solo en lectura; faltantes y cambio de PK son ambiguos.
CREATE FUNCTION audit.resolved_key(p_schema text,p_table text,p_old jsonb,p_new jsonb,p_stored jsonb) RETURNS jsonb
LANGUAGE plpgsql IMMUTABLE AS $$
DECLARE cols text[]; old_key jsonb; new_key jsonb; col text;
BEGIN
    IF p_stored IS NOT NULL THEN RETURN p_stored; END IF;
    cols := CASE p_schema||'.'||p_table
        WHEN 'identity.rol_permisos' THEN ARRAY['rol_id','permiso_id']
        WHEN 'portal.docente_habilidades' THEN ARRAY['perfil_id','habilidad_id','tipo']
        WHEN 'tareas.tarea_relaciones' THEN ARRAY['tarea_id','relacionada_id']
        ELSE NULL END;
    IF cols IS NULL OR (p_old IS NULL AND p_new IS NULL) THEN RETURN NULL; END IF;
    FOREACH col IN ARRAY cols LOOP
        IF (p_old IS NOT NULL AND (NOT p_old ? col OR p_old->col='null'::jsonb))
           OR (p_new IS NOT NULL AND (NOT p_new ? col OR p_new->col='null'::jsonb)) THEN RETURN NULL; END IF;
    END LOOP;
    IF p_old IS NOT NULL THEN SELECT jsonb_object_agg(key,p_old->key) INTO old_key FROM unnest(cols) key; END IF;
    IF p_new IS NOT NULL THEN SELECT jsonb_object_agg(key,p_new->key) INTO new_key FROM unnest(cols) key; END IF;
    IF old_key IS NOT NULL AND new_key IS NOT NULL AND old_key<>new_key THEN RETURN NULL; END IF;
    RETURN COALESCE(new_key,old_key);
END;
$$;

-- Superficie de historia exacta: nunca incluye agrupaciones legadas ambiguas.
CREATE FUNCTION audit.row_history_precise(p_schema text,p_table text,p_key jsonb)
RETURNS TABLE(created_at timestamptz,created_by uuid,updated_at timestamptz,updated_by uuid,deleted_at timestamptz,deleted_by uuid,key_format text)
LANGUAGE sql STABLE AS $$
    WITH events AS MATERIALIZED (
        SELECT id,action,changed_at,changed_by FROM audit.change_log
        WHERE schema_name=p_schema AND table_name=p_table
          AND audit.resolved_key(schema_name,table_name,old_row,new_row,row_key)=p_key
    ), creation AS (SELECT * FROM events WHERE action='INSERT' ORDER BY changed_at,id LIMIT 1),
    last_event AS (SELECT * FROM events ORDER BY changed_at DESC,id DESC LIMIT 1),
    deletion AS (SELECT * FROM events WHERE action='DELETE' ORDER BY changed_at DESC,id DESC LIMIT 1)
    SELECT c.changed_at,c.changed_by,u.changed_at,u.changed_by,d.changed_at,d.changed_by,'precise'::text
    FROM (VALUES(1)) anchor(n) LEFT JOIN creation c ON true LEFT JOIN last_event u ON true LEFT JOIN deletion d ON true;
$$;

CREATE FUNCTION audit.row_history_group(p_schema text,p_table text,p_pk text)
RETURNS TABLE(created_at timestamptz,created_by uuid,updated_at timestamptz,updated_by uuid,deleted_at timestamptz,deleted_by uuid,key_format text)
LANGUAGE sql STABLE AS $$
    SELECT h.*,'legacy-group'::text FROM audit.row_history(p_schema,p_table,p_pk) h;
$$;

-- Justificado por planes de página/COUNT de rango sobre 200.000 eventos.
-- BRIN e índices por fila/actor se conservan; COUNT total sigue recorriendo filas.
CREATE INDEX change_log_recientes_idx ON audit.change_log(changed_at DESC,id DESC);

SELECT audit.attach('identity.rol_permisos',ARRAY['rol_id','permiso_id']);
DO $$ BEGIN
    IF to_regclass('portal.docente_habilidades') IS NOT NULL THEN
        PERFORM audit.attach('portal.docente_habilidades',ARRAY['perfil_id','habilidad_id','tipo']);
    END IF;
END $$;
