-- designaciones.pedido_adjuntos
-- Documentación respaldatoria del trámite. Qué adjuntos son obligatorios depende
-- de la novedad y lo valida el backend, no la base:
--   Alta   -> cv + dni_frente + dni_dorso  [BR-designaciones-002]
--   Baja   -> justificativo                [BR-designaciones-003]
--
-- ON DELETE CASCADE: eliminar un borrador borra sus adjuntos. Es el único borrado
-- físico del módulo — a partir de 'en_revision_*' el pedido ya no se elimina.

CREATE TABLE designaciones.pedido_adjuntos (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    pedido_id   UUID         NOT NULL REFERENCES designaciones.pedidos(id) ON DELETE CASCADE,
    tipo        TEXT         NOT NULL,
    nombre      TEXT         NOT NULL,
    uri         TEXT         NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT pedido_adjuntos_tipo_valido CHECK (tipo IN (
        'cv', 'dni_frente', 'dni_dorso', 'justificativo'))
);

CREATE INDEX pedido_adjuntos_pedido_idx
    ON designaciones.pedido_adjuntos (pedido_id);

SELECT audit.attach('designaciones.pedido_adjuntos');
