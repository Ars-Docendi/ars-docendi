-- Baseline consolidado: identity/001_identity_users.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE identity.users (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    azure_oid uuid NOT NULL,
    upn text NOT NULL,
    display_name text NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    last_login_at timestamp with time zone,
    persona_id uuid
);

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_azure_oid_key UNIQUE (azure_oid);

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_upn_key UNIQUE (upn);

CREATE UNIQUE INDEX users_persona_unica ON identity.users USING btree (persona_id) WHERE (persona_id IS NOT NULL);
