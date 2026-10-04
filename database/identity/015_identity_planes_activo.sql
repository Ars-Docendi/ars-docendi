-- Plan activo: tiene alumnos y se dicta, aunque no sea vigente (no admite inscripciones nuevas).
-- `vigente` queda como dato informativo; los catálogos de designaciones, membresías y pedidos
-- filtran por `activo`. Los planes existentes quedan activos.

ALTER TABLE identity.planes
    ADD COLUMN activo BOOLEAN NOT NULL DEFAULT TRUE;
