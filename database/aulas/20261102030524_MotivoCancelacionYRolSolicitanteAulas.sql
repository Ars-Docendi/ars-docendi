-- 20261102030524_MotivoCancelacionYRolSolicitanteAulas (Aulas).
-- No editar después de publicar: crear otra migración incremental.
--
-- Change reserva-aulas-detalle-y-cancelacion.
--
-- motivo_cancelacion: motivo que un Administrativo ingresa al cancelar una reserva ya
-- aprobada (aprobada -> cancelada). NULL cuando el docente cancela su propia solicitud
-- pendiente: esa cancelación no lleva motivo.
--
-- rol_solicitante: rol activo de la sesión que creó la solicitud (p. ej. 'docente' o
-- 'jefe_catedra'). Lo usarán las notificaciones internas para decidir a qué bandeja
-- deep-linkear. NULL en las filas anteriores a esta migración: no se inventa un backfill.

ALTER TABLE aulas.solicitudes_reserva
    ADD COLUMN motivo_cancelacion TEXT NULL,
    ADD COLUMN rol_solicitante    TEXT NULL;

-- Consistencia del propio circuito: solo una solicitud Cancelada puede tener motivo de
-- cancelación, y si lo tiene no puede estar en blanco.
ALTER TABLE aulas.solicitudes_reserva
    ADD CONSTRAINT solicitudes_reserva_motivo_cancelacion_si_cancelada CHECK (
        motivo_cancelacion IS NULL
        OR (estado = 'cancelada' AND btrim(motivo_cancelacion) <> ''));
