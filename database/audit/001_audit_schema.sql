-- audit schema: universal change tracking via a single AFTER trigger.
-- Tables opt in by calling audit.attach('schema.table') at the end of their
-- own SQL file (or from a wire-up file when there's a circular FK like
-- identity.users <-> audit.change_log — see 008_identity_audit_attach.sql).

CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE audit.change_log (
    id              BIGSERIAL    PRIMARY KEY,
    schema_name     TEXT         NOT NULL,
    table_name      TEXT         NOT NULL,
    row_pk          TEXT         NOT NULL,
    action          TEXT         NOT NULL,
    old_row         JSONB        NULL,
    new_row         JSONB        NULL,
    changed_columns TEXT[]       NULL,
    changed_by      UUID         NULL REFERENCES identity.users(id),
    changed_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    request_id      TEXT         NULL,
    client_ip       INET         NULL,
    CONSTRAINT change_log_action_valid CHECK (action IN ('INSERT', 'UPDATE', 'DELETE'))
);

-- Hot path: "history of row X in table Y". Also powers audit.row_history(...).
CREATE INDEX change_log_row_history_idx
    ON audit.change_log (schema_name, table_name, row_pk, changed_at DESC);

-- "Everything user U touched lately". Partial — most rows are system writes during dev.
CREATE INDEX change_log_user_idx
    ON audit.change_log (changed_by, changed_at DESC)
    WHERE changed_by IS NOT NULL;

-- BRIN over changed_at: cheap to maintain on append-only log, great for retention/partition cutoffs.
CREATE INDEX change_log_changed_at_brin
    ON audit.change_log USING BRIN (changed_at);


-- AFTER INSERT/UPDATE/DELETE: writes one row to audit.change_log per DML event.
-- TG_ARGV[0] is the PK column name (default 'id') used to extract row_pk.
CREATE OR REPLACE FUNCTION audit.log_change()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    pk_col            TEXT := COALESCE(TG_ARGV[0], 'id');
    current_user_uuid UUID := NULLIF(current_setting('app.current_user_id', true), '')::UUID;
    request_id_val    TEXT := NULLIF(current_setting('app.request_id', true), '');
    new_jsonb         JSONB;
    old_jsonb         JSONB;
    pk_value          TEXT;
    changed_keys      TEXT[];
BEGIN
    IF TG_OP = 'DELETE' THEN
        old_jsonb := to_jsonb(OLD);
        pk_value  := old_jsonb ->> pk_col;
    ELSE
        new_jsonb := to_jsonb(NEW);
        pk_value  := new_jsonb ->> pk_col;
        IF TG_OP = 'UPDATE' THEN
            old_jsonb := to_jsonb(OLD);
            changed_keys := ARRAY(
                SELECT key
                FROM jsonb_each(new_jsonb)
                WHERE new_jsonb -> key IS DISTINCT FROM old_jsonb -> key
            );
            -- No-op UPDATE: nothing actually changed, don't pollute the log.
            IF changed_keys = '{}'::TEXT[] THEN
                RETURN NEW;
            END IF;
        END IF;
    END IF;

    INSERT INTO audit.change_log (
        schema_name, table_name, row_pk, action,
        old_row, new_row, changed_columns,
        changed_by, request_id
    ) VALUES (
        TG_TABLE_SCHEMA, TG_TABLE_NAME, pk_value, TG_OP,
        old_jsonb, new_jsonb, changed_keys,
        current_user_uuid, request_id_val
    );

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;


-- Wire the audit trigger onto target_table. Idempotent — drops an existing
-- trigger of the same name before recreating, so it's safe to call again.
--   pk_col  PK column name used to populate change_log.row_pk (default 'id').
CREATE OR REPLACE FUNCTION audit.attach(
    target_table  regclass,
    pk_col        TEXT DEFAULT 'id'
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
DECLARE
    qualified  TEXT := target_table::TEXT;
    short_name TEXT;
BEGIN
    SELECT relname INTO short_name FROM pg_class WHERE oid = target_table;

    EXECUTE format(
        'DROP TRIGGER IF EXISTS trg_%I_audit_log ON %s',
        short_name, qualified);
    EXECUTE format(
        'CREATE TRIGGER trg_%I_audit_log
         AFTER INSERT OR UPDATE OR DELETE ON %s
         FOR EACH ROW EXECUTE FUNCTION audit.log_change(%L)',
        short_name, qualified, pk_col);
END;
$$;


-- Recall the standard audit metadata for a single row from the event log.
-- Replaces the per-row created_by / updated_at / updated_by / deleted_by columns
-- the codebase used to denormalize onto every table.
--   created_*  = first INSERT event for (schema, table, pk).
--   updated_*  = most recent event of any kind (covers both UPDATE and the
--                INSERT for never-updated rows).
--   deleted_*  = most recent DELETE event. NULL for rows that were soft-deleted
--                (since soft-delete is just an UPDATE) — callers that use the
--                deleted_at domain column should read it directly from the row.
CREATE OR REPLACE FUNCTION audit.row_history(
    p_schema TEXT,
    p_table  TEXT,
    p_pk     TEXT
)
RETURNS TABLE (
    created_at  TIMESTAMPTZ,
    created_by  UUID,
    updated_at  TIMESTAMPTZ,
    updated_by  UUID,
    deleted_at  TIMESTAMPTZ,
    deleted_by  UUID
)
LANGUAGE sql
STABLE
AS $$
    WITH events AS (
        SELECT action, changed_at, changed_by
          FROM audit.change_log
         WHERE schema_name = p_schema
           AND table_name  = p_table
           AND row_pk      = p_pk
    )
    SELECT
        (SELECT changed_at FROM events WHERE action = 'INSERT' ORDER BY changed_at      LIMIT 1),
        (SELECT changed_by FROM events WHERE action = 'INSERT' ORDER BY changed_at      LIMIT 1),
        (SELECT changed_at FROM events                         ORDER BY changed_at DESC LIMIT 1),
        (SELECT changed_by FROM events                         ORDER BY changed_at DESC LIMIT 1),
        (SELECT changed_at FROM events WHERE action = 'DELETE' ORDER BY changed_at DESC LIMIT 1),
        (SELECT changed_by FROM events WHERE action = 'DELETE' ORDER BY changed_at DESC LIMIT 1);
$$;
