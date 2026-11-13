-- 20261107125238_RlsAsistenteSolicitudesAulas (Aulas).
-- No editar después de publicar: crear otra migración incremental.
--
-- Row Level Security sobre aulas.solicitudes_reserva para la lectura del asistente.
--
-- QUÉ DECIDE ESTA MIGRACIÓN
-- El predicado espeja la pantalla de Aulas y no agrega ni quita nada:
--   * `aulas.aprobar` (secretaria, administrativo, sys_admin) ve TODAS las solicitudes.
--   * `aulas.solicitar` (docente, jefe de cátedra) ve SÓLO las propias: las que tienen
--     docente_id = la persona del actor (ServicioSolicitudesAula).
-- Sin ninguno de los dos permisos, o sin persona vinculada
-- (identity.asistente_persona() NULL), la comparación da NULL y no hay fila visible.
--
-- CONSECUENCIA ACEPTADA, escrita acá para que nadie la «arregle»: no hay ámbito de
-- materia ni de carrera. La pantalla tampoco lo aplica, y un predicado más estricto
-- dejaría al asistente por debajo de ella.
--
-- POR QUÉ `ENABLE` Y NO `FORCE ROW LEVEL SECURITY`
-- Con ENABLE el DUEÑO de la tabla queda exento. La aplicación conecta como el dueño
-- (app_<ambiente>): la pantalla de Aulas y sus notificaciones siguen viendo y
-- escribiendo todas las filas. FORCE las sometería a esta policy FOR SELECT escrita
-- para el actor del asistente.
--
-- A QUIÉN APUNTA
-- Sin cláusula TO ni nombres de rol: los nombres llevan sufijo de ambiente que sólo
-- conoce la configuración del asistente. La restricción la impone el predicado, que
-- falla CERRADO: sin el ajuste `app.asistente_user_id` el permiso da falso.
--
-- ORDEN: corre ANTES del GRANT de database/asistente/001_asistente_grants.sql (el
-- módulo asistente se aplica después). Conceder primero abriría la tabla en la
-- ventana entre los dos merges.

ALTER TABLE aulas.solicitudes_reserva ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS asistente_ve_solicitudes_aula ON aulas.solicitudes_reserva;
CREATE POLICY asistente_ve_solicitudes_aula
    ON aulas.solicitudes_reserva
    FOR SELECT
    USING (
        identity.asistente_tiene_permiso('aulas.aprobar')
        OR (identity.asistente_tiene_permiso('aulas.solicitar')
            AND docente_id = identity.asistente_persona())
    );
