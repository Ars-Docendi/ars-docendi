-- 004_asistente_historial.sql
--
-- Own conversation history: one row per persisted conversation
-- (hilo_historico) and one row per persisted turn (turno_historico).
--
-- WHY THIS TABLE IS ACTOR-LINKED, UNLIKE registro_analitico/registro_operativo
-- Every other table this module writes exists to answer "how much is the
-- assistant used" and "what gets asked" WITHOUT being able to answer "who
-- asked it" (TD-012). This table exists for the opposite reason: "own
-- history" only means something if it is attributable to its owner, and the
-- product explicitly needs "user X asked Y" to be answerable through the
-- user's own history and through audited support access
-- (asistente-acceso-de-soporte-al-historial). See design.md D1/D12 of
-- asistente-historial-conversaciones. This is a deliberate, new privacy
-- surface, not an oversight — see the schema-level deny below and
-- manifiesto-privilegios.json for how it stays contained.
--
-- NEVER ADD A FOREIGN KEY OR JOIN PATH FROM THESE TABLES BACK TO
-- registro_analitico, registro_operativo, OR retroalimentacion_turno.
-- Those three exist specifically so that "what was asked" and "who asked
-- it" cannot be joined together (TD-012) and so that a feedback vote cannot
-- be tied to an actor. This table already carries the actor↔question link
-- on purpose, but it must stay its OWN, separate channel: linking it to any
-- of the other three would let a reader of this table also resolve the
-- anonymous/unlinkable side of those, which is exactly the channel they
-- exist to close. See design.md D8.
--
-- `ultima_actividad`, NOT EACH TURN'S OWN TIMESTAMP, DRIVES RETENTION.
-- Retention (OpcionesAsistente.RetencionDeHistorialDias, default 180 days) is
-- a property of the CONVERSATION, not of any one turn: a conversation
-- resumed and added to nine months later must not have its oldest turns
-- pruned out from under it while the conversation itself is still active.
-- See design.md D7.
--
-- EVERY CONVERSATION IS RECORDED HERE — THERE IS NO PER-CONVERSATION
-- OPT-OUT, BY EXPLICIT PRODUCT DECISION. The one and only turn outcome that
-- never gets a row is EstadoDelTurno.Fallo (an unhandled exception that
-- never produced an HTTP response) — see design.md D2. This is a code-level
-- guard (in CapaConversacional's registration hook), not a schema
-- constraint: this table has no column that could express "opted out".
--
-- IDEMPOTENT THE SAME WAY 002/003 ARE: `IF NOT EXISTS` throughout, so
-- re-running does not fail. Nothing here adds a column later; if one ever
-- is needed, it goes here AND as `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`,
-- same rule as 002_asistente_registros.sql.

CREATE TABLE IF NOT EXISTS asistente.hilo_historico (
    id                      uuid        PRIMARY KEY,
    actor_id                uuid        NOT NULL,
    titulo                  text        NOT NULL,
    creado_en               timestamptz NOT NULL,
    ultima_actividad        timestamptz NOT NULL,
    archivada_en            timestamptz NULL,
    borrado_pendiente_desde timestamptz NULL,
    lote_de_borrado         uuid        NULL
);

CREATE TABLE IF NOT EXISTS asistente.turno_historico (
    id            uuid        PRIMARY KEY,
    hilo_id       uuid        NOT NULL
                              REFERENCES asistente.hilo_historico(id) ON DELETE CASCADE,
    pregunta      text        NOT NULL,
    sql_resuelto  text        NULL,
    estado        text        NOT NULL,
    ocurrido_en   timestamptz NOT NULL,
    referencias   jsonb       NULL
);

-- LAS CUATRO COLUMNAS DE ABAJO SE AGREGARON DESPUÉS DE QUE LAS TABLAS
-- EXISTIERAN, y por eso van también como ALTER — mismo motivo, y mismo
-- riesgo si se lo salteara, que documenta 002_asistente_registros.sql: contra
-- una base que ya tenía la tabla, el CREATE de arriba es un no-op.
--
-- `archivada_en`, `borrado_pendiente_desde` y `lote_de_borrado`
-- (asistente-rediseno-v3, design.md D3/D4 de asistente-historial-conversaciones):
-- archivar es un timestamp nulable, sin efecto sobre `ultima_actividad`
-- (retención no se mueve); un borrado marca las dos últimas en vez de
-- ejecutar el DELETE en el momento — el DELETE de verdad lo hace el barrido
-- de un minuto (`BarridoDeBorradosPendientes`) y, como red, la purga diaria
-- — así que la ventana de «Deshacer» sobrevive a que se cierre la pestaña.
--
-- `referencias` (asistente-menciones, design.md D11): marcador `$refN` → tipo
-- y id de la mención citada en la SQL de este turno, para que «Volver a
-- consultar» y «Reanudar» puedan volver a bindearlos. Nula en todo turno sin
-- menciones, que sigue siendo el caso normal.
ALTER TABLE asistente.hilo_historico
    ADD COLUMN IF NOT EXISTS archivada_en timestamptz;

ALTER TABLE asistente.hilo_historico
    ADD COLUMN IF NOT EXISTS borrado_pendiente_desde timestamptz;

ALTER TABLE asistente.hilo_historico
    ADD COLUMN IF NOT EXISTS lote_de_borrado uuid;

ALTER TABLE asistente.turno_historico
    ADD COLUMN IF NOT EXISTS referencias jsonb;

-- El barrido y las lecturas de soporte dentro de la ventana filtran por
-- `borrado_pendiente_desde`; el índice parcial sólo cubre las filas
-- efectivamente pendientes, que son las únicas que esas dos consultas tocan.
CREATE INDEX IF NOT EXISTS ix_hilo_historico_borrado_pendiente
    ON asistente.hilo_historico (borrado_pendiente_desde)
 WHERE borrado_pendiente_desde IS NOT NULL;

COMMENT ON TABLE asistente.hilo_historico IS
    'Own conversation history, one row per persisted conversation. Actor-linked ON PURPOSE, unlike registro_analitico/registro_operativo — see the header of this file and design.md D1/D12 of asistente-historial-conversaciones. Retention of 180 days by default, counted from ultima_actividad (not creado_en), with automatic purge.';

COMMENT ON TABLE asistente.turno_historico IS
    'One row per persisted turn: question, resolved SQL (nullable), outcome state, timestamp. NEVER the rows a query returned, and NEVER the drafted answer text — see design.md D1 of the same change. Cascades from hilo_historico on delete.';

-- The list/search surface (list own conversations, full-text search over own
-- questions) is scoped by actor and ordered/filtered by recency, so the
-- index matches that access pattern exactly.
CREATE INDEX IF NOT EXISTS ix_hilo_historico_actor_ultima_actividad
    ON asistente.hilo_historico (actor_id, ultima_actividad);

-- Full-text search over the actor's own past questions (design.md D6):
-- PostgreSQL's own text search with Spanish stemming, not a leading-wildcard
-- ILIKE (which cannot use an index and does not fold accents/stems).
CREATE INDEX IF NOT EXISTS ix_turno_historico_pregunta_fts
    ON asistente.turno_historico USING gin (to_tsvector('spanish', pregunta));

-- ------------------------------------------- el asistente no lee su propio historial
--
-- No new REVOKE needed here, and that is a fact this file asserts rather
-- than a gap: 002_asistente_registros.sql already revokes the ENTIRE
-- `asistente` schema (REVOKE ALL ON SCHEMA, REVOKE ALL ON ALL TABLES IN
-- SCHEMA) from both asistente_ro and asistente_ro_pii. A table created
-- inside an already wholesale-denied schema inherits that denial for free —
-- same precedent as asistente.retroalimentacion_turno in
-- 003_asistente_retroalimentacion.sql. There is a regression test next to
-- ManifiestoPrivilegiosTests/PrivilegiosLecturaTests asserting neither role
-- can SELECT from either table here.
