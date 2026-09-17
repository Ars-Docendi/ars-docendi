-- Replay durable de comandos de transición. La clave se acota por actor y ruta;
-- pedido_id + request_hash detectan la reutilización para otra operación.
CREATE TABLE designaciones.idempotencia_comandos (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    clave          UUID         NOT NULL,
    actor_id       UUID         NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    ruta           TEXT         NOT NULL,
    pedido_id      UUID         NOT NULL REFERENCES designaciones.pedidos(id) ON DELETE CASCADE,
    request_hash   TEXT         NOT NULL,
    status_code    INTEGER      NOT NULL,
    response_body  JSONB        NOT NULL,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT idempotencia_status_valido CHECK (status_code BETWEEN 200 AND 599),
    CONSTRAINT idempotencia_actor_ruta_clave UNIQUE (actor_id, ruta, clave)
);

CREATE INDEX idempotencia_expiracion_idx
    ON designaciones.idempotencia_comandos (created_at);
