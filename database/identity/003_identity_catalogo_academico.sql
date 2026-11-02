-- Baseline consolidado: identity/003_identity_catalogo_academico.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE identity.carreras (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE identity.materias (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT materias_code_formato CHECK ((code ~ '^[0-9]{5}$'::text))
);

CREATE TABLE identity.materias_plan (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    plan_id uuid NOT NULL,
    materia_id uuid NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL
);

CREATE TABLE identity.planes (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    carrera_id uuid NOT NULL,
    codigo text NOT NULL,
    nombre text NOT NULL,
    vigente boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    activo boolean DEFAULT true NOT NULL
);

ALTER TABLE ONLY identity.carreras
    ADD CONSTRAINT carreras_code_key UNIQUE (code);

ALTER TABLE ONLY identity.carreras
    ADD CONSTRAINT carreras_pkey PRIMARY KEY (id);

ALTER TABLE ONLY identity.materias
    ADD CONSTRAINT materias_code_unico UNIQUE (code);

ALTER TABLE ONLY identity.materias
    ADD CONSTRAINT materias_pkey PRIMARY KEY (id);

ALTER TABLE ONLY identity.materias_plan
    ADD CONSTRAINT materias_plan_par_unico UNIQUE (plan_id, materia_id);

ALTER TABLE ONLY identity.materias_plan
    ADD CONSTRAINT materias_plan_pkey PRIMARY KEY (id);

ALTER TABLE ONLY identity.planes
    ADD CONSTRAINT planes_codigo_unico_por_carrera UNIQUE (carrera_id, codigo);

ALTER TABLE ONLY identity.planes
    ADD CONSTRAINT planes_pkey PRIMARY KEY (id);

CREATE INDEX materias_plan_materia_idx ON identity.materias_plan USING btree (materia_id);
