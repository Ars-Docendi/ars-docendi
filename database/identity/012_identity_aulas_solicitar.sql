-- identity.permisos / identity.rol_permisos
-- Agrega el permiso 'aulas.solicitar' (crear y cancelar solicitudes propias de
-- reserva de aula, self-service del Docente) y cierra un gap real de la matriz
-- provisional: el rol 'docente' no tenía ningún permiso de Aulas a pesar de ser
-- quien solicita. Ver openspec/changes/reserva-aulas/design.md (decisión 5).

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000023', 'aulas.solicitar', 'Solicitar reservas de aulas',
     'Crear y cancelar solicitudes propias de reserva de aula o laboratorio para mesas de examen.');

-- docente: no tenía ningún permiso de Aulas. Se agregan ver + solicitar.
-- jefe_catedra: ya tenía aulas.ver; se agrega solicitar (ON CONFLICT evita duplicar aulas.ver).
INSERT INTO identity.rol_permisos (rol_id, permiso_id)
SELECT r.id, p.id
  FROM identity.roles r
  JOIN identity.permisos p ON p.code = ANY (
        CASE r.code
            WHEN 'docente'      THEN ARRAY['aulas.ver', 'aulas.solicitar']
            WHEN 'jefe_catedra' THEN ARRAY['aulas.ver', 'aulas.solicitar']
            ELSE ARRAY[]::TEXT[]
        END)
ON CONFLICT (rol_id, permiso_id) DO NOTHING;
