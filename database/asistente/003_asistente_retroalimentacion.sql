-- 003_asistente_retroalimentacion.sql
--
-- Turn-level user feedback (thumbs up/down + reasons + a bounded comment),
-- keyed ONLY by the analytic row's own id (asistente.registro_analitico.id).
-- No actor column, no session column, no column shared with
-- asistente.registro_operativo.
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
-- WHY `razones` IS A LIST FROM A CLOSED SET, NOT FREE TEXT
-- Free text picked from an unbounded vocabulary is how a rare, identifying
-- complaint ends up sitting next to an otherwise-anonymous row (the same
-- class of risk TD-012 already calls out for `intencion_sombra`). A closed
-- vocabulary can't carry that; the CHECK below only restricts membership
-- (each element in the four values) — "no duplicates" is validated by the
-- controller, the table's only writer, the same way "at most one of the four"
-- used to be.
--
-- WHY `comentario` EXISTS DESPITE THAT SAME RISK (design.md D7/D14 of
-- asistente-rediseno-v3, PO-changed 2026-09-26)
-- The product owner confirmed the mock's free-text comment ships. It is
-- bounded to 500 characters (CHECK below), trimmed before storage, hinted at
-- in the UI ("No incluyas datos personales."), ages out with the same 90-day
-- purge as the rest of this row, and has no read surface anywhere (no admin
-- screen) — the risk is bounded and documented (TD-012 addendum in
-- docs/quality/tech-debt.md) rather than avoided.
--
-- WHY THERE IS NO `razon` COLUMN HERE ANYMORE
-- The legacy single-reason column (and the retired reason `lento` it used to
-- also accept) is gone from this file entirely: nothing has shipped to
-- production — the feedback table never reached `develop` — so there is no
-- row to preserve and no reason to keep dead provisions "just in case". A
-- base that already has the old `razon` column (from before this round) is
-- NOT rewritten: see the ADD COLUMN statements below for why, and why that
-- column is simply left in place, unused.
--
-- IDEMPOTENT THE SAME WAY 002 IS: `IF NOT EXISTS` on the table for a fresh
-- base. For a base that already has the table (with only `razon`, from
-- before this round), the two `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`
-- statements below add `razones` and `comentario`, and the two guarded `DO`
-- blocks after them add their CHECK constraints. This module's migration
-- convention (see ArquitecturaAsistenteTests) never DROPs or rewrites
-- anything that already exists, so the old `razon` column and its original
-- CHECK are left exactly as they were: nothing in this module reads or
-- writes them anymore, so there is nothing to converge.

CREATE TABLE IF NOT EXISTS asistente.retroalimentacion_turno (
    analitico_id   uuid        PRIMARY KEY
                                REFERENCES asistente.registro_analitico(id) ON DELETE CASCADE,
    voto           boolean     NOT NULL,
    razones        text[]      NULL
                                CONSTRAINT retroalimentacion_turno_razones_validas
                                CHECK (razones IS NULL OR razones <@ ARRAY[
                                    'datos_incorrectos',
                                    'no_entendio_la_pregunta',
                                    'faltan_datos',
                                    'otro'
                                ]::text[]),
    comentario     text        NULL
                                CONSTRAINT retroalimentacion_turno_comentario_longitud
                                CHECK (comentario IS NULL OR char_length(comentario) <= 500),
    actualizado_en timestamptz NOT NULL
);

-- REACHING A BASE THAT ALREADY HAD THE TABLE BEFORE `razones`/`comentario`
-- EXISTED (the only real case today: arsdocendi_pr_140). `CREATE TABLE IF NOT
-- EXISTS` is a no-op there, so these two columns would never appear without
-- the statements below. One column per statement, no inline CHECK: a CHECK
-- naming the four-value array literal has commas, which the module's only
-- permitted `ALTER TABLE` form (`ADD COLUMN IF NOT EXISTS <col> <type>;`,
-- no comma before the semicolon) does not allow — see
-- ArquitecturaAsistenteTests.DestruccionEnSql.
ALTER TABLE asistente.retroalimentacion_turno ADD COLUMN IF NOT EXISTS razones text[];
ALTER TABLE asistente.retroalimentacion_turno ADD COLUMN IF NOT EXISTS comentario text;

-- ADDING THE TWO CHECKS ON A BASE THAT ALREADY HAS THE TABLE
-- PostgreSQL has no `ADD CONSTRAINT IF NOT EXISTS`, so a plain `ALTER TABLE
-- ... ADD CONSTRAINT` run twice would fail on the second migrator pass. Each
-- block below adds its constraint only when a constraint with that exact
-- name does not exist yet, which is idempotent and does not depend on file
-- order. Both are, textually, `ALTER TABLE` statements that are not the one
-- permitted form, so both names are ratified exceptions listed by name in
-- ArquitecturaAsistenteTests.ReemplazosDeCheckRatificados — even though
-- neither one DROPs anything, because the detector's only unconditionally
-- allowed `ALTER TABLE` shape is `ADD COLUMN IF NOT EXISTS`.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'asistente.retroalimentacion_turno'::regclass
           AND conname = 'retroalimentacion_turno_razones_validas'
    ) THEN
        ALTER TABLE asistente.retroalimentacion_turno
            ADD CONSTRAINT retroalimentacion_turno_razones_validas
            CHECK (razones IS NULL OR razones <@ ARRAY[
                'datos_incorrectos',
                'no_entendio_la_pregunta',
                'faltan_datos',
                'otro'
            ]::text[]);
    END IF;
END
$$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_constraint
         WHERE conrelid = 'asistente.retroalimentacion_turno'::regclass
           AND conname = 'retroalimentacion_turno_comentario_longitud'
    ) THEN
        ALTER TABLE asistente.retroalimentacion_turno
            ADD CONSTRAINT retroalimentacion_turno_comentario_longitud
            CHECK (comentario IS NULL OR char_length(comentario) <= 500);
    END IF;
END
$$;

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
    'User feedback (thumbs up/down + zero or more reasons + a bounded comment) for an answered turn, keyed only by asistente.registro_analitico.id. No actor column, on purpose: see the comments above and TD-012.';

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
