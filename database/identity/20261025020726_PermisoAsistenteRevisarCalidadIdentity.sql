-- Permiso de revisar la lista de respuestas con problemas del asistente.
--
-- POR QUÉ NO SE REUSÓ NINGÚN PERMISO EXISTENTE
-- `asistente.administrar` gobierna presupuestos, tope organizacional,
-- mantenimiento y los contadores agregados de calidad. La lista de respuestas
-- con problemas muestra el TEXTO de la pregunta de personas (anónimo, sin
-- actor), que puede contener datos personales tipeados por quien preguntó.
-- Ver un contador y leer preguntas son dos decisiones distintas.
--
-- POR QUÉ NO SE LE CONCEDE A NINGÚN ROL, NI SIQUIERA sys_admin
-- Mismo criterio que `asistente.ver_consulta` y
-- `asistente.leer_historial_ajeno`: un permiso concedido de arranque es difícil
-- de quitar; uno vacío se concede desde /membresia-roles cuando el
-- Departamento decide quién revisa, y cada lectura queda auditada en
-- asistente.auditoria_revision_de_calidad.
--
-- Idempotente: ON CONFLICT DO NOTHING.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000033', 'asistente.revisar_calidad', 'Revisar la calidad de las respuestas del asistente', 'Ver la lista anónima de respuestas con problemas del asistente (pregunta, razón o causa, tipo, modelo, día y estado), con auditoría de cada lectura. No muestra quién preguntó ni el comentario libre del voto.')
ON CONFLICT (code) DO NOTHING;

-- Guarda: la decisión es explícita y es NO. Si algún rol ya lo tiene, no es un
-- error de esta migración, pero queda visible en el log.
DO $revisar_calidad$
DECLARE
    con_permiso INTEGER;
BEGIN
    SELECT count(*) INTO con_permiso
      FROM identity.rol_permisos rp
      JOIN identity.permisos p ON p.id = rp.permiso_id
     WHERE p.code = 'asistente.revisar_calidad';

    IF con_permiso > 0 THEN
        RAISE NOTICE
            'asistente.revisar_calidad ya está concedido a % rol(es). La migración no lo toca.',
            con_permiso;
    END IF;
END
$revisar_calidad$;
