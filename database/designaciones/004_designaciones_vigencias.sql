-- Baseline consolidado: designaciones/004_designaciones_vigencias.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;
COMMENT ON EXTENSION btree_gist IS 'support for indexing common datatypes in GiST';

CREATE TABLE designaciones.designaciones (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    persona_id uuid NOT NULL,
    cargo_id uuid NOT NULL,
    dedicacion text,
    horas integer NOT NULL,
    vigente_desde date NOT NULL,
    vigente_hasta date,
    origen_pedido_id uuid,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    dedicacion_id uuid,
    horas_investigacion integer,
    horas_externas integer,
    materia_id uuid NOT NULL,
    carrera_id uuid NOT NULL,
    CONSTRAINT designaciones_dedicacion_valida CHECK (((dedicacion IS NULL) OR (dedicacion = ANY (ARRAY['Categoría 0'::text, 'Categoría 1'::text, 'Categoría 2'::text, 'Categoría 3'::text, 'Categoría 4'::text, 'Categoría 5'::text, 'Categoría 6'::text])))),
    CONSTRAINT designaciones_horas_externas_check CHECK ((horas_externas >= 0)),
    CONSTRAINT designaciones_horas_investigacion_check CHECK ((horas_investigacion >= 0)),
    CONSTRAINT designaciones_horas_positivas CHECK ((horas > 0)),
    CONSTRAINT designaciones_vigencia_coherente CHECK (((vigente_hasta IS NULL) OR (vigente_hasta > vigente_desde)))
);

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_pkey PRIMARY KEY (id);

ALTER TABLE ONLY designaciones.designaciones
    ADD CONSTRAINT designaciones_sin_solapamiento EXCLUDE USING gist (persona_id WITH =, materia_id WITH =, daterange(vigente_desde, vigente_hasta, '[)'::text) WITH &&);

CREATE INDEX designaciones_carrera_idx ON designaciones.designaciones USING btree (carrera_id);

CREATE INDEX designaciones_materia_vigente_idx ON designaciones.designaciones USING btree (materia_id) WHERE (vigente_hasta IS NULL);

CREATE INDEX designaciones_origen_pedido_idx ON designaciones.designaciones USING btree (origen_pedido_id) WHERE (origen_pedido_id IS NOT NULL);

CREATE INDEX designaciones_persona_vigente_idx ON designaciones.designaciones USING btree (persona_id) WHERE (vigente_hasta IS NULL);
