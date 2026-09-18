ALTER TABLE designaciones.pedido_adjuntos
    ADD COLUMN IF NOT EXISTS archivo_id UUID NULL;

CREATE INDEX IF NOT EXISTS pedido_adjuntos_archivo_idx
    ON designaciones.pedido_adjuntos (archivo_id);
