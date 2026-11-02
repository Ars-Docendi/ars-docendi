-- 20261102181058_PermisosAulasYVocabularioIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- Change catalogo-aulas-y-vocabulario-portal.
--
-- aulas.gestionar estaba "reservado a futuro": pasa a administrar el catálogo de
-- aulas y laboratorios. Sus asignaciones (administrativo, secretaria, sys_admin)
-- no cambian.
UPDATE identity.permisos
   SET nombre = 'Gestionar el catálogo de aulas',
       descripcion = 'Dar de alta, editar, activar, desactivar y eliminar aulas y laboratorios.'
 WHERE code = 'aulas.gestionar';

-- portal.vocabulario.administrar: curar el vocabulario de habilidades e intereses.
-- No se reutiliza portal.administrar: también lo tienen jefe_catedra y decanato,
-- que editan perfiles pero no parametrizan el sistema.
INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000032', 'portal.vocabulario.administrar', 'Administrar el vocabulario de experticia',
     'Incorporar, renombrar y fusionar los términos de habilidades e intereses.')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  JOIN identity.permisos p ON p.code = 'portal.vocabulario.administrar'
 WHERE r.code IN ('secretaria', 'sys_admin')
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
