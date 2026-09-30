-- Objetos del cursor y manifiestos. La función audit.log_change se reemplaza
-- por la migración EF sólo después de instalar estas estructuras.
CREATE TABLE audit.seal_cursor (
    singleton_id BOOLEAN PRIMARY KEY DEFAULT TRUE CHECK (singleton_id),
    last_seq BIGINT NOT NULL DEFAULT 0 CHECK (last_seq >= 0)
);
INSERT INTO audit.seal_cursor (singleton_id, last_seq) VALUES (TRUE, 0);

ALTER TABLE audit.change_log ADD COLUMN seal_seq BIGINT NULL;
CREATE UNIQUE INDEX change_log_seal_seq_idx
    ON audit.change_log (seal_seq) WHERE seal_seq IS NOT NULL;

CREATE TABLE audit.seal_baseline (
    id SMALLINT PRIMARY KEY CHECK (id = 1),
    legacy_max_id BIGINT NOT NULL,
    observed_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    status TEXT NOT NULL CHECK (status IN ('observed', 'anchored'))
);

CREATE TABLE audit.seal_batches (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    environment TEXT NOT NULL,
    first_seq BIGINT NOT NULL CHECK (first_seq > 0),
    last_seq BIGINT NOT NULL CHECK (last_seq >= first_seq),
    event_count BIGINT NOT NULL CHECK (event_count > 0),
    format_version SMALLINT NOT NULL CHECK (format_version > 0),
    batch_nonce UUID NOT NULL,
    batch_hash BYTEA NOT NULL CHECK (octet_length(batch_hash) = 32),
    previous_batch_hash BYTEA NULL CHECK (previous_batch_hash IS NULL OR octet_length(previous_batch_hash) = 32),
    manifest BYTEA NOT NULL CHECK (octet_length(manifest) > 0),
    manifest_hash BYTEA NOT NULL CHECK (octet_length(manifest_hash) = 32),
    signature BYTEA NULL,
    signing_key_id TEXT NULL,
    primary_witnessed_at TIMESTAMPTZ NULL,
    secondary_witnessed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT seal_batches_range_unique UNIQUE (environment, first_seq, last_seq),
    CONSTRAINT seal_batches_witness_requires_signature CHECK (
        (primary_witnessed_at IS NULL AND secondary_witnessed_at IS NULL)
        OR (signature IS NOT NULL AND signing_key_id IS NOT NULL)
    )
);
CREATE INDEX seal_batches_environment_cursor_idx
    ON audit.seal_batches (environment, last_seq DESC);

-- El default cubre una ejecución de la función trigger anterior que haya
-- quedado esperando el lock de corte: su INSERT omitía seal_seq explícitamente.
CREATE OR REPLACE FUNCTION audit.next_seal_seq()
RETURNS BIGINT
LANGUAGE plpgsql
AS $$
DECLARE
    assigned_seq BIGINT;
BEGIN
    UPDATE audit.seal_cursor
       SET last_seq = last_seq + 1
     WHERE singleton_id = TRUE
     RETURNING last_seq INTO assigned_seq;
    IF assigned_seq IS NULL THEN RAISE EXCEPTION 'audit seal cursor is not initialized'; END IF;
    RETURN assigned_seq;
END;
$$;

-- Cortar en una transacción de migración mientras se bloquea el log:
-- los eventos previos conservan seal_seq NULL y no se declaran auténticos.
LOCK TABLE audit.change_log IN ACCESS EXCLUSIVE MODE;
INSERT INTO audit.seal_baseline (id, legacy_max_id, status)
SELECT 1, COALESCE(MAX(id), 0), 'observed' FROM audit.change_log;
ALTER TABLE audit.change_log ALTER COLUMN seal_seq SET DEFAULT audit.next_seal_seq();

CREATE OR REPLACE FUNCTION audit.log_change()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    pk_col            TEXT := COALESCE(TG_ARGV[0], 'id');
    current_user_uuid UUID := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
    request_id_val    TEXT := NULLIF(current_setting('app.request_id', true), '');
    assigned_seq      BIGINT;
    new_jsonb         JSONB;
    old_jsonb         JSONB;
    pk_value          TEXT;
    changed_keys      TEXT[];
BEGIN
    IF TG_OP = 'DELETE' THEN
        old_jsonb := to_jsonb(OLD);
        pk_value := old_jsonb ->> pk_col;
    ELSE
        new_jsonb := to_jsonb(NEW);
        pk_value := new_jsonb ->> pk_col;
        IF TG_OP = 'UPDATE' THEN
            old_jsonb := to_jsonb(OLD);
            changed_keys := ARRAY(
                SELECT key FROM jsonb_each(new_jsonb)
                WHERE new_jsonb -> key IS DISTINCT FROM old_jsonb -> key
            );
            IF changed_keys = '{}'::TEXT[] THEN RETURN NEW; END IF;
        END IF;
    END IF;

    -- El default transaccional también protege escritores legados en tránsito;
    -- esta función nueva solicita explícitamente el mismo cursor.
    assigned_seq := audit.next_seal_seq();

    INSERT INTO audit.change_log (
        schema_name, table_name, row_pk, action,
        old_row, new_row, changed_columns,
        changed_by, request_id, seal_seq
    ) VALUES (
        TG_TABLE_SCHEMA, TG_TABLE_NAME, pk_value, TG_OP,
        old_jsonb, new_jsonb, changed_keys,
        current_user_uuid, request_id_val, assigned_seq
    );

    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;
