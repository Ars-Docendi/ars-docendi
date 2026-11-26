-- 20261106234552_PermisoExportarLoteIdentityIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
-- Exportar el lote deja de depender del rol: se concede por permiso y, de inicio,
-- sólo a Secretaría Académica. Otros roles lo reciben desde la membresía de roles.
INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000040', 'designaciones.exportar_lote', 'Exportar el lote',
     'Descargar la planilla del lote de designaciones del período activo.')
ON CONFLICT (code) DO UPDATE SET nombre = EXCLUDED.nombre, descripcion = EXCLUDED.descripcion;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM identity.roles r CROSS JOIN identity.permisos p
 WHERE r.code = 'secretaria' AND p.code = 'designaciones.exportar_lote'
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
