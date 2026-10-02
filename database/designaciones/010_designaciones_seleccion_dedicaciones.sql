-- Las columnas textuales sólo conservan historia: una selección nueva usa FK.
CREATE FUNCTION designaciones.validar_dedicacion_vigente() RETURNS trigger
LANGUAGE plpgsql AS $$
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

CREATE TRIGGER validar_dedicacion_vigente BEFORE INSERT OR UPDATE
ON designaciones.designaciones FOR EACH ROW EXECUTE FUNCTION designaciones.validar_dedicacion_vigente();

CREATE FUNCTION designaciones.validar_dedicacion_pedido() RETURNS trigger
LANGUAGE plpgsql AS $$
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

CREATE TRIGGER validar_dedicacion_pedido BEFORE INSERT OR UPDATE
ON designaciones.pedidos FOR EACH ROW EXECUTE FUNCTION designaciones.validar_dedicacion_pedido();
