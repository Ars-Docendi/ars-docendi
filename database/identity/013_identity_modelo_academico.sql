-- Modelo académico: planes por carrera, materias canónicas y su pertenencia a planes.
--
-- Requiere datos académicos vacíos o recargados desde el modelo nuevo: `materias` pierde
-- `carrera_id`, y la unicidad de `code` pasa a ser global, así que códigos repetidos
-- entre carreras hacen fallar la migración a propósito. Ver change rediseno-modelo-academico.

-- Plan de estudios de una carrera, con vigencia.
CREATE TABLE identity.planes (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    carrera_id  UUID         NOT NULL REFERENCES identity.carreras(id) ON DELETE RESTRICT,
    codigo      TEXT         NOT NULL,
    nombre      TEXT         NOT NULL,
    vigente     BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT planes_codigo_unico_por_carrera UNIQUE (carrera_id, codigo)
);

SELECT audit.attach('identity.planes');

-- Materia canónica: una fila por código, compartida entre carreras y planes.
ALTER TABLE identity.materias
    DROP CONSTRAINT materias_code_unique_per_carrera,
    DROP COLUMN carrera_id,
    ADD CONSTRAINT materias_code_unico UNIQUE (code),
    ADD CONSTRAINT materias_code_formato CHECK (code ~ '^[0-9]{5}$');

-- Pertenencia de una materia a un plan.
CREATE TABLE identity.materias_plan (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    plan_id     UUID         NOT NULL REFERENCES identity.planes(id) ON DELETE RESTRICT,
    materia_id  UUID         NOT NULL REFERENCES identity.materias(id) ON DELETE RESTRICT,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT materias_plan_par_unico UNIQUE (plan_id, materia_id)
);

CREATE INDEX materias_plan_materia_idx ON identity.materias_plan (materia_id);

SELECT audit.attach('identity.materias_plan');

-- Membresías: el docente referencia la materia dentro de un plan (la carrera sale del plan);
-- el jefe de cátedra referencia la materia canónica (sirve en todos sus planes);
-- el coordinador referencia la carrera.
ALTER TABLE identity.user_roles
    DROP CONSTRAINT user_roles_materia_requires_carrera,
    ADD COLUMN materia_plan_id UUID NULL REFERENCES identity.materias_plan(id) ON DELETE RESTRICT;

DROP INDEX identity.user_roles_unique_assignment;

CREATE UNIQUE INDEX user_roles_unique_assignment
    ON identity.user_roles (user_id, role_id, materia_id, materia_plan_id, carrera_id)
    NULLS NOT DISTINCT
    WHERE deleted_at IS NULL;

CREATE INDEX user_roles_materia_plan_idx
    ON identity.user_roles (materia_plan_id)
    WHERE materia_plan_id IS NOT NULL;

-- El scope lo define el código de rol para los roles de sistema, y el campo `scope` para los
-- creados por el operador. Un rol docente nunca lleva carrera: la obtiene de su plan.
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
        IF NEW.materia_plan_id IS NULL OR NEW.materia_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % (docente) exige materia_plan_id y deja materia_id y carrera_id en NULL', NEW.role_id;
        END IF;
    ELSIF role_code = 'jefe_catedra' THEN
        IF NEW.materia_id IS NULL OR NEW.materia_plan_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % (jefe_catedra) exige materia_id y deja materia_plan_id y carrera_id en NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'global' THEN
        IF NEW.materia_id IS NOT NULL OR NEW.materia_plan_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is global; materia_id, materia_plan_id and carrera_id must be NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'materia' THEN
        IF NEW.materia_id IS NULL OR NEW.materia_plan_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is materia-scoped; materia_id required, materia_plan_id and carrera_id NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'carrera' THEN
        IF NEW.carrera_id IS NULL OR NEW.materia_id IS NOT NULL OR NEW.materia_plan_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is carrera-scoped; carrera_id required, materia_id and materia_plan_id NULL', NEW.role_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;
