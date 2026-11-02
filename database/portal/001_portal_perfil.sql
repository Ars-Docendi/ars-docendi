-- Baseline consolidado: portal/001_portal_perfil.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE SCHEMA IF NOT EXISTS portal;

CREATE TABLE portal.contactos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    perfil_id uuid NOT NULL,
    telefono text,
    mail text,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE portal.cvs (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    perfil_id uuid NOT NULL,
    nombre text NOT NULL,
    fecha_carga timestamp with time zone DEFAULT now() NOT NULL,
    uri text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    archivo_id uuid,
    CONSTRAINT cvs_nombre_no_vacio CHECK ((btrim(nombre) <> ''::text))
);

CREATE TABLE portal.perfiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    persona_id uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

ALTER TABLE ONLY portal.contactos
    ADD CONSTRAINT contactos_perfil_id_key UNIQUE (perfil_id);

ALTER TABLE ONLY portal.contactos
    ADD CONSTRAINT contactos_pkey PRIMARY KEY (id);

ALTER TABLE ONLY portal.cvs
    ADD CONSTRAINT cvs_perfil_id_key UNIQUE (perfil_id);

ALTER TABLE ONLY portal.cvs
    ADD CONSTRAINT cvs_pkey PRIMARY KEY (id);

ALTER TABLE ONLY portal.perfiles
    ADD CONSTRAINT perfiles_persona_id_key UNIQUE (persona_id);

ALTER TABLE ONLY portal.perfiles
    ADD CONSTRAINT perfiles_pkey PRIMARY KEY (id);

CREATE INDEX cvs_archivo_idx ON portal.cvs USING btree (archivo_id);
