-- Reservas recuperables. Las claves disponibles anteriores se preservan byte por byte.
ALTER TABLE storage.archivos
    ADD COLUMN intento_id uuid,
    ADD COLUMN reserva_id uuid,
    ADD COLUMN reserva_hasta timestamptz,
    ADD COLUMN fase_carga text;
ALTER TABLE storage.archivos ADD CONSTRAINT archivos_fase_carga_valida
    CHECK (fase_carga IS NULL OR fase_carga IN ('subiendo', 'cargado', 'revisando', 'eliminando'));

-- Una publicación conserva exactamente su identidad de objeto y validación.
CREATE FUNCTION storage.proteger_publicacion() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.estado = 'disponible' AND (
        NEW.clave_objeto IS DISTINCT FROM OLD.clave_objeto OR
        NEW.intento_id IS DISTINCT FROM OLD.intento_id OR
        NEW.bucket IS DISTINCT FROM OLD.bucket OR
        NEW.ambiente IS DISTINCT FROM OLD.ambiente OR
        NEW.sha256 IS DISTINCT FROM OLD.sha256 OR
        NEW.mime_detectado IS DISTINCT FROM OLD.mime_detectado OR
        NEW.tamano_bytes IS DISTINCT FROM OLD.tamano_bytes) THEN
        RAISE EXCEPTION 'La version publicada es inmutable' USING ERRCODE = '23514';
    END IF;
    IF OLD.fase_carga = 'eliminando' AND NEW.estado NOT IN ('cuarentena', 'eliminado') THEN
        RAISE EXCEPTION 'Una reserva de eliminacion no puede republicarse' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER archivos_publicacion_inmutable BEFORE UPDATE ON storage.archivos
    FOR EACH ROW EXECUTE FUNCTION storage.proteger_publicacion();
