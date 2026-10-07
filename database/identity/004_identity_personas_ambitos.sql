-- Baseline consolidado: identity/004_identity_personas_ambitos.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE identity.personas (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    documento text NOT NULL,
    cuil text,
    legajo text,
    nombre text NOT NULL,
    apellido text NOT NULL,
    fecha_nacimiento date,
    telefono text,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE identity.user_roles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    role_id uuid NOT NULL,
    materia_id uuid,
    carrera_id uuid,
    granted_at timestamp with time zone DEFAULT now() NOT NULL,
    granted_by uuid,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    deleted_at timestamp with time zone
);

ALTER TABLE ONLY identity.personas
    ADD CONSTRAINT personas_documento_key UNIQUE (documento);

ALTER TABLE ONLY identity.personas
    ADD CONSTRAINT personas_legajo_key UNIQUE (legajo);

ALTER TABLE ONLY identity.personas
    ADD CONSTRAINT personas_pkey PRIMARY KEY (id);

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT user_roles_pkey PRIMARY KEY (id);

CREATE INDEX personas_apellido_nombre_idx ON identity.personas USING btree (apellido, nombre);

CREATE INDEX user_roles_carrera_idx ON identity.user_roles USING btree (carrera_id) WHERE (carrera_id IS NOT NULL);

CREATE INDEX user_roles_materia_idx ON identity.user_roles USING btree (materia_id) WHERE (materia_id IS NOT NULL);

CREATE UNIQUE INDEX user_roles_unique_assignment ON identity.user_roles USING btree (user_id, role_id, materia_id, carrera_id) NULLS NOT DISTINCT WHERE (deleted_at IS NULL);

CREATE INDEX user_roles_user_idx ON identity.user_roles USING btree (user_id);
