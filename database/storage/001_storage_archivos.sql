CREATE SCHEMA IF NOT EXISTS storage;

CREATE TABLE IF NOT EXISTS storage.archivos (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    proposito       TEXT NOT NULL,
    ambiente        TEXT NOT NULL,
    bucket          TEXT NOT NULL,
    clave_objeto    TEXT NOT NULL,
    nombre_original TEXT NOT NULL,
    mime_declarado  TEXT NOT NULL,
    mime_detectado  TEXT NULL,
    tamano_bytes    BIGINT NOT NULL,
    sha256          TEXT NULL,
    estado          TEXT NOT NULL,
    propietario_id  UUID NOT NULL,
    expira_en       TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    confirmado_at   TIMESTAMPTZ NULL,
    revisado_at     TIMESTAMPTZ NULL,
    motivo_revision TEXT NULL,
    eliminado_at    TIMESTAMPTZ NULL,
    CONSTRAINT archivos_proposito_valido CHECK (proposito IN ('cv', 'dni_frente', 'dni_dorso', 'justificativo', 'documento_proyecto')),
    CONSTRAINT archivos_estado_valido CHECK (estado IN ('pendiente', 'cuarentena', 'disponible', 'rechazado', 'eliminado')),
    CONSTRAINT archivos_tamano_valido CHECK (tamano_bytes > 0),
    CONSTRAINT archivos_nombre_no_vacio CHECK (btrim(nombre_original) <> ''),
    CONSTRAINT archivos_clave_no_vacia CHECK (btrim(clave_objeto) <> ''),
    CONSTRAINT archivos_ambiente_no_vacio CHECK (btrim(ambiente) <> ''),
    CONSTRAINT archivos_ambiente_clave_unica UNIQUE (ambiente, clave_objeto)
);

CREATE INDEX IF NOT EXISTS archivos_limpieza_idx
    ON storage.archivos (estado, expira_en);
CREATE INDEX IF NOT EXISTS archivos_propietario_idx
    ON storage.archivos (propietario_id, estado);

SELECT audit.attach('storage.archivos');
