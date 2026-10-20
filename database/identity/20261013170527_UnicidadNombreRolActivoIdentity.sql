-- No modifica códigos, permisos ni identidades; preflight bloquea legados duplicados.
LOCK TABLE identity.roles IN SHARE ROW EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM identity.roles WHERE is_active
        GROUP BY lower(btrim(name)) HAVING count(*) > 1) THEN
        RAISE EXCEPTION USING ERRCODE = '23505', CONSTRAINT = 'roles_nombre_activo_normalizado_key',
            MESSAGE = 'Nombres activos duplicados: requiere saneamiento explícito antes del upgrade.';
    END IF;
END $$;
CREATE UNIQUE INDEX roles_nombre_activo_normalizado_key ON identity.roles (lower(btrim(name))) WHERE is_active;
