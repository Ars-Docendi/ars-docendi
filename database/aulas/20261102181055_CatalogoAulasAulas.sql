-- 20261102181055_CatalogoAulasAulas (Aulas).
-- No editar después de publicar: crear otra migración incremental.
--
-- Change catalogo-aulas-y-vocabulario-portal.
--
-- aulas.aulas: catálogo de aulas y laboratorios que administra quien tiene
-- `aulas.gestionar`. La asignación de una reserva pasa de texto libre a una FK
-- (`aula_id`). No hay reservas aprobadas en ningún ambiente: si apareciera una,
-- el CHECK nuevo hace fallar la migración en lugar de inventar un aula.

CREATE EXTENSION IF NOT EXISTS btree_gist WITH SCHEMA public;

CREATE TABLE aulas.aulas (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre        TEXT         NOT NULL,
    tipo          TEXT         NOT NULL,
    capacidad     INTEGER      NOT NULL,
    equipamiento  TEXT         NULL,
    activa        BOOLEAN      NOT NULL DEFAULT true,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT aulas_nombre_no_vacio CHECK (btrim(nombre) <> ''),
    CONSTRAINT aulas_tipo_valido CHECK (tipo IN ('aula', 'laboratorio')),
    CONSTRAINT aulas_capacidad_valida CHECK (capacidad > 0)
);

-- Nombre único sin distinguir mayúsculas ni espacios extremos.
CREATE UNIQUE INDEX aulas_nombre_unico ON aulas.aulas (lower(btrim(nombre)));

SELECT audit.attach('aulas.aulas');

ALTER TABLE aulas.solicitudes_reserva
    DROP CONSTRAINT solicitudes_reserva_aula_si_aprobada,
    DROP COLUMN aula_asignada,
    ADD COLUMN aula_id UUID NULL REFERENCES aulas.aulas(id) ON DELETE RESTRICT;

-- Consistencia del propio circuito: sólo una solicitud Aprobada tiene aula.
ALTER TABLE aulas.solicitudes_reserva
    ADD CONSTRAINT solicitudes_reserva_aula_si_aprobada CHECK (
        (estado = 'aprobada') = (aula_id IS NOT NULL));

-- BR-aulas-005: un aula no tiene dos reservas aprobadas superpuestas. Intervalo
-- semiabierto: una reserva que termina 10:00 no choca con otra que empieza 10:00.
ALTER TABLE aulas.solicitudes_reserva
    ADD CONSTRAINT solicitudes_reserva_aula_sin_superposicion EXCLUDE USING gist (
        aula_id WITH =,
        tsrange(dia + horario_desde, dia + horario_hasta, '[)') WITH &&)
    WHERE (estado = 'aprobada');
