-- 20261106222026_DedicacionCatalogoYExternosDesignaciones (Designaciones).
-- La dedicación se elige sólo del catálogo (con Categoría 0) y se incorporan los docentes externos:
-- una designación o pedido es interno (cargo + dedicación) o externo (sector externo), nunca ambos.
-- No editar después de publicar: crear otra migración incremental.

-- 1. Catálogo de dedicaciones: Categoría 0 a 6.
ALTER TABLE designaciones.dedicaciones DROP CONSTRAINT dedicaciones_codigo_check;
ALTER TABLE designaciones.dedicaciones
    ADD CONSTRAINT dedicaciones_codigo_check CHECK (codigo BETWEEN 0 AND 6);

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden, activo)
VALUES ('d6000000-0000-4000-8000-000000000000', 0, 'Categoría 0', 0, true)
ON CONFLICT (codigo) DO NOTHING;

-- 2. Catálogo de sectores externos.
CREATE TABLE designaciones.sectores_externos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    codigo text NOT NULL,
    nombre text NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT sectores_externos_pkey PRIMARY KEY (id),
    CONSTRAINT sectores_externos_codigo_key UNIQUE (codigo)
);

INSERT INTO designaciones.sectores_externos (id, codigo, nombre) VALUES
    ('e7000000-0000-4000-8000-000000000001', 'ACA', 'Secretaría Académica'),
    ('e7000000-0000-4000-8000-000000000002', 'DPU', 'Dirección de Pedagogía Universitaria'),
    ('e7000000-0000-4000-8000-000000000003', 'ECO', 'Departamento de Ciencias Económicas'),
    ('e7000000-0000-4000-8000-000000000004', 'ARQ', 'Departamento de Arquitectura y Urbanismo'),
    ('e7000000-0000-4000-8000-000000000005', 'SEU', 'Secretaría de Extensión Universitaria'),
    ('e7000000-0000-4000-8000-000000000006', 'DCP', 'Departamento de Derecho y Ciencia Política'),
    ('e7000000-0000-4000-8000-000000000007', 'CSA', 'Departamento de Ciencias de la Salud'),
    ('e7000000-0000-4000-8000-000000000008', 'CYT', 'Secretaría de Ciencia y Tecnología'),
    ('e7000000-0000-4000-8000-000000000009', 'HUM', 'Departamento de Humanidades y Ciencias Sociales');

SELECT audit.attach('designaciones.sectores_externos');

-- 3. Referencia al sector en designaciones y pedidos.
ALTER TABLE designaciones.designaciones
    ADD COLUMN sector_externo_id uuid,
    ADD CONSTRAINT designaciones_sector_externo_id_fkey FOREIGN KEY (sector_externo_id)
        REFERENCES designaciones.sectores_externos(id) ON DELETE RESTRICT;
ALTER TABLE designaciones.designaciones ALTER COLUMN cargo_id DROP NOT NULL;

ALTER TABLE designaciones.pedidos
    ADD COLUMN sector_externo_id uuid,
    ADD CONSTRAINT pedidos_sector_externo_id_fkey FOREIGN KEY (sector_externo_id)
        REFERENCES designaciones.sectores_externos(id) ON DELETE RESTRICT;

-- 4. Validación de dedicación y sector: interno exige dedicación del catálogo activa; externo,
-- un sector activo. Se mantiene el rechazo de la dedicación textual: esas columnas quedan sin uso.
CREATE OR REPLACE FUNCTION designaciones.validar_sector_externo(sector uuid) RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NOT EXISTS (SELECT FROM designaciones.sectores_externos WHERE id = sector) THEN
        RAISE EXCEPTION 'El sector externo no existe' USING ERRCODE = '23503';
    END IF;
    IF NOT EXISTS (SELECT FROM designaciones.sectores_externos WHERE id = sector AND activo) THEN
        RAISE EXCEPTION 'El sector externo está inactivo' USING ERRCODE = '23514';
    END IF;
END;
$$;

