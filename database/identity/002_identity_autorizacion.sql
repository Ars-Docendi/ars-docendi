-- Baseline consolidado: identity/002_identity_autorizacion.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE identity.permisos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code text NOT NULL,
    nombre text NOT NULL,
    descripcion text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE identity.rol_permisos (
    rol_id uuid NOT NULL,
    permiso_id uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE identity.roles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    description text,
    scope text NOT NULL,
    es_sistema boolean DEFAULT false NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT roles_scope_valid CHECK ((scope = ANY (ARRAY['global'::text, 'materia'::text, 'carrera'::text])))
);

ALTER TABLE ONLY identity.permisos
    ADD CONSTRAINT permisos_code_key UNIQUE (code);

ALTER TABLE ONLY identity.permisos
    ADD CONSTRAINT permisos_pkey PRIMARY KEY (id);

ALTER TABLE ONLY identity.rol_permisos
    ADD CONSTRAINT rol_permisos_pkey PRIMARY KEY (rol_id, permiso_id);

ALTER TABLE ONLY identity.roles
    ADD CONSTRAINT roles_code_key UNIQUE (code);

ALTER TABLE ONLY identity.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);

CREATE INDEX rol_permisos_permiso_idx ON identity.rol_permisos USING btree (permiso_id);
