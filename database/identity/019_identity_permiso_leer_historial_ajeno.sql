-- Permiso de leer el historial de conversaciones de OTRO actor, para soporte.
--
-- POR QUÉ NO SE REUSÓ NINGÚN PERMISO EXISTENTE
-- `asistente.consultar` admite al asistente; `asistente.ver_consulta` muestra
-- la SQL de la CONSULTA PROPIA de quien la pide. Ninguno de los dos habilita
-- leer la conversación de otra persona, y conflarlos con éste haría que
-- admitir a alguien al asistente —o darle visibilidad de su propia SQL— le
-- abriera sin querer el historial ajeno.
--
-- POR QUÉ NO SE LE CONCEDE A NINGÚN ROL, NI SIQUIERA sys_admin
-- Mismo criterio que `asistente.ver_consulta` en la migración 014: un
-- permiso concedido de arranque es difícil de quitar; uno vacío se concede en
-- treinta segundos desde /membresia-roles cuando el Departamento decide que
-- alguien de soporte lo necesita, y queda registrado quién lo pidió. Éste es
-- más sensible todavía: habilita leer preguntas y consultas de OTRO usuario,
-- no las propias.
--
-- Idempotente: ON CONFLICT DO NOTHING.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000028', 'asistente.leer_historial_ajeno', 'Leer el historial ajeno del asistente', 'Leer, con razón obligatoria y auditoría permanente, el historial de conversaciones de otro usuario con el asistente: preguntas, SQL, resultado y momentos. No habilita ver filas de resultado ni volver a ejecutar la consulta de otra persona.')
ON CONFLICT (code) DO NOTHING;

-- Guarda de sys_admin, igual que en 011 y 014: la decisión es explícita y es
-- NO. Este permiso no habilita administrar nada — habilita leer preguntas y
-- consultas ajenas, que es superficie de soporte, no de administración.
DO $leer_historial_ajeno$
DECLARE
    con_permiso INTEGER;
BEGIN
    SELECT count(*) INTO con_permiso
      FROM identity.rol_permisos rp
      JOIN identity.permisos p ON p.id = rp.permiso_id
     WHERE p.code = 'asistente.leer_historial_ajeno';

    IF con_permiso > 0 THEN
        RAISE NOTICE
            'asistente.leer_historial_ajeno ya está concedido a % rol(es). La migración no lo toca.',
            con_permiso;
    END IF;
END
$leer_historial_ajeno$;
