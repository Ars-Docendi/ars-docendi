CREATE SCHEMA IF NOT EXISTS portal;

CREATE TABLE portal.perfiles (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    persona_id  UUID NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE portal.contactos (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    perfil_id   UUID NOT NULL UNIQUE REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    telefono    TEXT NULL,
    mail        TEXT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE portal.cvs (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    perfil_id   UUID NOT NULL UNIQUE REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    nombre      TEXT NOT NULL,
    fecha_carga TIMESTAMPTZ NOT NULL DEFAULT now(),
    uri         TEXT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT cvs_nombre_no_vacio CHECK (btrim(nombre) <> '')
);

CREATE TABLE portal.experiencias (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    perfil_id    UUID NOT NULL REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    puesto       TEXT NOT NULL,
    organizacion TEXT NOT NULL,
    descripcion  TEXT NOT NULL DEFAULT '',
    desde       DATE NOT NULL,
    hasta       DATE NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT experiencias_periodo_valido CHECK (hasta IS NULL OR hasta >= desde)
);

CREATE TABLE portal.educaciones (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    perfil_id   UUID NOT NULL REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    nivel       TEXT NOT NULL,
    carrera     TEXT NOT NULL,
    institucion TEXT NOT NULL,
    desde       DATE NOT NULL,
    hasta       DATE NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT educaciones_periodo_valido CHECK (hasta IS NULL OR hasta >= desde)
);

CREATE TABLE portal.certificaciones (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    perfil_id    UUID NOT NULL REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    nombre       TEXT NOT NULL,
    emisor       TEXT NOT NULL,
    fecha        DATE NOT NULL,
    vencimiento  DATE NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT certificaciones_vencimiento_valido CHECK (vencimiento IS NULL OR vencimiento >= fecha)
);

CREATE TABLE portal.proyectos (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    perfil_id   UUID NOT NULL REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    nombre      TEXT NOT NULL,
    rol         TEXT NOT NULL,
    descripcion TEXT NOT NULL DEFAULT '',
    desde      DATE NOT NULL,
    hasta      DATE NULL,
    doi         TEXT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT proyectos_periodo_valido CHECK (hasta IS NULL OR hasta >= desde)
);

CREATE TABLE portal.proyecto_documentos (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    proyecto_id UUID NOT NULL UNIQUE REFERENCES portal.proyectos(id) ON DELETE CASCADE,
    nombre      TEXT NOT NULL,
    fecha_carga TIMESTAMPTZ NOT NULL DEFAULT now(),
    uri         TEXT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT proyecto_documentos_nombre_no_vacio CHECK (btrim(nombre) <> '')
);

CREATE TABLE portal.habilidades (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    termino      TEXT NOT NULL,
    termino_norm TEXT NOT NULL UNIQUE,
    sugerido     BOOLEAN NOT NULL DEFAULT FALSE,
    canonica_id  UUID NULL REFERENCES portal.habilidades(id),
    usos         INTEGER NOT NULL DEFAULT 0 CHECK (usos >= 0),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT habilidades_termino_no_vacio CHECK (btrim(termino) <> '')
);

CREATE TABLE portal.docente_habilidades (
    perfil_id    UUID NOT NULL REFERENCES portal.perfiles(id) ON DELETE CASCADE,
    habilidad_id UUID NOT NULL REFERENCES portal.habilidades(id) ON DELETE CASCADE,
    tipo         TEXT NOT NULL CHECK (tipo IN ('habilidad', 'interes')),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (perfil_id, habilidad_id, tipo)
);

SELECT audit.attach('portal.perfiles');
SELECT audit.attach('portal.contactos');
SELECT audit.attach('portal.cvs');
SELECT audit.attach('portal.experiencias');
SELECT audit.attach('portal.educaciones');
SELECT audit.attach('portal.certificaciones');
SELECT audit.attach('portal.proyectos');
SELECT audit.attach('portal.proyecto_documentos');
SELECT audit.attach('portal.habilidades');
SELECT audit.attach('portal.docente_habilidades', 'perfil_id');
