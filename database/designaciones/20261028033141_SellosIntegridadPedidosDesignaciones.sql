-- 20261028033141_SellosIntegridadPedidosDesignaciones (Designaciones).
-- No editar después de publicar: crear otra migración incremental.
-- Cadena de sellos por período [BR-designaciones-019]. La base encadena y protege;
-- el hash y la firma los calcula la aplicación, que es la única que tiene la clave.

CREATE TABLE designaciones.pedido_sellos (
    periodo_id uuid NOT NULL,
    secuencia bigint NOT NULL,
    historial_id uuid NOT NULL,
    pedido_id uuid NOT NULL,
    hash_anterior bytea NOT NULL,
    hash_estado bytea NOT NULL,
    hash bytea NOT NULL,
    firma bytea NOT NULL,
    version_clave smallint NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT pedido_sellos_pkey PRIMARY KEY (periodo_id, secuencia),
    CONSTRAINT pedido_sellos_historial_unico UNIQUE (historial_id),
    CONSTRAINT pedido_sellos_secuencia_positiva CHECK (secuencia > 0),
    CONSTRAINT pedido_sellos_longitudes CHECK (
        length(hash_anterior) = 32 AND length(hash_estado) = 32
        AND length(hash) = 32 AND length(firma) = 32),
    CONSTRAINT pedido_sellos_periodo_id_fkey FOREIGN KEY (periodo_id)
        REFERENCES designaciones.periodos(id) ON DELETE RESTRICT,
    CONSTRAINT pedido_sellos_historial_id_fkey FOREIGN KEY (historial_id)
        REFERENCES designaciones.pedido_historial(id) ON DELETE RESTRICT,
    CONSTRAINT pedido_sellos_pedido_id_fkey FOREIGN KEY (pedido_id)
        REFERENCES designaciones.pedidos(id) ON DELETE RESTRICT
);

CREATE INDEX pedido_sellos_pedido_idx ON designaciones.pedido_sellos (pedido_id, periodo_id, secuencia);

CREATE TABLE designaciones.periodo_anclas (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    periodo_id uuid NOT NULL,
    secuencia bigint NOT NULL,
    hash bytea NOT NULL,
    motivo text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT periodo_anclas_pkey PRIMARY KEY (id),
    CONSTRAINT periodo_anclas_motivo_valido CHECK (motivo IN ('exportacion_lote', 'retroactivo')),
    CONSTRAINT periodo_anclas_hash_longitud CHECK (length(hash) = 32),
    CONSTRAINT periodo_anclas_sello_fkey FOREIGN KEY (periodo_id, secuencia)
        REFERENCES designaciones.pedido_sellos(periodo_id, secuencia) ON DELETE RESTRICT
);

CREATE INDEX periodo_anclas_periodo_idx ON designaciones.periodo_anclas (periodo_id, secuencia);

-- Mismo lock que toma la aplicación antes de leer la cabeza: serializa la cadena del período.
CREATE FUNCTION designaciones.bloquear_cadena_periodo(p_periodo uuid) RETURNS void
LANGUAGE sql AS $$
    SELECT pg_advisory_xact_lock(hashtextextended('designaciones.pedido_sellos:' || p_periodo::text, 0));
$$;

CREATE FUNCTION designaciones.encadenar_sello() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    secuencia_cabeza bigint := 0;
    hash_cabeza bytea := sha256(convert_to(NEW.periodo_id::text, 'UTF8'));
BEGIN
    PERFORM designaciones.bloquear_cadena_periodo(NEW.periodo_id);
    SELECT s.secuencia, s.hash INTO secuencia_cabeza, hash_cabeza
      FROM designaciones.pedido_sellos s
     WHERE s.periodo_id = NEW.periodo_id
     ORDER BY s.secuencia DESC
     LIMIT 1;
    IF NOT FOUND THEN
        secuencia_cabeza := 0;
        hash_cabeza := sha256(convert_to(NEW.periodo_id::text, 'UTF8'));
    END IF;
    IF NEW.secuencia <> secuencia_cabeza + 1 OR NEW.hash_anterior <> hash_cabeza THEN
        RAISE EXCEPTION 'El sello no encadena con la cabeza del período' USING ERRCODE = '23514';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM designaciones.pedido_historial h
          JOIN designaciones.pedidos p ON p.id = h.pedido_id
         WHERE h.id = NEW.historial_id AND p.id = NEW.pedido_id AND p.periodo_id = NEW.periodo_id) THEN
        RAISE EXCEPTION 'El sello no corresponde a un evento del pedido en el período' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_pedido_sellos_encadenar
    BEFORE INSERT ON designaciones.pedido_sellos
    FOR EACH ROW EXECUTE FUNCTION designaciones.encadenar_sello();

CREATE FUNCTION designaciones.rechazar_modificacion() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION '%.% es de sólo inserción', TG_TABLE_SCHEMA, TG_TABLE_NAME USING ERRCODE = '42501';
END;
$$;

CREATE TRIGGER trg_pedido_sellos_inmutable
    BEFORE UPDATE OR DELETE ON designaciones.pedido_sellos
    FOR EACH ROW EXECUTE FUNCTION designaciones.rechazar_modificacion();

CREATE TRIGGER trg_periodo_anclas_inmutable
    BEFORE UPDATE OR DELETE ON designaciones.periodo_anclas
    FOR EACH ROW EXECUTE FUNCTION designaciones.rechazar_modificacion();

-- Un evento del circuito no se modifica; reescribir los mismos valores (upsert
-- idempotente del seed) no es una modificación.
CREATE TRIGGER trg_pedido_historial_inmutable
    BEFORE UPDATE ON designaciones.pedido_historial
    FOR EACH ROW
    WHEN (OLD.etapa <> 'borrador' AND OLD.* IS DISTINCT FROM NEW.*)
    EXECUTE FUNCTION designaciones.rechazar_modificacion();

-- Sólo un borrador se elimina, y con él su historial: la aplicación borra primero los
-- eventos y después el pedido.
CREATE FUNCTION designaciones.permitir_borrar_pedido_borrador() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.estado <> 'borrador' THEN
        RAISE EXCEPTION 'Sólo un pedido en borrador puede eliminarse' USING ERRCODE = '42501';
    END IF;
    RETURN OLD;
END;
$$;

CREATE FUNCTION designaciones.permitir_borrar_historial_borrador() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM designaciones.pedidos p WHERE p.id = OLD.pedido_id AND p.estado = 'borrador') THEN
        RAISE EXCEPTION 'Sólo el historial de un pedido en borrador puede eliminarse' USING ERRCODE = '42501';
    END IF;
    RETURN OLD;
END;
$$;

CREATE TRIGGER trg_pedidos_borrado_solo_borrador
    BEFORE DELETE ON designaciones.pedidos
    FOR EACH ROW EXECUTE FUNCTION designaciones.permitir_borrar_pedido_borrador();

CREATE TRIGGER trg_pedido_historial_borrado_solo_borrador
    BEFORE DELETE ON designaciones.pedido_historial
    FOR EACH ROW EXECUTE FUNCTION designaciones.permitir_borrar_historial_borrador();

SELECT audit.attach('designaciones.pedido_sellos', ARRAY['periodo_id', 'secuencia']);

SELECT audit.attach('designaciones.periodo_anclas');
