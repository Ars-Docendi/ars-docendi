-- identity.roles
-- Catálogo de roles. `scope` declara qué tipo de destino organizacional debe
-- llevar una fila de user_roles para este rol (lo valida enforce_role_scope).
--
-- El catálogo NO es cerrado: Secretaría puede crear roles propios para agrupar
-- permisos. Los 7 roles originales llevan es_sistema = TRUE y quedan protegidos
-- por trg_roles_proteger_sistema.
--
-- LÍMITE IMPORTANTE: la máquina de estados del circuito de designaciones resuelve
-- la correspondencia etapa -> rol revisor por `code`, contra los roles de sistema
-- únicamente. Un rol con es_sistema = FALSE agrupa permisos pero NO habilita a
-- aprobar, rechazar ni devolver pedidos en ninguna etapa.

CREATE TABLE identity.roles (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    code        TEXT         NOT NULL UNIQUE,
    name        TEXT         NOT NULL,
    description TEXT         NULL,
    scope       TEXT         NOT NULL,
    es_sistema  BOOLEAN      NOT NULL DEFAULT FALSE,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT roles_scope_valid CHECK (scope IN ('global', 'materia', 'carrera'))
);

-- UUIDs fijos: el seed tiene que ser estable entre ambientes para que las
-- referencias por id (fixtures, seeds de otros módulos) no dependan del orden
-- de inserción.
INSERT INTO identity.roles (id, code, name, scope, es_sistema) VALUES
    ('a1000000-0000-4000-8000-000000000001', 'docente',             'Docente',                   'materia', TRUE),
    ('a1000000-0000-4000-8000-000000000002', 'jefe_catedra',        'Jefe de Cátedra',           'materia', TRUE),
    ('a1000000-0000-4000-8000-000000000003', 'coordinador_carrera', 'Coordinador de Carrera',    'carrera', TRUE),
    ('a1000000-0000-4000-8000-000000000004', 'secretaria',          'Secretaría Académica',      'global',  TRUE),
    ('a1000000-0000-4000-8000-000000000005', 'decanato',            'Decanato',                  'global',  TRUE),
    ('a1000000-0000-4000-8000-000000000006', 'administrativo',      'Administrativo',            'global',  TRUE),
    ('a1000000-0000-4000-8000-000000000007', 'sys_admin',           'Administrador de Sistemas', 'global',  TRUE);

-- Protege el catálogo de sistema. `name` y `description` SÍ son editables (la
-- pantalla /roles ofrece "Editar rol"); `code` y `scope` no, porque son los que
-- la máquina de estados y enforce_role_scope interpretan.
--
-- También impide promover un rol común a rol de sistema: sin ese chequeo, un
-- operador con permiso de editar roles podría fabricarse un rol que participe
-- del circuito de aprobación.
CREATE OR REPLACE FUNCTION identity.proteger_roles_de_sistema()
RETURNS TRIGGER
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
        IF NOT NEW.es_sistema THEN
            RAISE EXCEPTION 'no se puede quitar la marca es_sistema del rol %', OLD.code;
        END IF;
    ELSIF NEW.es_sistema THEN
        RAISE EXCEPTION 'no se puede promover el rol % a rol de sistema', OLD.code;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_roles_proteger_sistema
BEFORE UPDATE OR DELETE ON identity.roles
FOR EACH ROW
EXECUTE FUNCTION identity.proteger_roles_de_sistema();

SELECT audit.attach('identity.roles');
