-- 003_asistente_retroalimentacion.sql
--
-- Turn-level user feedback (thumbs up/down + optional reason), keyed ONLY by
-- the analytic row's own id (asistente.registro_analitico.id). No actor
-- column, no session column, no column shared with asistente.registro_operativo.
--
-- WHY THIS TABLE STAYS LINKED TO registro_analitico AND NOTHING ELSE
-- TD-012 (see 002_asistente_registros.sql) exists to make it impossible to
-- join "who asked" with "what was asked". A feedback row that carried an
-- actor id, or that could be joined against registro_operativo, would reopen
-- exactly that channel one layer up. The foreign key below only points at
-- registro_analitico, on purpose.
--
-- WHY analitico_id IS THE PRIMARY KEY AND NOT A SEPARATE SURROGATE ID
-- One feedback row per turn. A vote can change; it never accumulates
-- history (design.md D4). A separate surrogate key would let more than one
-- row exist per turn, which is exactly what this design forbids.
--
-- WHY `razon` IS CONSTRAINED TO FOUR VALUES INSTEAD OF FREE TEXT
-- Free text is how a rare, identifying complaint ends up sitting next to an
-- otherwise-anonymous row (the same class of risk TD-012 already calls out
-- for `intencion_sombra`). A closed vocabulary can't carry that.
--
-- IDEMPOTENT THE SAME WAY 002 IS: `IF NOT EXISTS` on the table. Nothing here
-- adds a column later, so there is no `ALTER TABLE ... ADD COLUMN IF NOT
-- EXISTS` yet — if one is ever needed, it goes here AND in the CREATE, same
-- rule as 002.

CREATE TABLE IF NOT EXISTS asistente.retroalimentacion_turno (
    analitico_id   uuid        PRIMARY KEY
                                REFERENCES asistente.registro_analitico(id) ON DELETE CASCADE,
    voto           boolean     NOT NULL,
    razon          text        NULL
                                CONSTRAINT retroalimentacion_turno_razon_valida
                                CHECK (razon IS NULL OR razon IN (
                                    'datos_incorrectos',
                                    'no_entendio_la_pregunta',
                                    'lento',
                                    'otro'
                                )),
    actualizado_en timestamptz NOT NULL
);

-- DO NOT ADD AN ACTOR COLUMN HERE "FOR CONSISTENCY" WITH registro_operativo.
-- registro_operativo carries actor_id because it exists to answer "who used
-- the assistant and what did it cost". This table exists to answer "was
-- this answer good", on a row that is deliberately unattributable. Adding
-- an actor column here does not make the two tables more consistent; it
-- deletes the one property this table has to have.
--
-- DO NOT ADD THIS TABLE'S KEY, OR ANY VOTE OF IT, TO registro_operativo
-- EITHER, for the mirror-image reason: registro_operativo already carries
-- actor_id, so writing analitico_id there would let anyone with read access
-- to that one table alone reconstruct the exact join TD-012 exists to
-- prevent — no cross-table correlation needed.
--
-- DO NOT ADD A TIMESTAMP MORE PRECISE THAN `actualizado_en` NEEDS TO BE FOR
-- ITS OWN PURPOSE (letting an upsert report when the vote last changed).
-- `actualizado_en` is not a substitute for `registro_analitico.dia`'s
-- rounding: it lives on a row that already has no actor by construction,
-- so a precise timestamp here does not, by itself, reopen the join. It
-- would only become a problem if this table ever grew a column that DOES
-- correlate with an actor — which is exactly the two things forbidden
-- above. Keep it that way.
--
-- There is a regression test next to ManifiestoPrivilegiosTests /
-- PrivilegiosLecturaTests asserting neither `asistente_ro` nor
-- `asistente_ro_pii` can read this table.

COMMENT ON TABLE asistente.retroalimentacion_turno IS
    'User feedback (thumbs up/down + optional reason) for an answered turn, keyed only by asistente.registro_analitico.id. No actor column, on purpose: see the comments above and TD-012.';

-- --------------------------------------------------- no audit attached, either
--
-- Same declared exception as 002_asistente_registros.sql, and the same
-- reason: audit.change_log stores the whole row as JSON with no retention
-- policy of its own. Attaching it here would let a feedback row (and the
-- analytic id it's keyed by) outlive the 90-day purge in a third place.
-- There is a test that fails if a trigger appears on this table.

-- --------------------------------------------------- no new GRANT needed
--
-- This table lives in the `asistente` schema, which 002_asistente_registros.sql
-- already revokes wholesale from both `asistente_ro` and `asistente_ro_pii`
-- (schema-level REVOKE, not per-table). A new table created inside an
-- already-denied schema inherits that denial for free: neither role has
-- USAGE on the schema itself, so no additional REVOKE and no manifest
-- change are needed here.
