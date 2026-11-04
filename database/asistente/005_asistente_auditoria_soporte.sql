-- 005_asistente_auditoria_soporte.sql
--
-- Append-only audit trail for every support read of another user's
-- conversation history (asistente.leer_historial_ajeno). See design.md D10
-- of asistente-historial-conversaciones.
--
-- WHY `hilo_historico_id` CARRIES NO FOREIGN KEY, ON PURPOSE
-- The audit row has to survive the subject deleting their own conversation
-- (asistente-historial-conversaciones lets an owner hard-delete one
-- conversation or all of them). An audit trail that a subject could
-- sabotage merely by deleting the very data support looked at would not be
-- an audit trail. Keeping `hilo_historico_id` a plain, unconstrained `uuid`
-- (null for a listing call, the specific id for a single-conversation read)
-- means a later-deleted conversation still leaves a fully intelligible
-- audit row — who read what conversation of whose, when, why — because the
-- row never stores the conversation's content, only its id. See design.md
-- D10.
--
-- WHY THIS TABLE HAS NO UPDATE/DELETE PATH ANYWHERE IN THE APPLICATION
-- "Append-only, non-deletable" (asistente-acceso-de-soporte-al-historial) is
-- enforced by never writing the code that would update or delete a row, not
-- by a database-level trigger. There is a test asserting no endpoint or
-- interface in the module offers that action.
--
-- IDEMPOTENT THE SAME WAY 002-004 ARE: `IF NOT EXISTS` throughout.

CREATE TABLE IF NOT EXISTS asistente.auditoria_acceso_historial (
    id                bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    lector_id         uuid        NOT NULL,
    sujeto_id         uuid        NOT NULL,
    hilo_historico_id uuid        NULL,
    razon             text        NOT NULL,
    ocurrido_en       timestamptz NOT NULL
);

COMMENT ON TABLE asistente.auditoria_acceso_historial IS
    'Append-only audit trail of every support read of another actor''s conversation history. hilo_historico_id carries NO foreign key on purpose: the row must outlive the subject deleting that conversation — see the header of this file and design.md D10. Retention independent of history retention, defaulting to 365 days, with automatic purge.';

COMMENT ON COLUMN asistente.auditoria_acceso_historial.hilo_historico_id IS
    'Null for a listing call (no specific conversation was opened); the specific hilo_historico.id for a single-conversation read. Deliberately NOT a foreign key — see the table comment.';

-- The retention purge deletes by ocurrido_en, independent of whether the
-- referenced conversation (or its actor) still exists.
CREATE INDEX IF NOT EXISTS ix_auditoria_acceso_historial_ocurrido_en
    ON asistente.auditoria_acceso_historial (ocurrido_en);

-- ------------------------------------------- el asistente no lee su propia auditoría
--
-- Same fact as 004_asistente_historial.sql: no new REVOKE needed. This
-- table lives inside the `asistente` schema, already denied wholesale to
-- both read-only roles by 002_asistente_registros.sql. There is a
-- regression test next to ManifiestoPrivilegiosTests/PrivilegiosLecturaTests
-- asserting neither role can SELECT from this table.
