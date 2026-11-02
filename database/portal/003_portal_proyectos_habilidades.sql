-- Baseline consolidado: portal/003_portal_proyectos_habilidades.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE portal.docente_habilidades (
    perfil_id uuid NOT NULL,
    habilidad_id uuid NOT NULL,
    tipo text NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT docente_habilidades_tipo_check CHECK ((tipo = ANY (ARRAY['habilidad'::text, 'interes'::text])))
);

CREATE TABLE portal.habilidades (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    termino text NOT NULL,
    termino_norm text NOT NULL,
    sugerido boolean DEFAULT false NOT NULL,
    canonica_id uuid,
    usos integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT habilidades_termino_no_vacio CHECK ((btrim(termino) <> ''::text)),
    CONSTRAINT habilidades_usos_check CHECK ((usos >= 0))
);

CREATE TABLE portal.proyecto_documentos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    proyecto_id uuid NOT NULL,
    nombre text NOT NULL,
    fecha_carga timestamp with time zone DEFAULT now() NOT NULL,
    uri text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    archivo_id uuid,
    CONSTRAINT proyecto_documentos_nombre_no_vacio CHECK ((btrim(nombre) <> ''::text))
);

CREATE TABLE portal.proyectos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    perfil_id uuid NOT NULL,
    nombre text NOT NULL,
    rol text NOT NULL,
    descripcion text DEFAULT ''::text NOT NULL,
    desde date NOT NULL,
    hasta date,
    doi text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT proyectos_periodo_valido CHECK (((hasta IS NULL) OR (hasta >= desde)))
);

ALTER TABLE ONLY portal.docente_habilidades
    ADD CONSTRAINT docente_habilidades_pkey PRIMARY KEY (perfil_id, habilidad_id, tipo);

ALTER TABLE ONLY portal.habilidades
    ADD CONSTRAINT habilidades_pkey PRIMARY KEY (id);

ALTER TABLE ONLY portal.habilidades
    ADD CONSTRAINT habilidades_termino_norm_key UNIQUE (termino_norm);

ALTER TABLE ONLY portal.proyecto_documentos
    ADD CONSTRAINT proyecto_documentos_pkey PRIMARY KEY (id);

ALTER TABLE ONLY portal.proyecto_documentos
    ADD CONSTRAINT proyecto_documentos_proyecto_id_key UNIQUE (proyecto_id);

ALTER TABLE ONLY portal.proyectos
    ADD CONSTRAINT proyectos_pkey PRIMARY KEY (id);

CREATE INDEX proyecto_documentos_archivo_idx ON portal.proyecto_documentos USING btree (archivo_id);
