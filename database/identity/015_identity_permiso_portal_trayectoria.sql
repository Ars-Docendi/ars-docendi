-- Permiso de consultar la trayectoria profesional de OTRO docente.
--
-- POR QUÉ NO SE REUSA `portal.ver`
-- Es la trampa que este archivo existe para cerrar. `portal.ver` lo tienen los
-- SIETE roles —docente incluido, ver 008— y significa «acceder al portal PROPIO».
-- Un predicado de RLS que preguntara por él sería verdadero siempre: no protegería
-- nada, y abriría la formación, la experiencia y las certificaciones de todo el
-- padrón el día que se conceda el primer GRANT sobre `portal`.
--
-- Dos decisiones distintas necesitan dos interruptores distintos. Que alguien
-- pueda cargar su propio perfil no dice nada sobre si puede leer el de otro.
--
-- QUÉ HABILITA, EXACTAMENTE
-- Formación, experiencia laboral, certificaciones y habilidades declaradas de
-- cualquier persona del padrón. NO habilita el contacto personal —`portal.contactos`
-- no se concede a ningún rol— ni el archivo del CV, del que el asistente solo puede
-- decir que existe y de cuándo es.
--
-- POR QUÉ NO SE LE CONCEDE A NINGÚN ROL
-- Mismo criterio que 014, y acá pesa más. El portal docente guarda datos que la
-- persona cargó sobre sí misma, y consultarlos a través del asistente es un cambio
-- de finalidad respecto de aquella para la que se recogieron: hoy están disponibles
-- perfil por perfil y nadie los recorre; con el asistente se contestan cruzados en
-- dos segundos. Quién puede hacer esa pregunta es una decisión del Departamento con
-- respaldo normativo, no de quien escribe esta migración.
--
-- Un permiso concedido de arranque es difícil de quitar; uno vacío se concede en
-- treinta segundos desde /membresia-roles cuando alguien lo pide, y queda
-- registrado quién lo pidió.
--
-- LA FRONTERA ES ADMINISTRATIVA, Y HAY QUE DECIRLO
-- Que el permiso nazca vacío es un DEFAULT, no una barrera. La RLS solo lo hace
-- cumplir. Quién puede otorgarlo y bajo qué criterio es la regla de control real, y
-- vive en `docs/business-rules/portal.md` con su cita normativa.
--
-- Idempotente: ON CONFLICT DO NOTHING.

INSERT INTO identity.permisos (id, code, nombre, descripcion) VALUES
    ('b2000000-0000-4000-8000-000000000023', 'portal.ver_trayectoria_ajena', 'Ver la trayectoria de otros docentes', 'Consultar la formación, la experiencia laboral, las certificaciones y las habilidades declaradas de cualquier docente del padrón. No incluye el contacto personal ni el archivo del CV.')
ON CONFLICT (code) DO NOTHING;

-- Guarda de sys_admin, igual que en 011 y 014: `sys_admin` NO hereda permisos
-- nuevos, y acá la decisión explícita es NO. El permiso no habilita administrar
-- nada: habilita leer datos personales de terceros.
DO $portal_trayectoria$
DECLARE
    con_permiso INTEGER;
BEGIN
    SELECT count(*) INTO con_permiso
      FROM identity.rol_permisos rp
      JOIN identity.permisos p ON p.id = rp.permiso_id
     WHERE p.code = 'portal.ver_trayectoria_ajena';

    IF con_permiso > 0 THEN
        RAISE NOTICE
            'portal.ver_trayectoria_ajena ya está concedido a % rol(es). La migración no lo toca.',
            con_permiso;
    END IF;
END
$portal_trayectoria$;
