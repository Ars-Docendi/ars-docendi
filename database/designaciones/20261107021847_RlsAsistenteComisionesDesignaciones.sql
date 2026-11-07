-- 20261107021847_RlsAsistenteComisionesDesignaciones (Designaciones).
-- No editar después de publicar: crear otra migración incremental.
--
-- Row Level Security sobre las cuatro tablas de comisiones que el asistente lee.
--
-- QUÉ DECIDE ESTA MIGRACIÓN
-- El predicado es el PERMISO y nada más: `comisiones.ver`, sin ámbito. La pantalla
-- de Comisiones muestra todas las comisiones a quien tiene ese permiso, sea cual
-- sea su materia o carrera, y el asistente no debe saber ni más ni menos que la
-- pantalla. Los seis roles que usan el asistente lo tienen; `docente` no.
--
-- CONSECUENCIA ACEPTADA, y escrita acá para que nadie la «arregle»: un jefe de
-- cátedra o un coordinador ve por el asistente comisiones fuera de su materia o
-- carrera, exactamente como las ve en la pantalla. Es una diferencia deliberada con
-- designaciones, donde el predicado conjunta permiso Y alcance. No importar
-- identity.asistente_alcanza_ambito() acá: restringiría al asistente por debajo de
-- la pantalla.
--
-- CADA POLICY NOMBRA AL ACTOR POR SÍ MISMA, incluso las tablas hijas. Una policy que
-- se apoyara en la de otra tabla no se puede leer sola, y deja de proteger en cuanto
-- alguien toca la otra. Como no hay referencia a la fila de afuera, no aparece la
-- trampa de la columna calificada que sí hay en portal.
--
-- POR QUÉ `ENABLE` Y NO `FORCE ROW LEVEL SECURITY`
-- Con ENABLE el DUEÑO de la tabla queda exento. La aplicación conecta como el dueño
-- (app_<ambiente>): la pantalla de Comisiones y el importador siguen viendo y
-- escribiendo todas las filas. FORCE los sometería a estas policies, que son FOR
-- SELECT y están escritas para el actor del asistente: no endurece nada, tira el
-- backend entero.
--
-- A QUIÉN APUNTAN
-- Sin cláusula TO y sin nombrar roles: este archivo es del módulo Designaciones y
-- los nombres de rol llevan sufijo de ambiente que sólo conoce la configuración del
-- asistente. La restricción la impone el predicado, que falla CERRADO: sin el ajuste
-- `app.asistente_user_id`, identity.asistente_actor() devuelve NULL, el permiso da
-- falso y no hay fila visible. Un rol que no sea el dueño y no fije actor no ve nada.
--
-- ORDEN: esta migración corre ANTES del GRANT de database/asistente/001_asistente_grants.sql
-- (el módulo asistente se aplica después de Designaciones). Conceder primero abriría
-- el snapshot en la ventana entre los dos merges.

ALTER TABLE comisiones.sga_comisiones         ENABLE ROW LEVEL SECURITY;
ALTER TABLE comisiones.sga_comision_carreras  ENABLE ROW LEVEL SECURITY;
ALTER TABLE comisiones.sga_comision_docentes  ENABLE ROW LEVEL SECURITY;
ALTER TABLE comisiones.sga_comision_horarios  ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS asistente_ve_comisiones ON comisiones.sga_comisiones;
CREATE POLICY asistente_ve_comisiones
    ON comisiones.sga_comisiones
    FOR SELECT
    USING (identity.asistente_tiene_permiso('comisiones.ver'));

DROP POLICY IF EXISTS asistente_ve_comision_carreras ON comisiones.sga_comision_carreras;
CREATE POLICY asistente_ve_comision_carreras
    ON comisiones.sga_comision_carreras
    FOR SELECT
    USING (identity.asistente_tiene_permiso('comisiones.ver'));

DROP POLICY IF EXISTS asistente_ve_comision_docentes ON comisiones.sga_comision_docentes;
CREATE POLICY asistente_ve_comision_docentes
    ON comisiones.sga_comision_docentes
    FOR SELECT
    USING (identity.asistente_tiene_permiso('comisiones.ver'));

DROP POLICY IF EXISTS asistente_ve_comision_horarios ON comisiones.sga_comision_horarios;
CREATE POLICY asistente_ve_comision_horarios
    ON comisiones.sga_comision_horarios
    FOR SELECT
    USING (identity.asistente_tiene_permiso('comisiones.ver'));
