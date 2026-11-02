-- Baseline consolidado: identity/005_identity_integridad.sql
-- DDL final; no contiene transiciones ni backfills de alpha.

CREATE FUNCTION identity.enforce_role_scope() RETURNS trigger
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

CREATE FUNCTION identity.proteger_roles_de_sistema() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.es_sistema THEN
            RAISE EXCEPTION 'el rol de sistema % no se puede eliminar', OLD.code;
        END IF;
        RETURN OLD;
    END IF;

    IF OLD.es_sistema THEN
        IF NEW.code IS DISTINCT FROM OLD.code THEN
            RAISE EXCEPTION 'el code del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.scope IS DISTINCT FROM OLD.scope THEN
            RAISE EXCEPTION 'el scope del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.name IS DISTINCT FROM OLD.name THEN
            RAISE EXCEPTION 'el nombre del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.description IS DISTINCT FROM OLD.description THEN
            RAISE EXCEPTION 'la descripción del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NEW.is_active IS DISTINCT FROM OLD.is_active THEN
            RAISE EXCEPTION 'el estado del rol de sistema % es inmutable', OLD.code;
        END IF;
        IF NOT NEW.es_sistema THEN
            RAISE EXCEPTION 'no se puede quitar la marca es_sistema del rol %', OLD.code;
        END IF;
    ELSIF NEW.es_sistema THEN
        RAISE EXCEPTION 'no se puede promover el rol % a rol de sistema', OLD.code;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_roles_proteger_sistema BEFORE DELETE OR UPDATE ON identity.roles FOR EACH ROW EXECUTE FUNCTION identity.proteger_roles_de_sistema();

CREATE TRIGGER trg_user_roles_enforce_scope BEFORE INSERT OR UPDATE ON identity.user_roles FOR EACH ROW EXECUTE FUNCTION identity.enforce_role_scope();

ALTER TABLE ONLY identity.materias_plan
    ADD CONSTRAINT materias_plan_materia_id_fkey FOREIGN KEY (materia_id) REFERENCES identity.materias(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.materias_plan
    ADD CONSTRAINT materias_plan_plan_id_fkey FOREIGN KEY (plan_id) REFERENCES identity.planes(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.planes
    ADD CONSTRAINT planes_carrera_id_fkey FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.rol_permisos
    ADD CONSTRAINT rol_permisos_permiso_id_fkey FOREIGN KEY (permiso_id) REFERENCES identity.permisos(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.rol_permisos
    ADD CONSTRAINT rol_permisos_rol_id_fkey FOREIGN KEY (rol_id) REFERENCES identity.roles(id) ON DELETE CASCADE;

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT user_roles_carrera_id_fkey FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT user_roles_granted_by_fkey FOREIGN KEY (granted_by) REFERENCES identity.users(id);

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT user_roles_materia_id_fkey FOREIGN KEY (materia_id) REFERENCES identity.materias(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT user_roles_role_id_fkey FOREIGN KEY (role_id) REFERENCES identity.roles(id) ON DELETE RESTRICT;

ALTER TABLE ONLY identity.user_roles
    ADD CONSTRAINT user_roles_user_id_fkey FOREIGN KEY (user_id) REFERENCES identity.users(id) ON DELETE CASCADE;

ALTER TABLE ONLY identity.users
    ADD CONSTRAINT users_persona_id_fkey FOREIGN KEY (persona_id) REFERENCES identity.personas(id) ON DELETE RESTRICT;
