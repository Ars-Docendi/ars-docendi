-- 20261107013257_PermisoGestionarPedidosDepartamentoIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- identity.rol_permisos
-- Administrativo y Secretaría cargan pedidos de designación (Alta, Baja y Cambio) de cualquier
-- materia del Departamento, además del Jefe de Cátedra sobre las suyas: reciben
-- 'designaciones.gestionar'. Ver openspec/changes/dedicacion-catalogo-y-externos (decisión 11).

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  JOIN identity.permisos p ON p.code = 'designaciones.gestionar'
 WHERE r.code IN ('administrativo', 'secretaria')
ON CONFLICT DO NOTHING;
