CREATE SCHEMA IF NOT EXISTS designaciones;

-- designaciones.cargos
-- Catálogo único de cargos docentes. Reemplaza los tres vocabularios que
-- convivían en el frontend:
--   features/designaciones/types.ts   4 valores (abreviados)
--   features/docentes/mock            6 valores (nomenclatura completa)
--   admin-docentes D6 (sólo el texto) 7 valores (menciona "Docente Autorizado",
--                                     que no está en ningún array del código)
--
-- LISTA PROVISIONAL — PENDIENTE DE CONFIRMACIÓN CON EL CLIENTE. La nomenclatura
-- de cargos docentes viene del convenio colectivo y del estatuto UNLaM; no es una
-- decisión de diseño. Se siembra la lista de 6 por ser la más completa que está
-- efectivamente en el código. Corregirla es un INSERT/UPDATE, no una migración.
--
-- `orden` registra la jerarquía institucional (1 = mayor). Este change NO la usa
-- para restringir selecciones — la regla "el cargo sólo puede subir" quedó fuera
-- de alcance en la spec vigente (tema C). Está para no migrar cuando se implemente.

CREATE TABLE designaciones.cargos (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo      TEXT         NOT NULL UNIQUE,
    nombre      TEXT         NOT NULL,
    abreviatura TEXT         NOT NULL,
    orden       SMALLINT     NOT NULL,
    activo      BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT cargos_orden_unico UNIQUE (orden)
);

INSERT INTO designaciones.cargos (id, codigo, nombre, abreviatura, orden) VALUES
    ('c3000000-0000-4000-8000-000000000001', 'titular',   'Profesor Titular',            'Titular',  1),
    ('c3000000-0000-4000-8000-000000000002', 'asociado',  'Profesor Asociado',           'Asociado', 2),
    ('c3000000-0000-4000-8000-000000000003', 'adjunto',   'Profesor Adjunto',            'Adjunto',  3),
    ('c3000000-0000-4000-8000-000000000004', 'jtp',       'Jefe de Trabajos Prácticos',  'JTP',      4),
    ('c3000000-0000-4000-8000-000000000005', 'ayudante1', 'Ayudante de Primera',         'Ay. 1ra',  5),
    ('c3000000-0000-4000-8000-000000000006', 'ayudante2', 'Ayudante de Segunda',         'Ay. 2da',  6);

SELECT audit.attach('designaciones.cargos');
