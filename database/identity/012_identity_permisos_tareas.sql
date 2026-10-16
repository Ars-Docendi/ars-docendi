-- Permisos del módulo Tareas.
--
-- La operación de Tareas está disponible para todos los roles de sistema
-- (`tareas.ver`); crear y gestionar tareas y proyectos se controla por permiso.
-- `proyectos.gestionar` es nuevo: crear proyectos y cambiar su estado queda
-- reservado a Decanato y Secretaría Académica, más el Administrador de Sistemas
-- (`sys_admin`), que puede todo y es la máxima jerarquía.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000025', 'proyectos.gestionar',
     'Gestionar proyectos',
     'Crear proyectos de Tareas y cambiar su estado.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permisos p ON p.code = ANY (
    CASE r.code
        WHEN 'docente' THEN ARRAY['tareas.ver']
        WHEN 'jefe_catedra' THEN ARRAY['tareas.ver']
        WHEN 'coordinador_carrera' THEN ARRAY['tareas.ver']
        WHEN 'decanato' THEN ARRAY['tareas.ver', 'tareas.gestionar', 'proyectos.gestionar']
        WHEN 'secretaria' THEN ARRAY['tareas.ver', 'tareas.gestionar', 'proyectos.gestionar']
        WHEN 'administrativo' THEN ARRAY['tareas.ver', 'tareas.gestionar']
        WHEN 'sys_admin' THEN ARRAY['tareas.ver', 'tareas.gestionar', 'proyectos.gestionar']
        ELSE ARRAY[]::TEXT[]
    END)
WHERE r.es_sistema
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
