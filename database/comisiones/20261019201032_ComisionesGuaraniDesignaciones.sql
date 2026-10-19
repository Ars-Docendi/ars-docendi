-- 20261019201032_ComisionesGuaraniDesignaciones (Designaciones).
-- No editar después de publicar: crear otra migración incremental.
--
-- Snapshot de comisiones importadas desde SIU-Guaraní (prefijo sga_: datos
-- externos, no editables desde Ars Docendi). Cada importación reemplaza en una
-- transacción las comisiones de UN período lectivo; las tablas hijas caen por
-- cascada y se recalculan completas, por eso no hay vínculos manuales que perder.
--
-- Los vínculos hacia identity son FKs nullable ON DELETE SET NULL: NULL significa
-- "no existe en el sistema" y el snapshot nunca bloquea una operación sobre el
-- catálogo. El documento del docente se usa sólo en memoria para vincular.
--
-- Sólo sga_importaciones se audita: auditar el snapshot volcaría cientos de filas
-- por importación en audit.change_log por datos regenerables desde Guaraní.
--
-- Schema propio `comisiones`, separado de `designaciones`, gestionado por el módulo
-- Designaciones (su migración y su historial viven en designaciones.__EFMigrationsHistory).

CREATE SCHEMA IF NOT EXISTS comisiones;

CREATE TABLE comisiones.sga_importaciones (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    anio_academico integer NOT NULL,
    periodo_lectivo_guarani_id text NOT NULL,
    periodo_lectivo_nombre text NOT NULL,
    usuario_id uuid NULL,
    iniciada_at timestamp with time zone DEFAULT now() NOT NULL,
    finalizada_at timestamp with time zone NULL,
    estado text DEFAULT 'en_curso' NOT NULL,
    cantidad integer NULL,
    docentes_sin_vincular integer NULL,
    carreras_sin_vincular integer NULL,
    causa text NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT sga_importaciones_pkey PRIMARY KEY (id),
    CONSTRAINT sga_importaciones_usuario_fk
        FOREIGN KEY (usuario_id) REFERENCES identity.users(id) ON DELETE SET NULL,
    CONSTRAINT sga_importaciones_estado_valido
        CHECK (estado IN ('en_curso', 'exitosa', 'fallida')),
    CONSTRAINT sga_importaciones_causa_valida
        CHECK (causa IS NULL OR causa IN (
            'timeout', 'autenticacion', 'respuesta_invalida',
            'sin_comisiones', 'no_disponible', 'interrumpida')),
    CONSTRAINT sga_importaciones_cierre_coherente CHECK (
        (estado = 'en_curso' AND finalizada_at IS NULL AND causa IS NULL)
        OR (estado = 'exitosa' AND finalizada_at IS NOT NULL AND causa IS NULL AND cantidad IS NOT NULL)
        OR (estado = 'fallida' AND finalizada_at IS NOT NULL AND causa IS NOT NULL))
);

-- La base decide quién importa: a lo sumo una importación en curso por período.
CREATE UNIQUE INDEX sga_importaciones_una_en_curso
    ON comisiones.sga_importaciones (periodo_lectivo_guarani_id)
    WHERE estado = 'en_curso';
CREATE INDEX sga_importaciones_periodo_idx
    ON comisiones.sga_importaciones (periodo_lectivo_guarani_id, iniciada_at DESC);

