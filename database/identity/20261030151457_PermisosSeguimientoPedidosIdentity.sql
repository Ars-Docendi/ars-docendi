-- 20261030151457_PermisosSeguimientoPedidosIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- designaciones.ver_seguimiento: ver la etapa, el historial y a quién se devolvió un pedido.
-- Sin él, el pedido se ve con un estado simplificado (en proceso, devuelto a uno mismo,
-- aprobado). Se asigna a los roles que hoy ven pedidos, salvo Jefe de Cátedra.
--
-- Se retiran permisos que ningún endpoint ni pantalla usa, con sus asignaciones:
-- portal.editar (el portal propio se edita sin permiso y el ajeno con portal.administrar),
-- reportes.ver y sistema.parametrizar (no existen esas funciones).

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000030', 'designaciones.ver_seguimiento',
     'Ver seguimiento de pedidos',
     'Ver la etapa del circuito, el historial y a quién se devolvió cada pedido.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT DISTINCT rp.rol_id, seguimiento.id
FROM identity.rol_permisos rp
JOIN identity.permisos ver ON ver.id = rp.permiso_id AND ver.code = 'designaciones.ver'
JOIN identity.roles r ON r.id = rp.rol_id AND r.code <> 'jefe_catedra'
CROSS JOIN identity.permisos seguimiento
WHERE seguimiento.code = 'designaciones.ver_seguimiento'
ON CONFLICT (rol_id, permiso_id) DO NOTHING;

DELETE FROM identity.rol_permisos
WHERE permiso_id IN (SELECT id FROM identity.permisos
                     WHERE code IN ('portal.editar', 'reportes.ver', 'sistema.parametrizar'));

DELETE FROM identity.permisos
WHERE code IN ('portal.editar', 'reportes.ver', 'sistema.parametrizar');
