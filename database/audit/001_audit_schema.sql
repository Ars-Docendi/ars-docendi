-- Baseline consolidado: audit/001_audit_schema.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE SCHEMA IF NOT EXISTS audit;

CREATE TABLE audit.change_log (
    id bigint NOT NULL,
    schema_name text NOT NULL,
    table_name text NOT NULL,
    row_pk text NOT NULL,
    action text NOT NULL,
    old_row jsonb,
    new_row jsonb,
    changed_columns text[],
    changed_by uuid,
    changed_at timestamp with time zone DEFAULT now() NOT NULL,
    request_id text,
    client_ip inet,
    CONSTRAINT change_log_action_valid CHECK ((action = ANY (ARRAY['INSERT'::text, 'UPDATE'::text, 'DELETE'::text])))
);

CREATE SEQUENCE audit.change_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

ALTER SEQUENCE audit.change_log_id_seq OWNED BY audit.change_log.id;

ALTER TABLE ONLY audit.change_log ALTER COLUMN id SET DEFAULT nextval('audit.change_log_id_seq'::regclass);

ALTER TABLE ONLY audit.change_log
    ADD CONSTRAINT change_log_pkey PRIMARY KEY (id);

CREATE INDEX change_log_changed_at_brin ON audit.change_log USING brin (changed_at);

CREATE INDEX change_log_row_history_idx ON audit.change_log USING btree (schema_name, table_name, row_pk, changed_at DESC);

CREATE INDEX change_log_user_idx ON audit.change_log USING btree (changed_by, changed_at DESC) WHERE (changed_by IS NOT NULL);

ALTER TABLE ONLY audit.change_log
    ADD CONSTRAINT change_log_changed_by_fkey FOREIGN KEY (changed_by) REFERENCES identity.users(id);

CREATE FUNCTION audit.attach(target_table regclass, pk_col text DEFAULT 'id'::text) RETURNS void
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

CREATE FUNCTION audit.log_change() RETURNS trigger
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

CREATE FUNCTION audit.row_history(p_schema text, p_table text, p_pk text) RETURNS TABLE(created_at timestamp with time zone, created_by uuid, updated_at timestamp with time zone, updated_by uuid, deleted_at timestamp with time zone, deleted_by uuid)
    LANGUAGE sql STABLE
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
