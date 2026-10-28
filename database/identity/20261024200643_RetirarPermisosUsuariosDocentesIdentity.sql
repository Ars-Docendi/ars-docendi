-- 20261024200643_RetirarPermisosUsuariosDocentesIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- Las pantallas y endpoints de Usuarios y Docentes se reemplazaron por la administración
-- unificada de personas (personas.ver, personas.ver_ambito, personas.administrar,
-- portal.administrar). Se retiran los permisos que ya no usa ningún endpoint, con sus
-- asignaciones.

DELETE FROM identity.rol_permisos
WHERE permiso_id IN (SELECT id FROM identity.permisos
                     WHERE code IN ('usuarios.ver', 'usuarios.administrar', 'docentes.ver'));

DELETE FROM identity.permisos
WHERE code IN ('usuarios.ver', 'usuarios.administrar', 'docentes.ver');
