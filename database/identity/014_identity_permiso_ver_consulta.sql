-- Permiso de ver la consulta que el asistente generó.
--
-- POR QUÉ NO SE REUSÓ `asistente.consultar`
-- Mismo argumento con que se descartó reusar `designaciones.ver` para la admisión:
-- dos decisiones distintas necesitan dos interruptores distintos. Quitarle a
-- alguien la vista de la consulta no puede significar quitarle el asistente.
--
-- POR QUÉ NO SE LE CONCEDE A NINGÚN ROL
-- No es un olvido y no es prudencia genérica. La consulta generada es superficie de
-- DIAGNÓSTICO: su WHERE puede llevar un documento, un legajo o un nombre, así que
-- verla es ver datos que la respuesta redactada no muestra. Quién necesita eso es
-- una decisión del Departamento, no de quien escribe esta migración.
--
-- Un permiso concedido de arranque es difícil de quitar —hay que justificar por qué
-- se saca algo que ya estaba—; uno vacío se concede en treinta segundos desde
-- /membresia-roles cuando alguien lo pide, y queda registrado quién lo pidió.
--
-- Idempotente: ON CONFLICT DO NOTHING.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000022', 'asistente.ver_consulta', 'Ver la consulta del asistente', 'Ver la consulta SQL que el asistente generó para responder. Es superficie de diagnóstico: el filtro de una consulta puede contener datos personales que la respuesta redactada no muestra.')
ON CONFLICT (code) DO NOTHING;

-- Guarda de sys_admin, igual que en 011: `sys_admin` NO hereda permisos nuevos,
-- porque su matriz se sembró en la 008 evaluando el catálogo de permisos de ese
-- momento. Acá la decisión es explícita y es NO: ni siquiera sys_admin lo recibe
-- por default, porque el permiso no habilita administrar nada — habilita ver datos.
DO $ver_consulta$
DECLARE
    con_permiso INTEGER;
BEGIN
    SELECT count(*) INTO con_permiso
      FROM identity.rol_permisos rp
      JOIN identity.permisos p ON p.id = rp.permiso_id
     WHERE p.code = 'asistente.ver_consulta';

    IF con_permiso > 0 THEN
        RAISE NOTICE
            'asistente.ver_consulta ya está concedido a % rol(es). La migración no lo toca.',
            con_permiso;
    END IF;
END
$ver_consulta$;
