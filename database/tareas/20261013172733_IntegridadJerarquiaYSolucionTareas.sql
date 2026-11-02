-- Incremental: falla cerrado ante legado incompatible, sin saneamiento automático.
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM tareas.tareas t JOIN tareas.estados_tarea e ON e.id=t.estado_id
        WHERE e.codigo='resuelta' AND nullif(btrim(t.solucion),'') IS NULL) THEN
        RAISE EXCEPTION 'tareas-resuelta-sin-solucion' USING ERRCODE='23514';
    END IF;
END $$;

CREATE FUNCTION tareas.validar_solucion() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE codigo_estado text;
BEGIN
    SELECT codigo INTO codigo_estado FROM tareas.estados_tarea WHERE id=NEW.estado_id FOR SHARE;
    IF codigo_estado='resuelta' AND nullif(btrim(NEW.solucion),'') IS NULL THEN
        RAISE EXCEPTION 'tareas-resuelta-sin-solucion' USING ERRCODE='23514', CONSTRAINT='tareas_resuelta_con_solucion';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER tareas_validar_solucion BEFORE INSERT OR UPDATE OF estado_id,solucion ON tareas.tareas
    FOR EACH ROW EXECUTE FUNCTION tareas.validar_solucion();

CREATE FUNCTION tareas.proteger_codigo_estado() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.codigo IS DISTINCT FROM OLD.codigo AND EXISTS (SELECT 1 FROM tareas.tareas WHERE estado_id=OLD.id) THEN
        RAISE EXCEPTION 'tareas-catalogo-referenciado' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER estados_tarea_proteger_codigo BEFORE UPDATE OF codigo ON tareas.estados_tarea
    FOR EACH ROW EXECUTE FUNCTION tareas.proteger_codigo_estado();

-- Validación de legado antes de activar restricciones, sin reparenting ni backfill.
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM tareas.tareas t JOIN tareas.tareas p ON p.id=t.tarea_padre_id
               WHERE t.proyecto_id IS DISTINCT FROM p.proyecto_id) THEN
        RAISE EXCEPTION 'tareas-proyecto-incoherente' USING ERRCODE='23514';
    END IF;
    IF EXISTS (
        WITH RECURSIVE cadena AS (
            SELECT id,tarea_padre_id,ARRAY[id] camino,false ciclo FROM tareas.tareas
            UNION ALL
            SELECT t.id,t.tarea_padre_id,c.camino||t.id,t.id=ANY(c.camino)
            FROM cadena c JOIN tareas.tareas t ON t.id=c.tarea_padre_id WHERE NOT c.ciclo
        ) SELECT 1 FROM cadena WHERE ciclo
    ) THEN RAISE EXCEPTION 'tareas-ciclo' USING ERRCODE='23514'; END IF;
END $$;

-- Mismo lock por raíz utilizado por la aplicación. En un traslado directo se
-- protegen los componentes de origen y destino en orden UUID. Una colisión que
-- cambia los componentes mientras se espera se revalida al commit; un deadlock
-- aborta uno de los escritores, nunca permite confirmar un ciclo.
CREATE FUNCTION tareas.bloquear_jerarquia() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE raiz uuid;
BEGIN
    FOR raiz IN
        WITH RECURSIVE ancestros AS (
            SELECT id,tarea_padre_id FROM tareas.tareas WHERE id=NEW.id OR id=NEW.tarea_padre_id
            UNION
            SELECT t.id,t.tarea_padre_id FROM tareas.tareas t JOIN ancestros a ON t.id=a.tarea_padre_id
        ) SELECT id FROM ancestros WHERE tarea_padre_id IS NULL ORDER BY id
    LOOP
        PERFORM pg_advisory_xact_lock(hashtextextended(raiz::text,52005));
    END LOOP;
    RETURN NEW;
END $$;
CREATE TRIGGER tareas_bloquear_jerarquia BEFORE INSERT OR UPDATE OF tarea_padre_id,proyecto_id ON tareas.tareas
    FOR EACH ROW EXECUTE FUNCTION tareas.bloquear_jerarquia();

CREATE FUNCTION tareas.validar_jerarquia() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    -- Releer la fila final: puede haber varios updates válidos en la transacción.
    IF EXISTS (SELECT 1 FROM tareas.tareas t JOIN tareas.tareas p ON p.id=t.tarea_padre_id
        WHERE (t.id=NEW.id OR p.id=NEW.id) AND t.proyecto_id IS DISTINCT FROM p.proyecto_id) THEN
        RAISE EXCEPTION 'tareas-proyecto-incoherente' USING ERRCODE='23514', CONSTRAINT='tareas_proyecto_heredado';
    END IF;
    IF EXISTS (
        WITH RECURSIVE cadena AS (
            SELECT id,tarea_padre_id,ARRAY[id] camino,false ciclo FROM tareas.tareas WHERE id=NEW.id
            UNION ALL
            SELECT t.id,t.tarea_padre_id,c.camino||t.id,t.id=ANY(c.camino)
            FROM cadena c JOIN tareas.tareas t ON t.id=c.tarea_padre_id WHERE NOT c.ciclo
        ) SELECT 1 FROM cadena WHERE ciclo
    ) THEN RAISE EXCEPTION 'tareas-ciclo' USING ERRCODE='23514', CONSTRAINT='tareas_jerarquia_aciclica'; END IF;
    RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER tareas_validar_jerarquia AFTER INSERT OR UPDATE ON tareas.tareas
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION tareas.validar_jerarquia();
