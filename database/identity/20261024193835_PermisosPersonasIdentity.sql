-- 20261024193835_PermisosPersonasIdentity (Identity).
-- No editar después de publicar: crear otra migración incremental.
--
-- Permisos de la administración unificada de personas (reemplaza Usuarios y Docentes):
--   personas.ver          ver todas las personas;
--   personas.ver_ambito   ver sólo las personas designadas en las materias de jefatura;
--   personas.administrar  crear, editar datos, roles y asignaciones, activar y desactivar;
--   portal.administrar    corregir el Mi Portal de otra persona (acotado al ámbito sin personas.ver).
--
-- Las asignaciones se traducen desde las vigentes, para respetar lo que se haya editado desde
-- Roles; además Decanato pasa a administrar y Jefe de Cátedra a corregir Mi Portal en su ámbito.
-- Los permisos anteriores (usuarios.*, docentes.ver) se retiran en una migración posterior,
-- cuando ya no los use ningún endpoint.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000026', 'personas.ver',
     'Ver personas',
     'Consultar todas las personas con su cuenta, roles, asignaciones y perfil.'),
    ('b2000000-0000-4000-8000-000000000027', 'personas.ver_ambito',
     'Ver personas del ámbito',
     'Consultar las personas designadas en las materias donde el actor es Jefe de Cátedra.'),
    ('b2000000-0000-4000-8000-000000000028', 'personas.administrar',
     'Administrar personas',
     'Crear personas y editar sus datos, roles y asignaciones; activarlas y desactivarlas.'),
    ('b2000000-0000-4000-8000-000000000029', 'portal.administrar',
     'Administrar Mi Portal de otros',
     'Corregir el perfil de Mi Portal de otra persona, dentro del ámbito del actor.')
ON CONFLICT (code) DO UPDATE SET
    nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion;

-- Traducción de las asignaciones existentes.
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT DISTINCT rp.rol_id, nuevo.id
FROM identity.rol_permisos rp
JOIN identity.permisos viejo ON viejo.id = rp.permiso_id
JOIN identity.roles r ON r.id = rp.rol_id
JOIN identity.permisos nuevo ON nuevo.code = ANY (
    CASE
        WHEN viejo.code = 'usuarios.ver' THEN ARRAY['personas.ver']
        WHEN viejo.code = 'usuarios.administrar' THEN ARRAY['personas.administrar', 'portal.administrar']
        WHEN viejo.code = 'docentes.ver' AND r.code = 'jefe_catedra' THEN ARRAY['personas.ver_ambito']
        ELSE ARRAY[]::TEXT[]
    END)
ON CONFLICT (rol_id, permiso_id) DO NOTHING;

-- Decanato administra personas; Jefe de Cátedra corrige Mi Portal en su ámbito.
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
FROM identity.roles r
JOIN identity.permisos p ON p.code = ANY (
    CASE r.code
        WHEN 'decanato' THEN ARRAY['personas.ver', 'personas.administrar', 'portal.administrar']
        WHEN 'jefe_catedra' THEN ARRAY['personas.ver_ambito', 'portal.administrar']
        ELSE ARRAY[]::TEXT[]
    END)
WHERE r.es_sistema
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
