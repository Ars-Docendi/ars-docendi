-- Baseline consolidado: designaciones/002_designaciones_pedidos.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE designaciones.pedidos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    numero text NOT NULL,
    periodo_id uuid NOT NULL,
    persona_id uuid NOT NULL,
    novedad text NOT NULL,
    estado text DEFAULT 'borrador'::text NOT NULL,
    prioritario boolean DEFAULT false NOT NULL,
    cargo_solicitado_id uuid,
    dedicacion_solicitada text,
    horas integer,
    horas_investigacion integer,
    horas_externas integer,
    justificacion text,
    tipo_baja text,
    tipo_baja_detalle text,
    etapa_retorno text,
    propietario_actual text,
    snapshot jsonb,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    dedicacion_solicitada_id uuid,
    materia_id uuid NOT NULL,
    carrera_id uuid NOT NULL,
    CONSTRAINT pedidos_dedicacion_valida CHECK (((dedicacion_solicitada IS NULL) OR (dedicacion_solicitada = ANY (ARRAY['Categoría 0'::text, 'Categoría 1'::text, 'Categoría 2'::text, 'Categoría 3'::text, 'Categoría 4'::text, 'Categoría 5'::text, 'Categoría 6'::text])))),
    CONSTRAINT pedidos_estado_valido CHECK ((estado = ANY (ARRAY['borrador'::text, 'en_revision_coordinador'::text, 'en_revision_secretaria'::text, 'en_revision_decanato'::text, 'devuelto'::text, 'en_lote'::text, 'rechazado'::text, 'cancelado'::text]))),
    CONSTRAINT pedidos_horas_no_negativas CHECK ((((horas IS NULL) OR (horas >= 0)) AND ((horas_investigacion IS NULL) OR (horas_investigacion >= 0)) AND ((horas_externas IS NULL) OR (horas_externas >= 0)))),
    CONSTRAINT pedidos_novedad_valida CHECK ((novedad = ANY (ARRAY['Sin novedad'::text, 'Alta'::text, 'Baja'::text, 'Cambio de cargo o dedicación'::text]))),
    CONSTRAINT pedidos_tipo_baja_valido CHECK (((tipo_baja IS NULL) OR (tipo_baja = ANY (ARRAY['Renuncia'::text, 'Jubilación'::text, 'Otro'::text]))))
);

CREATE TABLE designaciones.periodos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    nombre text NOT NULL,
    carga_desde date NOT NULL,
    carga_hasta date NOT NULL,
    impacto_desde date NOT NULL,
    impacto_hasta date NOT NULL,
    activo boolean DEFAULT false NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT periodos_carga_coherente CHECK ((carga_hasta >= carga_desde)),
    CONSTRAINT periodos_impacto_coherente CHECK ((impacto_hasta >= impacto_desde))
);

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_numero_key UNIQUE (numero);

ALTER TABLE ONLY designaciones.pedidos
    ADD CONSTRAINT pedidos_pkey PRIMARY KEY (id);

ALTER TABLE ONLY designaciones.periodos
    ADD CONSTRAINT periodos_pkey PRIMARY KEY (id);

CREATE INDEX pedidos_carrera_idx ON designaciones.pedidos USING btree (carrera_id);

CREATE INDEX pedidos_materia_idx ON designaciones.pedidos USING btree (materia_id);

CREATE INDEX pedidos_periodo_estado_idx ON designaciones.pedidos USING btree (periodo_id, estado);

CREATE INDEX pedidos_persona_idx ON designaciones.pedidos USING btree (persona_id);

CREATE UNIQUE INDEX pedidos_uno_por_docente_periodo ON designaciones.pedidos USING btree (periodo_id, persona_id) WHERE (estado <> ALL (ARRAY['rechazado'::text, 'cancelado'::text]));

CREATE UNIQUE INDEX periodos_unico_activo ON designaciones.periodos USING btree ((true)) WHERE activo;
