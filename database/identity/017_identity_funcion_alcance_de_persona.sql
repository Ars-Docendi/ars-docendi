-- ¿El actor del asistente alcanza el perfil de esta persona?
--
-- QUÉ DECIDE
-- El predicado completo de visibilidad del portal, en un solo lugar. Las seis
-- policies de `portal` lo invocan y no repiten la lógica: un predicado escrito
-- seis veces es seis oportunidades de que una quede distinta, y ya pasó una vez en
-- este mismo archivo de policies —la de `habilidades` decía `dh.habilidad_id = id`
-- y Postgres lo resolvió contra `pf.id`, dejando el predicado siempre falso sin un
-- solo error—.
--
-- EL PREDICADO, Y SUS TRES RAMAS
--
--     es mi propio perfil
--     OR (tengo el permiso AND soy de ámbito global)
--     OR (tengo el permiso AND la persona tiene designación vigente en una de mis
--         materias visibles)
--
-- LA PRIMERA RAMA NO PIDE NADA, y es deliberado: mirar lo propio no es un
-- privilegio. El ámbito acota lo ajeno, nunca lo propio — por eso un docente sin
-- designación y sin permiso sigue viendo su propia ficha.
--
-- POR QUÉ LA RAMA GLOBAL VA APARTE, aunque `asistente_materias_visibles()` ya
-- devuelva TODAS las materias para un actor global. Porque el puente es la
-- designación, no la materia: un actor global que dependiera de esa rama vería
-- únicamente a las personas CON designación vigente, y en el padrón sintético son
-- 4 de 15. Secretaría, Administración y Decanato tienen que alcanzar al padrón
-- entero, incluida la gente entre períodos. Sin esta rama, el cambio de ámbito les
-- habría recortado el alcance en silencio.
--
-- POR QUÉ LA DESIGNACIÓN VIGENTE Y NO LA ÚLTIMA
-- Es la lectura literal de «sus profesores asignados», y una decisión tomada con
-- el número a la vista: hoy 11 de 15 personas no tienen designación vigente, así
-- que sólo los roles globales las alcanzan. Tiene filo —un docente entre períodos
-- desaparece de la vista de su jefe justo cuando hay que renovarlo— y se aceptó
-- porque las alternativas (la última designación aunque esté cerrada, o un período
-- de gracia) hacen que el alcance nunca se achique, y un alcance que sólo crece
-- deja de ser un alcance. Ver D2 de `asistente-portal-por-ambito`.
--
-- ESTO ATA PORTAL A `designaciones`, Y ES EL COSTO ACEPTADO
-- La decisión D1 de `asistente-lee-portal` había descartado este puente por eso
-- mismo. Se enmienda con la definición de alcance de Secretaría a la vista: el
-- acoplamiento es real y es lo que permite responder «los docentes de mi cátedra».
--
-- CANAL DE INFERENCIA QUE SE ABRE, NOMBRADO A PROPÓSITO
-- Un actor con el permiso de portal y ámbito de carrera puede deducir, de qué
-- perfiles ve, quiénes tienen designación vigente en su carrera — aunque no tenga
-- `designaciones.ver`. Se acepta: a quien se le concedió leer la trayectoria de
-- los docentes de su carrera, saber cuáles son los docentes de su carrera no le
-- agrega nada. Queda escrito para que sea una decisión y no un hallazgo.
--
-- SECURITY DEFINER con `search_path` vacío, igual que el resto de las funciones de
-- este archivo: corre como el dueño para poder calcular el alcance sin quedar
-- filtrada por la RLS de `designaciones`, que es justamente lo que está midiendo.

CREATE OR REPLACE FUNCTION identity.asistente_alcanza_a(persona UUID)
RETURNS BOOLEAN
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $asistente_alcanza_a$
    SELECT persona = identity.asistente_persona()
        OR (
            identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena')
            AND (
                identity.asistente_es_global()
                OR EXISTS (
                    SELECT 1
                      FROM designaciones.designaciones d
                     WHERE d.persona_id = persona
                       AND d.vigente_hasta IS NULL
                       AND d.materia_id IN (
                           SELECT identity.asistente_materias_visibles())
                )
            )
        );
$asistente_alcanza_a$;

COMMENT ON FUNCTION identity.asistente_alcanza_a(UUID) IS
    'Si el actor del asistente alcanza el perfil de portal de esa persona: es el '
    'suyo, o tiene el permiso y la persona está dentro de su ámbito.';

-- Se revoca de PUBLIC igual que las otras cinco. El GRANT EXECUTE a los dos roles
-- de lectura vive en `database/asistente/001_asistente_grants.sql`, que es la única
-- migración que conoce sus nombres —llevan sufijo de ambiente—.
--
-- Las dos sentencias son una unidad: sin el GRANT, la policy que la usa no devuelve
-- cero filas sino que TIRA «permission denied for function», que es un modo de falla
-- distinto y mucho más ruidoso.
REVOKE EXECUTE ON FUNCTION identity.asistente_alcanza_a(UUID) FROM PUBLIC;
