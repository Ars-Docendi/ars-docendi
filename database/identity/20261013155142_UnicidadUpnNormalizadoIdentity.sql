-- Sin actualizar ni fusionar cuentas. El lock evita cambios entre preflight e índice.
LOCK TABLE identity.users IN SHARE ROW EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM identity.users GROUP BY lower(btrim(upn)) HAVING count(*) > 1) THEN
        RAISE EXCEPTION USING ERRCODE = '23505', CONSTRAINT = 'users_upn_normalizado_key',
            MESSAGE = 'UPN normalizados duplicados: requiere saneamiento explícito antes del upgrade.';
    END IF;
END $$;
CREATE UNIQUE INDEX users_upn_normalizado_key ON identity.users (lower(btrim(upn)));
