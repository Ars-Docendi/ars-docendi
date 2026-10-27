-- 010_asistente_calidad.sql
--
-- Quality metrics (asistente-metricas-de-calidad, slice 1): the failure cause, the
-- model and the replaced flag on the analytic record, plus the audit table for
-- reads of the problem list. See design.md D2-D5.
--
-- INERT UNTIL SOMETHING WRITES IT. The three columns are nullable or defaulted, so
-- every existing row stays valid and nothing reads them until the quality readers
-- ship.
--
-- DO NOT ADD AN ACTOR OR A PRECISE TIME TO registro_analitico (TD-012). The three
-- columns below are coarse attributes of the turn, not of the person: `causa` is a
-- closed vocabulary, `modelo` is a small catalog string shared with
-- registro_operativo.proveedor (accepted in design D3 and recorded in TD-012), and
-- `reemplazado` is a boolean. Anything finer turns the analytic table back into a
-- joinable one.
--
-- IDEMPOTENT LIKE 002-009: one `ADD COLUMN IF NOT EXISTS` per statement (the only
-- ALTER shape the module's DDL guard allows), a guarded DO block for the CHECK
-- (PostgreSQL has no `ADD CONSTRAINT IF NOT EXISTS`) and `CREATE TABLE IF NOT
-- EXISTS`. The new table lives inside the `asistente` schema, already denied
-- wholesale to both read-only roles, so it needs no entry in any manifest.

-- ------------------------------------------------------ registro_analitico

ALTER TABLE asistente.registro_analitico ADD COLUMN IF NOT EXISTS causa text;
ALTER TABLE asistente.registro_analitico ADD COLUMN IF NOT EXISTS modelo text;
ALTER TABLE asistente.registro_analitico ADD COLUMN IF NOT EXISTS reemplazado boolean NOT NULL DEFAULT false;

-- The closed vocabulary of design D2. Adding a code later needs a ratified CHECK
-- replacement in ArquitecturaAsistenteTests.ReemplazosDeCheckRatificados.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'asistente.registro_analitico'::regclass
           AND conname = 'registro_analitico_causa_valida'
    ) THEN
        ALTER TABLE asistente.registro_analitico
            ADD CONSTRAINT registro_analitico_causa_valida
            CHECK (causa IS NULL OR causa IN (
                'proveedor_no_disponible',
                'proveedor_saturado',
                'timeout_proveedor',
                'credencial_rechazada',
                'modelo_no_encontrado',
                'error_del_proveedor',
                'techo_de_llamadas',
                'presupuesto_del_turno',
                'generacion_cortada',
                'respuesta_ininteligible',
                'validador_rechazo',
                'plan_invalido',
                'sin_privilegio_de_datos',
                'error_del_motor',
                'timeout_de_sentencia',
                'excepcion',
                'resultado_vacio',
                'cupo_agotado',
                'tope_organizacional',
                'mantenimiento',
                'acceso_revocado',
                'turno_concurrente'
            ));
    END IF;
END
$$;

COMMENT ON COLUMN asistente.registro_analitico.causa IS
    'Why the turn failed or came back empty, from a closed vocabulary (design D2 of asistente-metricas-de-calidad). Null on a normal answer and on a model''s own declination (that is not a cause). TD-012: never add an actor or a precise time to this table.';
COMMENT ON COLUMN asistente.registro_analitico.modelo IS
    'empresa/modelo of the adapter resolved for the turn, also on turns that never called a model. Shared with registro_operativo.proveedor (accepted in design D3, noted in TD-012). TD-012: never add an actor or a precise time to this table.';
COMMENT ON COLUMN asistente.registro_analitico.reemplazado IS
    'True when the user replaced this answered turn by editing and resending (implicit dissatisfaction). Set by a separate UPDATE after the replacement completes; unset when the turn''s id is unknown (non-answered turn, or a restart). TD-012: never add an actor or a precise time to this table.';

-- ------------------------------------------ auditoria_revision_de_calidad
--
-- One row per successful read of the quality problem list, written before the
-- response (same discipline as auditoria_acceso_historial): who read, when, which
-- filters and how many rows. NO foreign keys, so the trail outlives anything it
-- refers to. Append-only, purged by its own retention option.
CREATE TABLE IF NOT EXISTS asistente.auditoria_revision_de_calidad (
    id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    lector_id   uuid        NOT NULL,
    ocurrido_en timestamptz NOT NULL,
    filtros     text        NOT NULL,
    filas       integer     NOT NULL
);

COMMENT ON TABLE asistente.auditoria_revision_de_calidad IS
    'Append-only audit trail of every read of the quality problem list (anonymous question text). No foreign keys on purpose. Retention independent of the analytic record, defaulting to 365 days, with automatic purge.';
COMMENT ON COLUMN asistente.auditoria_revision_de_calidad.lector_id IS
    'The reviewer who read the list.';
COMMENT ON COLUMN asistente.auditoria_revision_de_calidad.filtros IS
    'The filters of the read (period, model, type, page), as a short text.';
COMMENT ON COLUMN asistente.auditoria_revision_de_calidad.filas IS
    'How many rows the read returned.';

CREATE INDEX IF NOT EXISTS ix_auditoria_revision_de_calidad_ocurrido_en
    ON asistente.auditoria_revision_de_calidad (ocurrido_en);
