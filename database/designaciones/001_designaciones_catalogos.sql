-- Baseline consolidado: designaciones/001_designaciones_catalogos.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE SCHEMA IF NOT EXISTS designaciones;

CREATE TABLE designaciones.cargos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    codigo text NOT NULL,
    nombre text NOT NULL,
    abreviatura text NOT NULL,
    orden smallint NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE designaciones.dedicaciones (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    codigo smallint NOT NULL,
    nombre text NOT NULL,
    orden smallint NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT dedicaciones_codigo_check CHECK (((codigo >= 1) AND (codigo <= 6)))
);

ALTER TABLE ONLY designaciones.cargos
    ADD CONSTRAINT cargos_codigo_key UNIQUE (codigo);

ALTER TABLE ONLY designaciones.cargos
    ADD CONSTRAINT cargos_orden_unico UNIQUE (orden);

ALTER TABLE ONLY designaciones.cargos
    ADD CONSTRAINT cargos_pkey PRIMARY KEY (id);

ALTER TABLE ONLY designaciones.dedicaciones
    ADD CONSTRAINT dedicaciones_codigo_key UNIQUE (codigo);

ALTER TABLE ONLY designaciones.dedicaciones
    ADD CONSTRAINT dedicaciones_orden_key UNIQUE (orden);

ALTER TABLE ONLY designaciones.dedicaciones
    ADD CONSTRAINT dedicaciones_pkey PRIMARY KEY (id);
