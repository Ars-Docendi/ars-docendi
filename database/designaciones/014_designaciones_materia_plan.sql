-- Pedidos y designaciones referencian la pertenencia materia–plan (identity.materias_plan)
-- en lugar de la materia canónica. La carrera sale del plan, así que una materia dictada en
-- dos carreras produce pedidos distintos según el plan elegido. Ver change rediseno-modelo-academico.
--
-- Backfill: cada materia referida debe tener exactamente una pertenencia de plan. Si alguna
-- tiene cero o varias, la migración aborta: elegir el plan correcto es una decisión de datos,
-- no de migración.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM designaciones.pedidos p
        WHERE (SELECT count(*) FROM identity.materias_plan mp WHERE mp.materia_id = p.materia_id) <> 1
    ) OR EXISTS (
        SELECT 1
        FROM designaciones.designaciones d
        WHERE (SELECT count(*) FROM identity.materias_plan mp WHERE mp.materia_id = d.materia_id) <> 1
    ) THEN
        RAISE EXCEPTION 'Hay pedidos o designaciones cuya materia no tiene exactamente una pertenencia de plan; resolver los datos antes de migrar';
    END IF;
END $$;

-- pedidos -----------------------------------------------------------------------------------

ALTER TABLE designaciones.pedidos
    ADD COLUMN materia_plan_id UUID;

UPDATE designaciones.pedidos p
   SET materia_plan_id = mp.id
  FROM identity.materias_plan mp
 WHERE mp.materia_id = p.materia_id;

DROP INDEX designaciones.pedidos_materia_idx;

ALTER TABLE designaciones.pedidos
    DROP CONSTRAINT pedidos_materia_id_fkey,
    DROP COLUMN materia_id,
    ALTER COLUMN materia_plan_id SET NOT NULL,
    ADD CONSTRAINT pedidos_materia_plan_id_fkey
        FOREIGN KEY (materia_plan_id) REFERENCES identity.materias_plan(id) ON DELETE RESTRICT;

-- Guard de ámbito del Jefe de Cátedra y derivación de la carrera vía el plan.
CREATE INDEX pedidos_materia_plan_idx
    ON designaciones.pedidos (materia_plan_id);

-- designaciones -----------------------------------------------------------------------------

ALTER TABLE designaciones.designaciones
    ADD COLUMN materia_plan_id UUID;

UPDATE designaciones.designaciones d
   SET materia_plan_id = mp.id
  FROM identity.materias_plan mp
 WHERE mp.materia_id = d.materia_id;

ALTER TABLE designaciones.designaciones
    DROP CONSTRAINT designaciones_sin_solapamiento,
    DROP CONSTRAINT designaciones_materia_id_fkey;

DROP INDEX designaciones.designaciones_materia_vigente_idx;

ALTER TABLE designaciones.designaciones
    DROP COLUMN materia_id,
    ALTER COLUMN materia_plan_id SET NOT NULL,
    ADD CONSTRAINT designaciones_materia_plan_id_fkey
        FOREIGN KEY (materia_plan_id) REFERENCES identity.materias_plan(id) ON DELETE RESTRICT;

-- Una persona no puede tener dos designaciones solapadas sobre la misma pertenencia de plan.
ALTER TABLE designaciones.designaciones
    ADD CONSTRAINT designaciones_sin_solapamiento
    EXCLUDE USING gist (
        persona_id WITH =,
        materia_plan_id WITH =,
        daterange(vigente_desde, vigente_hasta, '[)') WITH &&
    );

-- "Plantel vigente de la cátedra".
CREATE INDEX designaciones_materia_plan_vigente_idx
    ON designaciones.designaciones (materia_plan_id)
    WHERE vigente_hasta IS NULL;
