-- RLS de `portal.proyectos` para el asistente.
--
-- Se abre la tabla (sin `descripcion`) porque el jefe de cátedra que busca quién
-- puede dictar una materia necesita los antecedentes de investigación: nombre,
-- rol, fechas y DOI. Decisión del equipo, 2026-11-07.
--
-- ENABLE y no FORCE, igual que el resto de portal: el dueño de las tablas —la API
-- del portal— no queda sometido a la policy. Las policies acotan únicamente el
-- camino del asistente.
--
-- La policy nombra al actor por sí misma, como las demás de portal: pregunta por
-- el alcance sobre el perfil dueño del proyecto con `identity.asistente_alcanza_a`
-- (perfil propio, o permiso Y ámbito). La columna de la fila externa va calificada.

ALTER TABLE portal.proyectos ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS asistente_ve_proyectos ON portal.proyectos;
CREATE POLICY asistente_ve_proyectos
    ON portal.proyectos
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
              FROM portal.perfiles pf
             WHERE pf.id = portal.proyectos.perfil_id
               AND identity.asistente_alcanza_a(pf.persona_id)
        )
    );
