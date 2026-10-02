-- identity.materias
-- Catalog of subjects (materias) offered within a carrera.
-- Lives in identity (rather than designaciones) because it is a scope target
-- for role assignments — co-locating avoids a cross-schema FK from user_roles.
-- Designaciones soft-references this table by id.

CREATE TABLE identity.materias (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    code        TEXT         NOT NULL,
    name        TEXT         NOT NULL,
    carrera_id  UUID         NOT NULL REFERENCES identity.carreras(id) ON DELETE RESTRICT,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    -- Same materia code can be reused across different carreras, but must be unique within one.
    CONSTRAINT materias_code_unique_per_carrera UNIQUE (carrera_id, code)
);

SELECT audit.attach('identity.materias');