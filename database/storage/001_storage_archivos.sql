-- Baseline consolidado: storage/001_storage_archivos.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE SCHEMA IF NOT EXISTS storage;

CREATE TABLE storage.archivos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    proposito text NOT NULL,
    ambiente text NOT NULL,
    bucket text NOT NULL,
    clave_objeto text NOT NULL,
    nombre_original text NOT NULL,
    mime_declarado text NOT NULL,
    mime_detectado text,
    tamano_bytes bigint NOT NULL,
    sha256 text,
    estado text NOT NULL,
    propietario_id uuid NOT NULL,
    expira_en timestamp with time zone NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    confirmado_at timestamp with time zone,
    revisado_at timestamp with time zone,
    motivo_revision text,
    eliminado_at timestamp with time zone,
    CONSTRAINT archivos_ambiente_no_vacio CHECK ((btrim(ambiente) <> ''::text)),
    CONSTRAINT archivos_clave_no_vacia CHECK ((btrim(clave_objeto) <> ''::text)),
    CONSTRAINT archivos_estado_valido CHECK ((estado = ANY (ARRAY['pendiente'::text, 'cuarentena'::text, 'disponible'::text, 'rechazado'::text, 'eliminado'::text]))),
    CONSTRAINT archivos_nombre_no_vacio CHECK ((btrim(nombre_original) <> ''::text)),
    CONSTRAINT archivos_proposito_valido CHECK ((proposito = ANY (ARRAY['cv'::text, 'dni_frente'::text, 'dni_dorso'::text, 'justificativo'::text, 'documento_proyecto'::text]))),
    CONSTRAINT archivos_tamano_valido CHECK ((tamano_bytes > 0))
);

ALTER TABLE ONLY storage.archivos
    ADD CONSTRAINT archivos_ambiente_clave_unica UNIQUE (ambiente, clave_objeto);

ALTER TABLE ONLY storage.archivos
    ADD CONSTRAINT archivos_pkey PRIMARY KEY (id);

CREATE INDEX archivos_limpieza_idx ON storage.archivos USING btree (estado, expira_en);

CREATE INDEX archivos_propietario_idx ON storage.archivos USING btree (propietario_id, estado);
