-- 20261019201032_PermisosComisionesIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- Permisos de la pantalla de comisiones importadas desde Guaraní.
-- `comisiones.ver`: consultar el snapshot. `comisiones.importar`: traer un
-- período desde Guaraní, reservado a Secretaría Académica y al Administrador de
-- Sistemas. Ids -033/-034: -026 a -032 los usa la rama del asistente.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000033', 'comisiones.ver',
     'Ver comisiones',
     'Consultar las comisiones importadas desde Guaraní.'),
    ('b2000000-0000-4000-8000-000000000034', 'comisiones.importar',
     'Importar comisiones',
     'Actualizar desde Guaraní las comisiones de un período lectivo.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permisos p ON p.code = ANY (
    CASE r.code
        WHEN 'jefe_catedra' THEN ARRAY['comisiones.ver']
        WHEN 'coordinador_carrera' THEN ARRAY['comisiones.ver']
        WHEN 'decanato' THEN ARRAY['comisiones.ver']
        WHEN 'secretaria' THEN ARRAY['comisiones.ver', 'comisiones.importar']
        WHEN 'administrativo' THEN ARRAY['comisiones.ver']
        WHEN 'sys_admin' THEN ARRAY['comisiones.ver', 'comisiones.importar']
        ELSE ARRAY[]::TEXT[]
    END)
WHERE r.es_sistema
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