CREATE TABLE comisiones.sga_comisiones (
    comision_guarani_id text NOT NULL,
    importacion_id uuid NOT NULL,
    anio_academico integer NOT NULL,
    periodo_lectivo_guarani_id text NOT NULL,
    periodo_lectivo_nombre text NOT NULL,
    nombre text NOT NULL,
    materia_codigo text NOT NULL,
    materia_nombre text NOT NULL,
    materia_id uuid NULL,
    modalidad_codigo text NULL,
    modalidad text NULL,
    ubicacion text NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT sga_comisiones_pkey PRIMARY KEY (comision_guarani_id),
    CONSTRAINT sga_comisiones_importacion_fk
        FOREIGN KEY (importacion_id) REFERENCES comisiones.sga_importaciones(id),
    CONSTRAINT sga_comisiones_materia_fk
        FOREIGN KEY (materia_id) REFERENCES identity.materias(id) ON DELETE SET NULL,
    CONSTRAINT sga_comisiones_nombre_no_vacio CHECK (btrim(nombre) <> ''),
    CONSTRAINT sga_comisiones_materia_no_vacia CHECK (btrim(materia_codigo) <> '' AND btrim(materia_nombre) <> '')
);
CREATE INDEX sga_comisiones_periodo_idx ON comisiones.sga_comisiones (periodo_lectivo_guarani_id);
CREATE INDEX sga_comisiones_importacion_idx ON comisiones.sga_comisiones (importacion_id);
CREATE INDEX sga_comisiones_materia_idx ON comisiones.sga_comisiones (materia_id);

CREATE TABLE comisiones.sga_comision_carreras (
    comision_guarani_id text NOT NULL,
    carrera_codigo text NOT NULL,
    carrera_nombre text NOT NULL,
    carrera_id uuid NULL,
    CONSTRAINT sga_comision_carreras_pkey PRIMARY KEY (comision_guarani_id, carrera_codigo),
    CONSTRAINT sga_comision_carreras_comision_fk
        FOREIGN KEY (comision_guarani_id) REFERENCES comisiones.sga_comisiones(comision_guarani_id) ON DELETE CASCADE,
    CONSTRAINT sga_comision_carreras_carrera_fk
        FOREIGN KEY (carrera_id) REFERENCES identity.carreras(id) ON DELETE SET NULL
);
CREATE INDEX sga_comision_carreras_carrera_idx ON comisiones.sga_comision_carreras (carrera_id);

CREATE TABLE comisiones.sga_comision_docentes (
    comision_guarani_id text NOT NULL,
    orden smallint NOT NULL,
    nombre text NOT NULL,
    responsabilidad text NULL,
    persona_id uuid NULL,
    CONSTRAINT sga_comision_docentes_pkey PRIMARY KEY (comision_guarani_id, orden),
    CONSTRAINT sga_comision_docentes_comision_fk
        FOREIGN KEY (comision_guarani_id) REFERENCES comisiones.sga_comisiones(comision_guarani_id) ON DELETE CASCADE,
    CONSTRAINT sga_comision_docentes_persona_fk
        FOREIGN KEY (persona_id) REFERENCES identity.personas(id) ON DELETE SET NULL,
    CONSTRAINT sga_comision_docentes_orden_valido CHECK (orden >= 1),
    CONSTRAINT sga_comision_docentes_nombre_no_vacio CHECK (btrim(nombre) <> '')
);
CREATE INDEX sga_comision_docentes_persona_idx ON comisiones.sga_comision_docentes (persona_id);

CREATE TABLE comisiones.sga_comision_horarios (
    comision_guarani_id text NOT NULL,
    asignacion_guarani_id text NOT NULL,
    dia smallint NOT NULL,
    desde time without time zone NOT NULL,
    hasta time without time zone NOT NULL,
    aula text NULL,
    CONSTRAINT sga_comision_horarios_pkey PRIMARY KEY (comision_guarani_id, asignacion_guarani_id),
    CONSTRAINT sga_comision_horarios_comision_fk
        FOREIGN KEY (comision_guarani_id) REFERENCES comisiones.sga_comisiones(comision_guarani_id) ON DELETE CASCADE,
    CONSTRAINT sga_comision_horarios_dia_valido CHECK (dia BETWEEN 1 AND 7),
    CONSTRAINT sga_comision_horarios_rango_valido CHECK (desde < hasta)
);
CREATE INDEX sga_comision_horarios_asignacion_idx ON comisiones.sga_comision_horarios (asignacion_guarani_id);

SELECT audit.attach('comisiones.sga_importaciones');
