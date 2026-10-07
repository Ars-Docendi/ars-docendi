-- Baseline consolidado: portal/002_portal_trayectoria.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE portal.certificaciones (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    perfil_id uuid NOT NULL,
    nombre text NOT NULL,
    emisor text NOT NULL,
    fecha date NOT NULL,
    vencimiento date,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT certificaciones_vencimiento_valido CHECK (((vencimiento IS NULL) OR (vencimiento >= fecha)))
);

CREATE TABLE portal.educaciones (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    perfil_id uuid NOT NULL,
    nivel text NOT NULL,
    carrera text NOT NULL,
    institucion text NOT NULL,
    desde date NOT NULL,
    hasta date,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT educaciones_periodo_valido CHECK (((hasta IS NULL) OR (hasta >= desde)))
);

CREATE TABLE portal.experiencias (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    perfil_id uuid NOT NULL,
    puesto text NOT NULL,
    organizacion text NOT NULL,
    descripcion text DEFAULT ''::text NOT NULL,
    desde date NOT NULL,
    hasta date,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT experiencias_periodo_valido CHECK (((hasta IS NULL) OR (hasta >= desde)))
);

ALTER TABLE ONLY portal.certificaciones
    ADD CONSTRAINT certificaciones_pkey PRIMARY KEY (id);

ALTER TABLE ONLY portal.educaciones
    ADD CONSTRAINT educaciones_pkey PRIMARY KEY (id);

ALTER TABLE ONLY portal.experiencias
    ADD CONSTRAINT experiencias_pkey PRIMARY KEY (id);
