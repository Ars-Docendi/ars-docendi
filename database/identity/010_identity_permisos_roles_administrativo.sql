-- Administración puede operar las mismas pantallas de configuración de roles
-- que Secretaría. La navegación y los guards de /roles y /membresia-roles ya
-- exponen ambas superficies a este rol; su matriz debe conceder los permisos
-- que las políticas HTTP exigen.
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
CROSS JOIN identity.permisos p
WHERE r.code = 'administrativo'
  AND p.code IN ('roles.ver', 'roles.administrar', 'roles.gestionar_membresia')
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