CREATE OR REPLACE FUNCTION designaciones.validar_dedicacion_pedido() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.dedicacion_solicitada_id IS NOT DISTINCT FROM OLD.dedicacion_solicitada_id
       AND NEW.dedicacion_solicitada IS NOT DISTINCT FROM OLD.dedicacion_solicitada
       AND NEW.sector_externo_id IS NOT DISTINCT FROM OLD.sector_externo_id
       AND NEW.novedad = OLD.novedad THEN
        RETURN NEW;
    END IF;
    IF NEW.dedicacion_solicitada IS NOT NULL THEN
        RAISE EXCEPTION 'No se admite una nueva dedicación textual' USING ERRCODE = '23514';
    END IF;
    IF NEW.sector_externo_id IS NOT NULL THEN
        PERFORM designaciones.validar_sector_externo(NEW.sector_externo_id);
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
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION designaciones.validar_dedicacion_vigente() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND NEW.dedicacion_id IS NOT DISTINCT FROM OLD.dedicacion_id
       AND NEW.dedicacion IS NOT DISTINCT FROM OLD.dedicacion
       AND NEW.sector_externo_id IS NOT DISTINCT FROM OLD.sector_externo_id THEN
        RETURN NEW;
    END IF;
    IF NEW.dedicacion IS NOT NULL THEN
        RAISE EXCEPTION 'No se admite una nueva dedicación textual' USING ERRCODE = '23514';
    END IF;
    IF NEW.sector_externo_id IS NOT NULL THEN
        PERFORM designaciones.validar_sector_externo(NEW.sector_externo_id);
        RETURN NEW;
    END IF;
    IF NEW.dedicacion_id IS NULL THEN
        RAISE EXCEPTION 'La designación requiere una dedicación del catálogo' USING ERRCODE = '23514';
    END IF;
    IF NOT EXISTS (SELECT FROM designaciones.dedicaciones WHERE id = NEW.dedicacion_id) THEN
        RAISE EXCEPTION 'La dedicación no existe' USING ERRCODE = '23503';
    END IF;
    IF NOT EXISTS (SELECT FROM designaciones.dedicaciones WHERE id = NEW.dedicacion_id AND activo) THEN
        RAISE EXCEPTION 'La dedicación está inactiva' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

-- 5. Coherencia interno/externo.
ALTER TABLE designaciones.designaciones ADD CONSTRAINT designaciones_interna_o_externa CHECK (
    (sector_externo_id IS NULL AND cargo_id IS NOT NULL AND dedicacion_id IS NOT NULL)
    OR (sector_externo_id IS NOT NULL AND cargo_id IS NULL AND dedicacion_id IS NULL)
);

-- Un pedido no exige cargo mientras es borrador ni en una Baja: sólo se prohíbe mezclar.
ALTER TABLE designaciones.pedidos ADD CONSTRAINT pedidos_interno_o_externo CHECK (
    sector_externo_id IS NULL
    OR (cargo_solicitado_id IS NULL AND dedicacion_solicitada_id IS NULL)
);

ALTER TABLE designaciones.pedidos DROP CONSTRAINT pedidos_completitud_circuito;
ALTER TABLE designaciones.pedidos ADD CONSTRAINT pedidos_completitud_circuito CHECK (
    novedad NOT IN ('Alta', 'Cambio de cargo o dedicación')
    OR estado NOT IN ('en_revision_coordinador', 'en_revision_secretaria', 'en_revision_decanato', 'devuelto', 'en_lote')
    OR (horas IS NOT NULL AND horas > 0
        AND ((cargo_solicitado_id IS NOT NULL AND dedicacion_solicitada_id IS NOT NULL)
             OR sector_externo_id IS NOT NULL))
);

CREATE INDEX designaciones_sector_externo_idx ON designaciones.designaciones USING btree (sector_externo_id)
    WHERE sector_externo_id IS NOT NULL;

COMMENT ON TABLE designaciones.sectores_externos IS
    'Departamentos o áreas de la universidad que pagan a un docente externo: dicta en Ingeniería sin cargo ni dedicación propios del Departamento. codigo es la sigla que figura en la propuesta y en el lote (ECO, DPU, ...).';
COMMENT ON COLUMN designaciones.designaciones.sector_externo_id IS
    'Sector externo de un docente externo. Si tiene valor, la designación no tiene cargo ni dedicación; si es NULL, es interna y tiene ambos.';
COMMENT ON COLUMN designaciones.pedidos.sector_externo_id IS
    'Sector externo solicitado en un Alta o Cambio externo. Excluye cargo y dedicación solicitados.';
