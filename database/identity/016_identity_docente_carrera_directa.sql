-- El docente vuelve a referenciar materia_id + carrera_id directos, igual que el Jefe de
-- Cátedra y el Coordinador ya hacían. El plan deja de ser el punto de enganche de las
-- asignaciones: `identity.planes` y `identity.materias_plan` quedan como catálogo informativo
-- (qué materias dicta cada carrera, y si su plan está vigente/activo), sin ninguna FK que
-- dependa de ellos. Ver change rediseno-modelo-academico.
--
-- La ambigüedad de qué plan puntual corresponde a una asignación (una materia puede estar en
-- dos planes viejos de la misma carrera) deja de existir: la asignación sólo necesita saber
-- la carrera, no el plan.

-- El trigger se reemplaza ANTES del backfill: la función vieja exige que un docente tenga
-- materia_plan_id solo y materia_id/carrera_id en NULL, y el UPDATE de abajo hace exactamente
-- lo contrario (deja las tres columnas pobladas a la vez, todavía sin haber borrado
-- materia_plan_id). Si el reemplazo fuera después, ese UPDATE dispara la regla vieja y falla.
CREATE OR REPLACE FUNCTION identity.enforce_role_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    role_code  TEXT;
    role_scope TEXT;
BEGIN
    SELECT code, scope INTO role_code, role_scope FROM identity.roles WHERE id = NEW.role_id;

    IF role_scope IS NULL THEN
        RAISE EXCEPTION 'unknown role_id %', NEW.role_id;
    END IF;

    IF role_code = 'docente' THEN
        IF NEW.materia_id IS NULL OR NEW.carrera_id IS NULL THEN
            RAISE EXCEPTION 'role_id % (docente) exige materia_id y carrera_id', NEW.role_id;
        END IF;
    ELSIF role_code = 'jefe_catedra' THEN
        IF NEW.materia_id IS NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % (jefe_catedra) exige materia_id y deja carrera_id en NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'global' THEN
        IF NEW.materia_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is global; materia_id and carrera_id must be NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'materia' THEN
        IF NEW.materia_id IS NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is materia-scoped; materia_id required, carrera_id NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'carrera' THEN
        IF NEW.carrera_id IS NULL OR NEW.materia_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is carrera-scoped; carrera_id required, materia_id NULL', NEW.role_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

-- Backfill: cada fila de docente conocía su materia y su carrera a través del plan.
UPDATE identity.user_roles ur
   SET materia_id = mp.materia_id,
       carrera_id = pl.carrera_id
  FROM identity.materias_plan mp
  JOIN identity.planes pl ON pl.id = mp.plan_id
 WHERE ur.materia_plan_id = mp.id;

DROP INDEX identity.user_roles_materia_plan_idx;
DROP INDEX identity.user_roles_unique_assignment;

ALTER TABLE identity.user_roles
    DROP COLUMN materia_plan_id;

-- `materias_plan_id_materia_unico` (identity.materias_plan) sigue en pie: la FK compuesta de
-- designaciones.pedidos/designaciones todavía la referencia. La migración
-- 017_designaciones_carrera_directa la borra una vez que dropea esas FKs.

CREATE UNIQUE INDEX user_roles_unique_assignment
    ON identity.user_roles (user_id, role_id, materia_id, carrera_id)
    NULLS NOT DISTINCT
    WHERE deleted_at IS NULL;
