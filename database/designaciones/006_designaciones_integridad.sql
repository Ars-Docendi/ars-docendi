-- Baseline consolidado: designaciones/006_designaciones_integridad.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE SEQUENCE designaciones.pedidos_numero_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE FUNCTION designaciones.siguiente_numero_pedido() RETURNS text
    LANGUAGE sql
    AS $$
    SELECT to_char(now(), 'YYYY') || '-' ||
           lpad(nextval('designaciones.pedidos_numero_seq')::TEXT, 4, '0');
$$;

CREATE FUNCTION designaciones.validar_dedicacion_pedido() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.dedicacion_solicitada_id IS NOT DISTINCT FROM OLD.dedicacion_solicitada_id
       AND NEW.dedicacion_solicitada IS NOT DISTINCT FROM OLD.dedicacion_solicitada
       AND NEW.novedad = OLD.novedad THEN
        RETURN NEW;
    END IF;
    IF NEW.dedicacion_solicitada_id IS NULL AND NEW.novedad IN ('Alta', 'Cambio de cargo o dedicación') THEN
        RAISE EXCEPTION 'La solicitud requiere una dedicación del catálogo' USING ERRCODE = '23514';
    END IF;
    IF NEW.dedicacion_solicitada_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT FROM designaciones.dedicaciones WHERE id = NEW.dedicacion_solicitada_id) THEN
            RAISE EXCEPTION 'La dedicación no existe' USING ERRCODE = '23503';
        END IF;
        IF NOT EXISTS (SELECT FROM designaciones.dedicaciones WHERE id = NEW.dedicacion_solicitada_id AND activo) THEN
            RAISE EXCEPTION 'La dedicación está inactiva' USING ERRCODE = '23514';
        END IF;
    END IF;
    IF NEW.dedicacion_solicitada IS NOT NULL THEN
        RAISE EXCEPTION 'No se admite una nueva dedicación textual' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION designaciones.validar_dedicacion_vigente() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.dedicacion_id IS NOT DISTINCT FROM OLD.dedicacion_id
       AND NEW.dedicacion IS NOT DISTINCT FROM OLD.dedicacion THEN
        RETURN NEW;
    END IF;
    IF NEW.dedicacion_id IS NULL THEN
        RAISE EXCEPTION 'La designación requiere una dedicación del catálogo'
            USING ERRCODE = '23514';
    END IF;
    IF NOT EXISTS (SELECT FROM designaciones.dedicaciones WHERE id = NEW.dedicacion_id) THEN
        RAISE EXCEPTION 'La dedicación no existe' USING ERRCODE = '23503';
    END IF;
    IF NOT EXISTS (SELECT FROM designaciones.dedicaciones WHERE id = NEW.dedicacion_id AND activo) THEN
        RAISE EXCEPTION 'La dedicación está inactiva' USING ERRCODE = '23514';
    END IF;
    IF NEW.dedicacion IS NOT NULL THEN
        RAISE EXCEPTION 'No se admite una nueva dedicación textual' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER validar_dedicacion_pedido BEFORE INSERT OR UPDATE ON designaciones.pedidos FOR EACH ROW EXECUTE FUNCTION designaciones.validar_dedicacion_pedido();

CREATE TRIGGER validar_dedicacion_vigente BEFORE INSERT OR UPDATE ON designaciones.designaciones FOR EACH ROW EXECUTE FUNCTION designaciones.validar_dedicacion_vigente();

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_cargo_id_fkey FOREIGN KEY (cargo_id) REFERENCES designaciones.cargos(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_carrera_id_fkey FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_dedicacion_id_fkey FOREIGN KEY (dedicacion_id) REFERENCES designaciones.dedicaciones(id);

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_origen_pedido_id_fkey FOREIGN KEY (origen_pedido_id) REFERENCES designaciones.pedidos(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_persona_id_fkey FOREIGN KEY (persona_id) REFERENCES identity.personas(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.idempotencia_comandos
    ADD CONSTRAINT idempotencia_comandos_actor_id_fkey FOREIGN KEY (actor_id) REFERENCES identity.users(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.idempotencia_comandos
    ADD CONSTRAINT idempotencia_comandos_pedido_id_fkey FOREIGN KEY (pedido_id) REFERENCES designaciones.pedidos(id) ON DELETE CASCADE;

ALTER TABLE ONLY designaciones.pedido_adjuntos
    ADD CONSTRAINT pedido_adjuntos_pedido_id_fkey FOREIGN KEY (pedido_id) REFERENCES designaciones.pedidos(id) ON DELETE CASCADE;

ALTER TABLE ONLY designaciones.pedido_historial
    ADD CONSTRAINT pedido_historial_actor_id_fkey FOREIGN KEY (actor_id) REFERENCES identity.users(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedido_historial
    ADD CONSTRAINT pedido_historial_pedido_id_fkey FOREIGN KEY (pedido_id) REFERENCES designaciones.pedidos(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedido_historial
    ADD CONSTRAINT pedido_historial_rol_id_fkey FOREIGN KEY (rol_id) REFERENCES identity.roles(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_cargo_solicitado_id_fkey FOREIGN KEY (cargo_solicitado_id) REFERENCES designaciones.cargos(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_carrera_id_fkey FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_dedicacion_solicitada_id_fkey FOREIGN KEY (dedicacion_solicitada_id) REFERENCES designaciones.dedicaciones(id);

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_materia_id_fkey FOREIGN KEY (materia_id) REFERENCES identity.materias(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_periodo_id_fkey FOREIGN KEY (periodo_id) REFERENCES designaciones.periodos(id) ON DELETE RESTRICT;

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_persona_id_fkey FOREIGN KEY (persona_id) REFERENCES identity.personas(id) ON DELETE RESTRICT;
