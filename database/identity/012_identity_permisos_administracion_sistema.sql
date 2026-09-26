-- Permisos de lectura para las vistas operativas de administración del sistema.
INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000023', 'sistema.estado.ver',
     'Ver estado del sistema',
     'Consultar el estado actual de los módulos API y la conectividad de PostgreSQL.'),
    ('b2000000-0000-4000-8000-000000000024', 'auditoria.ver',
     'Consultar auditoría',
     'Consultar eventos de auditoría en modo de solo lectura.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

-- La asignación inicial es explícita: el seed original no vuelve a ejecutarse
-- al migrar instalaciones existentes.
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permisos p ON p.code IN ('sistema.estado.ver', 'auditoria.ver')
WHERE r.code = 'sys_admin'
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
