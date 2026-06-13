CREATE SCHEMA IF NOT EXISTS identity;

-- identity.users
-- One row per Azure AD principal seen by the system.
-- Stores ONLY the minimum to authenticate/authorize. Docente PII (DNI, teléfono, áreas)
-- lives in portal.docentes and references identity.users.id by soft reference.

CREATE TABLE identity.users (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    azure_oid       UUID         NOT NULL UNIQUE,
    upn             TEXT         NOT NULL UNIQUE,
    display_name    TEXT         NOT NULL,
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    last_login_at   TIMESTAMPTZ  NULL
);

SELECT audit.attach('identity.users');