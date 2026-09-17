-- Wire-up diferido de la auditoría sobre identity.users.
--
-- Existe por una dependencia circular entre schemas:
--   audit.change_log  declara  changed_by REFERENCES identity.users(id)
--   identity.users    necesita audit.attach() para engancharse al log
--
-- El orden de aplicación resuelve el ciclo creando identity.users primero (sin
-- enganche), después el schema audit completo, y recién entonces este archivo.
-- El resto de las tablas de identity llaman a audit.attach en su propio archivo,
-- porque para cuando corren el schema audit ya existe.

SELECT audit.attach('identity.users');
