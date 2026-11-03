-- Las policies de portal pasan a conjugar ÁMBITO.
--
-- QUÉ CAMBIA RESPECTO DE 003
-- El predicado era una disyunción —«es mi perfil O tengo el permiso»— y el ámbito
-- quedaba afuera. Ahora es:
--
--     es mi propio perfil
--     OR (tengo el permiso AND la persona está dentro de mi ámbito)
--
-- POR QUÉ CAMBIA
-- Secretaría definió el alcance que quiere y no es el que se construyó: el jefe de
-- cátedra ve a los designados en sus materias, el coordinador a los de su carrera,
-- y Secretaría, Administración y Decanato a todos. La decisión D1 de
-- `asistente-lee-portal` había dejado el ámbito afuera razonando que «el ámbito no
-- dice nada sobre un dato de persona», y ese argumento valía A FALTA DE UNA
-- DEFINICIÓN DEL CLIENTE. Ahora la hay. Ver `asistente-portal-por-ambito`.
--
-- EL PREDICADO VIVE EN UNA FUNCIÓN, Y ESO SÍ CAMBIA DE CRITERIO
-- 003 repetía el predicado completo en cada policy a propósito, para que ninguna
-- dependiera de otra para leerse. Con el ámbito adentro, ese predicado pasa de dos
-- líneas a diez, y repetirlo seis veces deja de proteger: son seis lugares donde
-- una puede quedar distinta. `identity.asistente_alcanza_a(persona)` lo dice una
-- vez, cada policy sigue leyéndose sola —la invoca por nombre, no depende de otra
-- policy— y hay un solo lugar donde equivocarse.
--
-- Es la misma forma que ya tenían `asistente_tiene_permiso` y `asistente_persona`,
-- que estas policies venían invocando sin que nadie lo objetara.
--
-- LAS TABLAS HIJAS SIGUEN NOMBRANDO AL ACTOR
-- No se apoyan en que la RLS del padre se aplique dentro de su subconsulta. Ver la
-- nota larga de 003: una policy cuya única protección es el comportamiento de otra
-- no se puede leer sola.
--
-- Y CADA REFERENCIA A LA FILA EXTERNA VA CALIFICADA. Dentro del EXISTS, un nombre
-- sin calificar se resuelve primero contra la subconsulta: `dh.habilidad_id = id`
-- se leyó como `dh.habilidad_id = pf.id` y el predicado quedó siempre falso, sin
-- error ni warning. Lo agarraron los tests de comportamiento, no una revisión.
--
-- Idempotente: DROP IF EXISTS + CREATE converge. Re-ejecutar reemplaza las de 003.

-- ------------------------------------------------------------------ el padre

DROP POLICY IF EXISTS asistente_ve_perfiles ON portal.perfiles;
CREATE POLICY asistente_ve_perfiles
    ON portal.perfiles
    FOR SELECT
    USING (identity.asistente_alcanza_a(portal.perfiles.persona_id));

-- ------------------------------------------------------------- la trayectoria

DROP POLICY IF EXISTS asistente_ve_educaciones ON portal.educaciones;
CREATE POLICY asistente_ve_educaciones
    ON portal.educaciones
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
              FROM portal.perfiles pf
             WHERE pf.id = portal.educaciones.perfil_id
               AND identity.asistente_alcanza_a(pf.persona_id)
        )
    );

DROP POLICY IF EXISTS asistente_ve_certificaciones ON portal.certificaciones;
CREATE POLICY asistente_ve_certificaciones
    ON portal.certificaciones
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
              FROM portal.perfiles pf
             WHERE pf.id = portal.certificaciones.perfil_id
               AND identity.asistente_alcanza_a(pf.persona_id)
        )
    );

DROP POLICY IF EXISTS asistente_ve_experiencias ON portal.experiencias;
CREATE POLICY asistente_ve_experiencias
    ON portal.experiencias
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
              FROM portal.perfiles pf
             WHERE pf.id = portal.experiencias.perfil_id
               AND identity.asistente_alcanza_a(pf.persona_id)
        )
    );

DROP POLICY IF EXISTS asistente_ve_docente_habilidades ON portal.docente_habilidades;
CREATE POLICY asistente_ve_docente_habilidades
    ON portal.docente_habilidades
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
              FROM portal.perfiles pf
             WHERE pf.id = portal.docente_habilidades.perfil_id
               AND identity.asistente_alcanza_a(pf.persona_id)
        )
    );

-- ------------------------------------------------------------- el vocabulario

-- Mismo criterio que 003, ahora acotado por ámbito: se ve un término si alguien a
-- quien el actor ALCANZA lo declaró. Para el actor sin permiso eso son sus propias
-- habilidades; para un jefe de cátedra, las de su cátedra.
--
-- El vocabulario se angosta con el ámbito, y es lo correcto: un término raro
-- identifica a una persona por sí solo —«operación de reactor de investigación
-- RA-6»—, así que verlo ya dice que alguien alcanzable lo declaró. Si el término
-- viajara sin acotar, un jefe de cátedra podría enumerar habilidades de docentes
-- que no alcanza.

DROP POLICY IF EXISTS asistente_ve_habilidades ON portal.habilidades;
CREATE POLICY asistente_ve_habilidades
    ON portal.habilidades
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
              FROM portal.docente_habilidades dh
              JOIN portal.perfiles pf ON pf.id = dh.perfil_id
             WHERE dh.habilidad_id = portal.habilidades.id
               AND identity.asistente_alcanza_a(pf.persona_id)
        )
    );
