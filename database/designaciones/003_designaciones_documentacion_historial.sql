-- Baseline consolidado: designaciones/003_designaciones_documentacion_historial.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE designaciones.pedido_adjuntos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    pedido_id uuid NOT NULL,
    tipo text NOT NULL,
    nombre text NOT NULL,
    uri text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    archivo_id uuid,
    CONSTRAINT pedido_adjuntos_tipo_valido CHECK ((tipo = ANY (ARRAY['cv'::text, 'dni_frente'::text, 'dni_dorso'::text, 'justificativo'::text])))
);

CREATE TABLE designaciones.pedido_historial (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    pedido_id uuid NOT NULL,
    accion text NOT NULL,
    rol_id uuid NOT NULL,
    actor_id uuid,
    etapa text NOT NULL,
    comentario text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT pedido_historial_accion_valida CHECK ((accion = ANY (ARRAY['crear'::text, 'enviar'::text, 'aceptar'::text, 'rechazar'::text, 'devolver'::text, 'reenviar'::text, 'editar'::text, 'cancelar'::text, 'priorizar'::text, 'despriorizar'::text]))),
    CONSTRAINT pedido_historial_etapa_valida CHECK ((etapa = ANY (ARRAY['borrador'::text, 'en_revision_coordinador'::text, 'en_revision_secretaria'::text, 'en_revision_decanato'::text, 'devuelto'::text, 'en_lote'::text, 'rechazado'::text, 'cancelado'::text])))
);

ALTER TABLE ONLY designaciones.pedido_adjuntos
    ADD CONSTRAINT pedido_adjuntos_pkey PRIMARY KEY (id);

ALTER TABLE ONLY designaciones.pedido_historial
    ADD CONSTRAINT pedido_historial_pkey PRIMARY KEY (id);

CREATE INDEX pedido_adjuntos_archivo_idx ON designaciones.pedido_adjuntos USING btree (archivo_id);

CREATE INDEX pedido_adjuntos_pedido_idx ON designaciones.pedido_adjuntos USING btree (pedido_id);

CREATE INDEX pedido_historial_pedido_idx ON designaciones.pedido_historial USING btree (pedido_id, created_at);
