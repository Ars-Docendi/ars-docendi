ALTER TABLE portal.cvs
    ADD COLUMN IF NOT EXISTS archivo_id UUID NULL;
ALTER TABLE portal.proyecto_documentos
    ADD COLUMN IF NOT EXISTS archivo_id UUID NULL;

CREATE INDEX IF NOT EXISTS cvs_archivo_idx ON portal.cvs (archivo_id);
CREATE INDEX IF NOT EXISTS proyecto_documentos_archivo_idx ON portal.proyecto_documentos (archivo_id);
