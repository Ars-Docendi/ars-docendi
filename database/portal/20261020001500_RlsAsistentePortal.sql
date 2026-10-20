-- Row Level Security sobre las tablas de portal que el asistente puede leer.
--
-- QUÉ DECIDE ESTA MIGRACIÓN
-- Quién ve el perfil de quién. El portal está archivado por PERSONA, no por
-- materia ni por carrera, así que el patrón de `designaciones` no se puede copiar:
-- allá el predicado conjuga permiso Y ámbito, y acá el ámbito no significa nada
-- sobre un dato de persona. Que alguien coordine una carrera no dice si puede leer
-- dónde estudió un docente.
--
-- EL PREDICADO ES UNA DISYUNCIÓN, Y SON DOS CASOS DISTINTOS:
--
--     es mi propio perfil       OR   tengo el permiso de leer el ajeno
--
-- El primer disyunto no necesita permiso ninguno: mirar lo propio no es un
-- privilegio. El segundo no necesita ámbito: el permiso ES la frontera.
--
-- POR QUÉ NO SE USA `asistente_es_global()`
-- Porque el ámbito quedó fuera de la decisión. Un Coordinador con el permiso ve
-- todo el padrón y una Secretaría sin él no ve nada: los dos ejes dejaron de
-- coincidir a propósito. Meter el ámbito acá reintroduciría una condición que
-- nadie pidió y que haría el predicado más difícil de explicar que de cumplir.
--
-- POR QUÉ NO SE REUSA `portal.ver`
-- Lo tienen los siete roles y significa «acceder al portal PROPIO». Ver la nota
-- larga de `identity/015`.
--
-- CADA POLICY NOMBRA AL ACTOR, INCLUSO LAS HIJAS
-- Las tablas que cuelgan de `perfiles` podrían apoyarse en que la RLS del padre se
-- aplique dentro de su propia subconsulta, que es lo que hace `designaciones/009`.
-- Acá se escribe el predicado completo igual, y no es redundancia inútil: una
-- policy cuya única protección es el comportamiento de OTRA policy no se puede leer
-- sola, y el día que alguien toque la del padre se lleva puestas cinco sin darse
-- cuenta. La conjunción del mismo predicado consigo mismo es idempotente; el costo
-- es de líneas, no de filas.
--
-- POR QUÉ `ENABLE` Y NO `FORCE`
-- Mismo motivo que en designaciones: con ENABLE el DUEÑO queda exento, y la
-- aplicación conecta como el dueño. Estas policies son FOR SELECT y están escritas
-- para el actor del asistente; FORCE no endurecería nada y dejaría a `ServicioPortal`
-- sin ver los perfiles que administra.
--
-- ESTAS POLICIES NO PROTEGEN LA API REST, Y CONVIENE DECIRLO ACÁ
-- La defensa de los dieciocho endpoints de Portal es `ServicioPortal.PersonaActualAsync`
-- más el filtro por dueño del repositorio, y sigue siendo esa. Lo de acá cubre
-- exclusivamente el camino del asistente, que entra con roles de solo lectura que no
-- son el dueño, no tienen BYPASSRLS y no heredan.
--
-- CADA REFERENCIA A LA FILA EXTERNA VA CALIFICADA, Y NO ES ESTILO
-- Dentro del EXISTS, un nombre sin calificar se resuelve PRIMERO contra la
-- subconsulta. La policy de `habilidades` se escribió como `dh.habilidad_id = id`
-- y Postgres lo leyó como `dh.habilidad_id = pf.id`, porque `pf` también tiene una
-- columna `id`: el predicado quedó siempre falso y nadie veía nada, ni siquiera con
-- el permiso. No hay error ni warning — la consulta es válida y significa otra cosa.
-- Lo agarraron los tests de comportamiento, no una revisión de la SQL.
--
-- Por eso todas dicen `portal.<tabla>.<columna>` aunque sobre en las cuatro donde
-- hoy no hay colisión: la que no colisiona hoy colisiona el día que alguien agregue
-- una columna a `perfiles`.
--
-- QUÉ TABLAS NO APARECEN, Y POR QUÉ
-- `portal.contactos`, `portal.cvs`, `portal.proyectos` y `portal.proyecto_documentos`
-- no llevan policy porque no se le conceden al asistente. Contactos por decisión
-- —el contacto institucional ya sale de identity y el personal no responde ninguna
-- pregunta que aquél no cubra—; las otras tres porque ninguna pregunta del catálogo
-- las necesita, y una tabla expuesta que nadie consulta es prefijo que se paga en
-- cada llamada. Si alguna se abre después, entra con su policy en el mismo cambio.

ALTER TABLE portal.perfiles            ENABLE ROW LEVEL SECURITY;
ALTER TABLE portal.educaciones         ENABLE ROW LEVEL SECURITY;
ALTER TABLE portal.certificaciones     ENABLE ROW LEVEL SECURITY;
ALTER TABLE portal.experiencias        ENABLE ROW LEVEL SECURITY;
ALTER TABLE portal.docente_habilidades ENABLE ROW LEVEL SECURITY;
ALTER TABLE portal.habilidades         ENABLE ROW LEVEL SECURITY;

-- ------------------------------------------------------------------ el padre

DROP POLICY IF EXISTS asistente_ve_perfiles ON portal.perfiles;
CREATE POLICY asistente_ve_perfiles
    ON portal.perfiles
    FOR SELECT
    USING (
        persona_id = identity.asistente_persona()
        OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena')
    );

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
               AND (pf.persona_id = identity.asistente_persona()
                    OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena'))
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
               AND (pf.persona_id = identity.asistente_persona()
                    OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena'))
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
               AND (pf.persona_id = identity.asistente_persona()
                    OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena'))
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
               AND (pf.persona_id = identity.asistente_persona()
                    OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena'))
        )
    );

-- ------------------------------------------------------------- el vocabulario

-- `portal.habilidades` NO ES UN CATÁLOGO CURADO, y tratarlo como tal fue la
-- tentación que este bloque descarta. El término lo tipea el docente en su propio
-- portal: `ServicioPortal.ReemplazarTagsAsync` crea la fila con `sugerido = true` a
-- partir de texto libre del usuario. Un término raro identifica a una persona por sí
-- solo —«operación de reactor de investigación RA-6»— y `usos = 1` lo confirma.
--
-- Por eso se ve un término si alguien a quien el actor ya alcanza lo declaró. Para
-- el actor sin permiso eso son sus propias habilidades, que es coherente: el
-- vocabulario que ve es el suyo. Y no hay fila «huérfana» visible: un término que
-- nadie declaró todavía no le dice nada a nadie.
--
-- La columna `usos` no se concede a ningún rol, aparte de esto. Es un contador
-- AGREGADO sobre todo el padrón, y un agregado no lo puede acotar una policy por
-- fila: la fila es una sola y su número ya vio todo.

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
               AND (pf.persona_id = identity.asistente_persona()
                    OR identity.asistente_tiene_permiso('portal.ver_trayectoria_ajena'))
        )
    );
