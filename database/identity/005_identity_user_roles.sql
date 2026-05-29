-- identity.user_roles
-- Junction: which roles each user holds, optionally scoped to a materia and/or carrera.
-- materia_id and carrera_id are both real FKs (identity.materias and identity.carreras
-- live in the same schema). A row with materia_id set MUST also carry carrera_id, because
-- every materia belongs to a carrera — see CHECK user_roles_materia_requires_carrera below.

CREATE TABLE identity.user_roles (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID         NOT NULL REFERENCES identity.users(id)    ON DELETE CASCADE,
    role_id      SMALLINT     NOT NULL REFERENCES identity.roles(id)    ON DELETE RESTRICT,
    materia_id   UUID         NULL     REFERENCES identity.materias(id) ON DELETE RESTRICT,
    carrera_id   UUID         NULL     REFERENCES identity.carreras(id) ON DELETE RESTRICT,
    granted_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
    granted_by   UUID         NULL     REFERENCES identity.users(id),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
    deleted_at   TIMESTAMPTZ  NULL,
    CONSTRAINT user_roles_materia_requires_carrera
        CHECK (materia_id IS NULL OR carrera_id IS NOT NULL)
);

-- Deduplicate live assignments. NULLS NOT DISTINCT (PG15+) treats NULL columns as equal,
-- so duplicate global-role rows (both scope columns NULL) are rejected too. Soft-deleted
-- rows are excluded so a revoked (user, role, scope) tuple can be re-granted later.
CREATE UNIQUE INDEX user_roles_unique_assignment
    ON identity.user_roles (user_id, role_id, materia_id, carrera_id)
    NULLS NOT DISTINCT
    WHERE deleted_at IS NULL;

CREATE INDEX user_roles_user_idx
    ON identity.user_roles (user_id);

CREATE INDEX user_roles_materia_idx
    ON identity.user_roles (materia_id)
    WHERE materia_id IS NOT NULL;

CREATE INDEX user_roles_carrera_idx
    ON identity.user_roles (carrera_id)
    WHERE carrera_id IS NOT NULL;

-- Enforce that the scope columns match the assigned role's declared scope.
-- Not a CHECK because it crosses tables (looks up identity.roles.scope).
CREATE OR REPLACE FUNCTION identity.enforce_role_scope()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    role_scope TEXT;
BEGIN
    SELECT scope INTO role_scope FROM identity.roles WHERE id = NEW.role_id;

    IF role_scope IS NULL THEN
        RAISE EXCEPTION 'unknown role_id %', NEW.role_id;
    END IF;

    IF role_scope = 'global' THEN
        IF NEW.materia_id IS NOT NULL OR NEW.carrera_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is global; materia_id and carrera_id must both be NULL', NEW.role_id;
        END IF;
    ELSIF role_scope = 'materia' THEN
        IF NEW.materia_id IS NULL OR NEW.carrera_id IS NULL THEN
            RAISE EXCEPTION 'role_id % is materia-scoped; materia_id and carrera_id are both required', NEW.role_id;
        END IF;
    ELSIF role_scope = 'carrera' THEN
        IF NEW.carrera_id IS NULL OR NEW.materia_id IS NOT NULL THEN
            RAISE EXCEPTION 'role_id % is carrera-scoped; carrera_id required, materia_id must be NULL', NEW.role_id;
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_user_roles_enforce_scope
BEFORE INSERT OR UPDATE ON identity.user_roles
FOR EACH ROW
EXECUTE FUNCTION identity.enforce_role_scope();

SELECT audit.attach('identity.user_roles');