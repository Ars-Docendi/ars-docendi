CREATE SCHEMA IF NOT EXISTS tareas;

-- Numeración correlativa legible. Secuencias y no MAX()+1: dos altas simultáneas
-- leerían el mismo máximo. nextval no se revierte con un ROLLBACK: se prefiere un
-- salto a un número reutilizado.
CREATE SEQUENCE tareas.proyectos_numero_seq AS INTEGER START WITH 1;
CREATE SEQUENCE tareas.tareas_numero_seq AS INTEGER START WITH 1;

-- Las personas (responsable, autor, autores de comentario) se guardan como el id
-- del usuario de identity, SIN clave foránea entre schemas: identity es del módulo
-- de administración y los módulos de negocio solo lo leen.
CREATE TABLE tareas.proyectos (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    numero         INTEGER NOT NULL UNIQUE DEFAULT nextval('tareas.proyectos_numero_seq'),
    nombre         TEXT NOT NULL,
    descripcion    TEXT NOT NULL DEFAULT '',
    fecha_inicio   DATE NOT NULL,
    fecha_fin      DATE NOT NULL,
    estado         TEXT NOT NULL DEFAULT 'abierto',
    responsable_id UUID NOT NULL,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT proyectos_nombre_no_vacio CHECK (btrim(nombre) <> ''),
    CONSTRAINT proyectos_estado_valido CHECK (estado IN ('abierto', 'finalizado', 'cancelado')),
    CONSTRAINT proyectos_periodo_valido CHECK (fecha_fin >= fecha_inicio)
);
ALTER SEQUENCE tareas.proyectos_numero_seq OWNED BY tareas.proyectos.numero;

CREATE TABLE tareas.tareas (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    numero            INTEGER NOT NULL UNIQUE DEFAULT nextval('tareas.tareas_numero_seq'),
    titulo            TEXT NOT NULL,
    descripcion       TEXT NOT NULL DEFAULT '',
    fecha_inicio      DATE NOT NULL,
    fecha_fin         DATE NOT NULL,
    prioridad         TEXT NOT NULL,
    tipo              TEXT NOT NULL,
    estado            TEXT NOT NULL DEFAULT 'pendiente',
    porcentaje_avance SMALLINT NOT NULL DEFAULT 0,
    solucion          TEXT NULL,
    responsable_id    UUID NOT NULL,
    creado_por_id     UUID NOT NULL,
    proyecto_id       UUID NULL REFERENCES tareas.proyectos(id),
    tarea_padre_id    UUID NULL REFERENCES tareas.tareas(id),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT tareas_titulo_no_vacio CHECK (btrim(titulo) <> ''),
    CONSTRAINT tareas_prioridad_valida CHECK (prioridad IN ('alta', 'media', 'baja')),
    CONSTRAINT tareas_tipo_valido CHECK (tipo IN
        ('extension', 'administrativa', 'posgrado', 'investigacion', 'academica', 'decanato')),
    CONSTRAINT tareas_estado_valido CHECK (estado IN
        ('pendiente', 'en_curso', 'pausa', 'resuelta', 'cancelada')),
    CONSTRAINT tareas_avance_valido CHECK (porcentaje_avance BETWEEN 0 AND 100),
    CONSTRAINT tareas_periodo_valido CHECK (fecha_fin >= fecha_inicio),
    CONSTRAINT tareas_no_es_su_propio_padre CHECK (tarea_padre_id IS NULL OR tarea_padre_id <> id),
    CONSTRAINT tareas_resuelta_con_solucion CHECK (
        estado <> 'resuelta' OR btrim(COALESCE(solucion, '')) <> '')
);
ALTER SEQUENCE tareas.tareas_numero_seq OWNED BY tareas.tareas.numero;

CREATE INDEX tareas_proyecto_idx ON tareas.tareas (proyecto_id);
CREATE INDEX tareas_padre_idx ON tareas.tareas (tarea_padre_id);
CREATE INDEX tareas_responsable_idx ON tareas.tareas (responsable_id);

-- Relación simple entre tareas (sin jerarquía): una sola fila por par, con
-- tarea_id < relacionada_id. Se lee en ambos sentidos.
CREATE TABLE tareas.tarea_relaciones (
    tarea_id       UUID NOT NULL REFERENCES tareas.tareas(id) ON DELETE CASCADE,
    relacionada_id UUID NOT NULL REFERENCES tareas.tareas(id) ON DELETE CASCADE,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (tarea_id, relacionada_id),
    CONSTRAINT tarea_relaciones_par_canonico CHECK (tarea_id < relacionada_id)
);
CREATE INDEX tarea_relaciones_relacionada_idx ON tareas.tarea_relaciones (relacionada_id);

CREATE TABLE tareas.tarea_comentarios (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tarea_id   UUID NOT NULL REFERENCES tareas.tareas(id) ON DELETE CASCADE,
    autor_id   UUID NOT NULL,
    autor_rol  TEXT NOT NULL,
    texto      TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT tarea_comentarios_texto_no_vacio CHECK (btrim(texto) <> '')
);
CREATE INDEX tarea_comentarios_tarea_idx ON tareas.tarea_comentarios (tarea_id, created_at);

CREATE TABLE tareas.tarea_historial (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tarea_id   UUID NOT NULL REFERENCES tareas.tareas(id) ON DELETE CASCADE,
    accion     TEXT NOT NULL,
    actor_id   UUID NOT NULL,
    actor_rol  TEXT NOT NULL,
    estado     TEXT NOT NULL,
    detalle    TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT tarea_historial_accion_valida CHECK (accion IN
        ('crear', 'cambiar_estado', 'editar_avance', 'editar'))
);
CREATE INDEX tarea_historial_tarea_idx ON tareas.tarea_historial (tarea_id, created_at);

SELECT audit.attach('tareas.proyectos');
SELECT audit.attach('tareas.tareas');
