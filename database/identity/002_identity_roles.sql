-- identity.roles
-- Closed catalog of roles. scope declares what kind of organizational target
-- a user_roles row for this role must carry.

CREATE TABLE identity.roles (
    id          SMALLINT     PRIMARY KEY,
    code        TEXT         NOT NULL UNIQUE,
    name        TEXT         NOT NULL,
    scope       TEXT         NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT roles_scope_valid CHECK (scope IN ('global', 'materia', 'carrera'))
);

INSERT INTO identity.roles (id, code, name, scope) VALUES
    (1, 'docente',              'Docente',                   'materia'),
    (2, 'jefe_catedra',         'Jefe de Cátedra',           'materia'),
    (3, 'coordinador_carrera',  'Coordinador de Carrera',    'carrera'),
    (4, 'secretaria',           'Secretaría Académica',      'global'),
    (5, 'decanato',             'Decanato',                  'global'),
    (6, 'administrativo',       'Administrativo',            'global'),
    (7, 'sys_admin',            'Administrador de Sistemas', 'global');

SELECT audit.attach('identity.roles');