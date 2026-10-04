-- Una persona tiene a lo sumo UNA designación vigente por materia canónica, sin importar el plan.
-- 014 dejó la designación apuntando sólo a la pertenencia materia–plan, y el EXCLUDE quedó por plan.
-- Acá la designación vuelve a guardar la materia canónica, derivada del plan, y el FK compuesto
-- impide que materia_id y materia_plan_id se contradigan. Ver change rediseno-modelo-academico.

-- Requiere identity/014 (unicidad de (id, materia_id) en materias_plan), que corre antes.
ALTER TABLE designaciones.designaciones
    ADD COLUMN materia_id UUID;

UPDATE designaciones.designaciones d
   SET materia_id = mp.materia_id
  FROM identity.materias_plan mp
 WHERE mp.id = d.materia_plan_id;

ALTER TABLE designaciones.designaciones
    DROP CONSTRAINT designaciones_sin_solapamiento,
    DROP CONSTRAINT designaciones_materia_plan_id_fkey,
    ALTER COLUMN materia_id SET NOT NULL,
    ADD CONSTRAINT designaciones_materia_plan_fkey
        FOREIGN KEY (materia_plan_id, materia_id)
        REFERENCES identity.materias_plan (id, materia_id) ON DELETE RESTRICT;

-- Una persona no puede tener dos designaciones solapadas sobre la misma materia canónica.
ALTER TABLE designaciones.designaciones
    ADD CONSTRAINT designaciones_sin_solapamiento
    EXCLUDE USING gist (
        persona_id WITH =,
        materia_id WITH =,
        daterange(vigente_desde, vigente_hasta, '[)') WITH &&
    );

-- "Plantel vigente de la cátedra", por materia canónica.
CREATE INDEX designaciones_materia_vigente_idx
    ON designaciones.designaciones (materia_id)
    WHERE vigente_hasta IS NULL;
