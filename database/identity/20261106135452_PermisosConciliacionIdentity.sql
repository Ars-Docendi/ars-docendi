-- 20261106135452_PermisosConciliacionIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- designaciones.conciliar: ver la conciliación del lote, rechazar pedidos en lote y
-- sincronizarlos con las designaciones vigentes.
-- designaciones.editar: editar un pedido en lote o rechazado en la conciliación.
-- No cambia la edición de borradores y devueltos (BR-designaciones-008).
--
-- Se conceden inicialmente a Secretaría y Administrativo; desde /membresia-roles se
-- asignan a cualquier otro rol sin desplegar. Idempotente.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000038', 'designaciones.conciliar',
     'Conciliar el lote',
     'Ver los pedidos en lote, rechazarlos según la rectificación y sincronizar los aprobados con las designaciones vigentes.'),
    ('b2000000-0000-4000-8000-000000000039', 'designaciones.editar',
     'Editar pedidos en lote',
     'Editar cualquier campo de un pedido en lote o rechazado en la conciliación, antes de sincronizarlo.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  CROSS JOIN identity.permisos p
 WHERE r.code IN ('secretaria', 'administrativo')
   AND p.code IN ('designaciones.conciliar', 'designaciones.editar')
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
