CREATE TABLE designaciones.dedicaciones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo SMALLINT NOT NULL UNIQUE CHECK (codigo BETWEEN 1 AND 6),
    nombre TEXT NOT NULL,
    orden SMALLINT NOT NULL UNIQUE,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO designaciones.dedicaciones (id, codigo, nombre, orden)
SELECT ('d6000000-0000-4000-8000-' || lpad(codigo::text, 12, '0'))::uuid,
       codigo, 'Categoría ' || codigo, codigo
FROM generate_series(1, 6) AS codigo;

ALTER TABLE designaciones.pedidos
    ADD COLUMN dedicacion_solicitada_id UUID REFERENCES designaciones.dedicaciones(id);
ALTER TABLE designaciones.designaciones
    ADD COLUMN dedicacion_id UUID REFERENCES designaciones.dedicaciones(id);

-- Sólo se migran coincidencias exactas. Los textos fuera de catálogo siguen
-- disponibles como legado; los snapshots conservan su contenido original.
UPDATE designaciones.pedidos p
SET dedicacion_solicitada_id = d.id
FROM designaciones.dedicaciones d
WHERE p.dedicacion_solicitada = d.nombre;

UPDATE designaciones.designaciones v
SET dedicacion_id = d.id
FROM designaciones.dedicaciones d
WHERE v.dedicacion = d.nombre;

SELECT audit.attach('designaciones.dedicaciones');
