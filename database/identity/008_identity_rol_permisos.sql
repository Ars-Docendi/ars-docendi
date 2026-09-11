-- identity.rol_permisos
-- Membresía rol -> permiso. Es la parte EDITABLE del modelo de autorización:
-- el catálogo de roles de sistema y el de permisos están cerrados, pero qué
-- permisos tiene cada rol lo gestiona Secretaría desde /roles.

CREATE TABLE identity.rol_permisos (
    rol_id      UUID         NOT NULL REFERENCES identity.roles(id)    ON DELETE CASCADE,
    permiso_id  UUID         NOT NULL REFERENCES identity.permisos(id) ON DELETE RESTRICT,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    PRIMARY KEY (rol_id, permiso_id)
);

CREATE INDEX rol_permisos_permiso_idx
    ON identity.rol_permisos (permiso_id);

-- Matriz inicial PROVISIONAL, derivada de las responsabilidades de cada rol
-- documentadas en AGENTS.md. NO se seedea desde el antiguo mock del frontend
-- de membresías del frontend: esa matriz era de relleno y
-- asigna, por ejemplo, "Aprobar designaciones — Decanato" al rol Docente.
--
-- PENDIENTE DE CONFIRMACIÓN CON EL CLIENTE. Un sistema que arranca sin ninguna
-- membresía queda inoperable, así que se siembra un default defendible; la
-- matriz definitiva se ajusta desde /roles sin migración.
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  JOIN identity.permisos p ON p.code = ANY (
        CASE r.code
            WHEN 'docente' THEN ARRAY[
                'portal.ver', 'portal.editar']
            WHEN 'jefe_catedra' THEN ARRAY[
                'portal.ver', 'portal.editar',
                'designaciones.ver', 'designaciones.gestionar',
                'aulas.ver', 'docentes.ver']
            WHEN 'coordinador_carrera' THEN ARRAY[
                'portal.ver', 'portal.editar',
                'designaciones.ver', 'designaciones.aprobar_coordinacion',
                'aulas.ver', 'reportes.ver', 'designaciones.revisar']
            WHEN 'secretaria' THEN ARRAY[
                'portal.ver', 'portal.editar',
                'designaciones.ver', 'designaciones.aprobar_secretaria',
                'aulas.ver', 'aulas.gestionar', 'aulas.aprobar',
                'usuarios.ver', 'usuarios.administrar',
                'roles.ver', 'roles.administrar', 'roles.gestionar_membresia',
                'periodos.administrar', 'sistema.parametrizar',
                'tareas.ver', 'tareas.gestionar', 'reportes.ver',
                'designaciones.revisar', 'docentes.ver']
            WHEN 'decanato' THEN ARRAY[
                'portal.ver', 'portal.editar',
                'designaciones.ver', 'designaciones.aprobar_decanato', 'designaciones.revisar',
                'reportes.ver']
            WHEN 'administrativo' THEN ARRAY[
                'portal.ver', 'portal.editar',
                'designaciones.ver',
                'aulas.ver', 'aulas.gestionar', 'aulas.aprobar',
                'usuarios.ver', 'usuarios.administrar',
                'tareas.ver', 'tareas.gestionar', 'designaciones.revisar', 'docentes.ver']
            WHEN 'sys_admin' THEN ARRAY(SELECT code FROM identity.permisos)
            ELSE ARRAY[]::TEXT[]
        END);

SELECT audit.attach('identity.rol_permisos', 'rol_id');
