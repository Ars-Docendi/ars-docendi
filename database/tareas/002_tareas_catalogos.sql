-- Catálogos de Tareas.
--
-- Los estados, prioridades y tipos dejan de ser listas de valores repetidas en CHECKs y
-- DEFAULTs: viven en tablas de catálogo y las tablas de negocio los referencian por FK.
-- Agregar o renombrar un estado es un INSERT/UPDATE de datos, no un cambio de DDL.
-- Las banderas describen el comportamiento que el código necesita conocer sin nombrar
-- estados concretos: cuál es el estado inicial y si un proyecto admite tareas nuevas.

CREATE TABLE tareas.estados_proyecto (
    codigo        TEXT PRIMARY KEY,
    nombre        TEXT NOT NULL,
    verbo         TEXT NOT NULL,
    orden         SMALLINT NOT NULL UNIQUE,
    es_inicial    BOOLEAN NOT NULL DEFAULT FALSE,
    admite_tareas BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE UNIQUE INDEX estados_proyecto_un_inicial ON tareas.estados_proyecto (es_inicial) WHERE es_inicial;

CREATE TABLE tareas.estados_tarea (
    codigo     TEXT PRIMARY KEY,
    nombre     TEXT NOT NULL,
    orden      SMALLINT NOT NULL UNIQUE,
    es_inicial BOOLEAN NOT NULL DEFAULT FALSE
);
CREATE UNIQUE INDEX estados_tarea_un_inicial ON tareas.estados_tarea (es_inicial) WHERE es_inicial;

CREATE TABLE tareas.prioridades (
    codigo TEXT PRIMARY KEY,
    nombre TEXT NOT NULL,
    orden  SMALLINT NOT NULL UNIQUE
);

CREATE TABLE tareas.tipos_tarea (
    codigo TEXT PRIMARY KEY,
    nombre TEXT NOT NULL,
    orden  SMALLINT NOT NULL UNIQUE
);

INSERT INTO tareas.estados_proyecto (codigo, nombre, verbo, orden, es_inicial, admite_tareas) VALUES
    ('abierto', 'Abierto', 'Reabrir', 1, TRUE, TRUE),
    ('finalizado', 'Finalizado', 'Finalizar', 2, FALSE, FALSE),
    ('cancelado', 'Cancelado', 'Cancelar', 3, FALSE, FALSE);

INSERT INTO tareas.estados_tarea (codigo, nombre, orden, es_inicial) VALUES
    ('pendiente', 'Pendiente', 1, TRUE),
    ('en_curso', 'En curso', 2, FALSE),
    ('pausa', 'Pausa', 3, FALSE),
    ('resuelta', 'Resuelta', 4, FALSE),
    ('cancelada', 'Cancelada', 5, FALSE);

INSERT INTO tareas.prioridades (codigo, nombre, orden) VALUES
    ('alta', 'Alta', 1), ('media', 'Media', 2), ('baja', 'Baja', 3);

INSERT INTO tareas.tipos_tarea (codigo, nombre, orden) VALUES
    ('extension', 'Extensión', 1),
    ('administrativa', 'Administrativa', 2),
    ('posgrado', 'Posgrado', 3),
    ('investigacion', 'Investigación', 4),
    ('academica', 'Académicas', 5),
    ('decanato', 'Decanato', 6);

-- Las tablas de negocio pasan de CHECK/DEFAULT con valores fijos a FK contra el catálogo.
ALTER TABLE tareas.proyectos
    DROP CONSTRAINT proyectos_estado_valido,
    ALTER COLUMN estado DROP DEFAULT,
    ADD CONSTRAINT proyectos_estado_fk FOREIGN KEY (estado) REFERENCES tareas.estados_proyecto (codigo);

ALTER TABLE tareas.tareas
    DROP CONSTRAINT tareas_prioridad_valida,
    DROP CONSTRAINT tareas_tipo_valido,
    DROP CONSTRAINT tareas_estado_valido,
    ALTER COLUMN estado DROP DEFAULT,
    ADD CONSTRAINT tareas_prioridad_fk FOREIGN KEY (prioridad) REFERENCES tareas.prioridades (codigo),
    ADD CONSTRAINT tareas_tipo_fk FOREIGN KEY (tipo) REFERENCES tareas.tipos_tarea (codigo),
    ADD CONSTRAINT tareas_estado_fk FOREIGN KEY (estado) REFERENCES tareas.estados_tarea (codigo);
