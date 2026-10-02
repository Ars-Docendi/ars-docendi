-- designaciones.pedido_historial
-- Historial del trámite. Es dato de DOMINIO, no metadata de auditoría, y por eso
-- NO se deriva de audit.change_log:
--
--   1. `rol_id` no es derivable. change_log guarda changed_by (un usuario), pero
--      un usuario puede tener varios roles. Con cuál actuó no lo sabe el log.
--   2. `comentario` lo exige BR-designaciones-005 (justificativo en el rechazo,
--      comentario en la devolución) y la UI del detalle lo muestra.
--   3. change_log.changed_by es NULL-able (queda NULL si el claim no parsea como
--      UUID). Un registro con valor probatorio no lo tolera.
--   4. change_log tiene un índice BRIN sobre changed_at pensado para cortes de
--      retención. El historial de un trámite no se purga nunca.
--
-- Igual hace audit.attach: que alguien edite el historial a mano tiene que dejar
-- rastro. Las dos cosas responden preguntas distintas — el historial dice qué pasó
-- en el trámite, el change_log dice quién tocó qué fila.
--
-- Sin ON DELETE CASCADE contra pedidos: sólo los borradores se borran, y un
-- borrador no tiene historial de revisión que preservar. RESTRICT hace explícito
-- que borrar un pedido con historial es un error, no una limpieza.

CREATE TABLE designaciones.pedido_historial (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    pedido_id   UUID         NOT NULL REFERENCES designaciones.pedidos(id) ON DELETE RESTRICT,
    accion      TEXT         NOT NULL,
    -- Con qué rol actuó, explícito. No se infiere del usuario.
    rol_id      UUID         NOT NULL REFERENCES identity.roles(id)        ON DELETE RESTRICT,
    actor_id    UUID         NULL     REFERENCES identity.users(id)        ON DELETE RESTRICT,
    -- Estado del pedido al momento de registrar el evento.
    etapa       TEXT         NOT NULL,
    -- Justificativo (rechazo) / comentario (devolución) / motivo (prioridad).
    comentario  TEXT         NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT pedido_historial_accion_valida CHECK (accion IN (
        'crear', 'enviar', 'aceptar', 'rechazar', 'devolver', 'reenviar',
        'editar', 'cancelar', 'priorizar', 'despriorizar')),
    CONSTRAINT pedido_historial_etapa_valida CHECK (etapa IN (
        'borrador', 'en_revision_coordinador', 'en_revision_secretaria',
        'en_revision_decanato', 'devuelto', 'en_lote', 'rechazado', 'cancelado'))
);

-- Lectura natural: la línea de tiempo de un pedido, en orden.
CREATE INDEX pedido_historial_pedido_idx
    ON designaciones.pedido_historial (pedido_id, created_at);

SELECT audit.attach('designaciones.pedido_historial');
