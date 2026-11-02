-- Baseline consolidado: designaciones/005_designaciones_idempotencia.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE TABLE designaciones.idempotencia_comandos (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    clave uuid NOT NULL,
    actor_id uuid NOT NULL,
    ruta text NOT NULL,
    pedido_id uuid NOT NULL,
    request_hash text NOT NULL,
    status_code integer NOT NULL,
    response_body jsonb NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT idempotencia_status_valido CHECK (((status_code >= 200) AND (status_code <= 599)))
);

ALTER TABLE ONLY designaciones.idempotencia_comandos
    ADD CONSTRAINT idempotencia_actor_ruta_clave UNIQUE (actor_id, ruta, clave);

ALTER TABLE ONLY designaciones.idempotencia_comandos
    ADD CONSTRAINT idempotencia_comandos_pkey PRIMARY KEY (id);

CREATE INDEX idempotencia_expiracion_idx ON designaciones.idempotencia_comandos USING btree (created_at);
