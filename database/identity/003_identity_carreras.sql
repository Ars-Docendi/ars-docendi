-- identity.carreras
-- Catalog of degree programs (e.g., "Ingeniería Informática").
-- Lives in identity (rather than designaciones) because it is a scope target
-- for role assignments — co-locating avoids a cross-schema FK from user_roles.
-- Designaciones soft-references this table by id.

CREATE TABLE identity.carreras (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    code        TEXT         NOT NULL UNIQUE,
    name        TEXT         NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

SELECT audit.attach('identity.carreras');