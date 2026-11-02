-- Coordinación por fila canónica, compartida entre cambios de scope y grants.
LOCK TABLE identity.roles, identity.user_roles IN SHARE ROW EXCLUSIVE MODE;

-- Preflight de asignaciones ya existentes: una migración nueva no debe dar por
-- coherente un legado sólo porque los triggers futuros rechacen nuevas escrituras.
DO $$ BEGIN
    IF EXISTS (
        SELECT 1
        FROM identity.user_roles ur
        JOIN identity.roles r ON r.id = ur.role_id
        WHERE ur.deleted_at IS NULL
          AND NOT (
              (r.code = 'docente' AND ur.materia_id IS NOT NULL AND ur.carrera_id IS NOT NULL)
              OR (r.code = 'jefe_catedra' AND ur.materia_id IS NOT NULL AND ur.carrera_id IS NULL)
              OR (r.scope = 'global' AND ur.materia_id IS NULL AND ur.carrera_id IS NULL)
              OR (r.scope = 'materia' AND ur.materia_id IS NOT NULL AND ur.carrera_id IS NULL)
              OR (r.scope = 'carrera' AND ur.materia_id IS NULL AND ur.carrera_id IS NOT NULL)
          )
    ) THEN
        RAISE EXCEPTION 'identity-scope-incoherente' USING ERRCODE = '23514';
    END IF;
END $$;

CREATE FUNCTION identity.proteger_ambito_con_asignaciones() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.scope IS DISTINCT FROM OLD.scope AND EXISTS (
        SELECT 1 FROM identity.user_roles WHERE role_id = OLD.id AND deleted_at IS NULL
    ) THEN
        RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'roles_scope_asignaciones_vivas',
            MESSAGE = 'No se puede cambiar el ámbito con asignaciones vigentes.';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_roles_proteger_ambito BEFORE UPDATE ON identity.roles
FOR EACH ROW EXECUTE FUNCTION identity.proteger_ambito_con_asignaciones();

CREATE OR REPLACE FUNCTION identity.enforce_role_scope() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    role_code text;
    role_scope text;
BEGIN
    -- Revocar de verdad un legado incoherente es seguro, pero no alterar su contexto.
    IF TG_OP = 'UPDATE' THEN
        IF OLD.deleted_at IS NULL AND NEW.deleted_at IS NOT NULL
            AND (to_jsonb(NEW) - 'deleted_at') = (to_jsonb(OLD) - 'deleted_at') THEN
            RETURN NEW;
        END IF;
        -- Una fila histórica no pierde su ámbito al cambiar el rol.
        IF OLD.deleted_at IS NOT NULL AND NEW.deleted_at IS NOT NULL
            AND (to_jsonb(NEW) - 'deleted_at') = (to_jsonb(OLD) - 'deleted_at') THEN
            RETURN NEW;
        END IF;
    END IF;
    SELECT code, scope INTO role_code, role_scope FROM identity.roles
        WHERE id = NEW.role_id FOR SHARE;
    IF role_scope IS NULL OR NOT (
        CASE
            WHEN role_code = 'docente' THEN NEW.materia_id IS NOT NULL AND NEW.carrera_id IS NOT NULL
            WHEN role_code = 'jefe_catedra' THEN NEW.materia_id IS NOT NULL AND NEW.carrera_id IS NULL
            WHEN role_scope = 'global' THEN NEW.materia_id IS NULL AND NEW.carrera_id IS NULL
            WHEN role_scope = 'materia' THEN NEW.materia_id IS NOT NULL AND NEW.carrera_id IS NULL
            WHEN role_scope = 'carrera' THEN NEW.materia_id IS NULL AND NEW.carrera_id IS NOT NULL
            ELSE false
        END
    ) THEN
        RAISE EXCEPTION USING ERRCODE = '23514', CONSTRAINT = 'user_roles_scope_coherente',
            MESSAGE = 'La asignación no corresponde al ámbito vigente del rol.';
    END IF;
    RETURN NEW;
END $$;
