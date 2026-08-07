-- designaciones.designaciones
-- EL ESTADO VIGENTE. La entidad que el sistema no tenía: hasta ahora todo el
-- modelo era el trámite (pedidos), y "qué cargo tiene hoy el docente" lo inventaba
-- el mock del frontend (DocenteExistente.cargoActual / materiasActuales).
--
-- Es la contracara del pedido y quiere propiedades opuestas:
--   pedido       -> inmutable, dice "esto decía cuando se firmó"
--   designación  -> mutable con vigencia, dice "esto es cierto hoy"
--
-- Aprobar un pedido se traduce acá, en una sola transacción:
--   Alta         -> INSERT de una designación nueva
--   Baja         -> UPDATE de vigente_hasta sobre la vigente
--   Cambio       -> cierra la vigente y abre una nueva con lo solicitado
--   Sin novedad  -> no toca nada
--
-- `origen_pedido_id` NULL significa carga administrativa directa (la pantalla
-- /docentes escribe esta misma tabla). Es la columna que hace distinguible una
-- designación producida por el circuito de una cargada a mano — sin ella, la
-- trazabilidad no puede separar los dos caminos de escritura.

-- Requerido por la constraint EXCLUDE de más abajo: permite combinar operadores
-- de igualdad btree (=) con el de solapamiento de rangos (&&) en un índice GiST.
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE designaciones.designaciones (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    persona_id        UUID         NOT NULL REFERENCES identity.personas(id)      ON DELETE RESTRICT,
    materia_id        UUID         NOT NULL REFERENCES identity.materias(id)      ON DELETE RESTRICT,
    cargo_id          UUID         NOT NULL REFERENCES designaciones.cargos(id)   ON DELETE RESTRICT,
    dedicacion        TEXT         NULL,
    horas             INTEGER      NOT NULL,
    vigente_desde     DATE         NOT NULL,
    -- NULL = vigente. Cerrar una designación es fijar esta fecha, no borrar la fila.
    vigente_hasta     DATE         NULL,
    origen_pedido_id  UUID         NULL     REFERENCES designaciones.pedidos(id)  ON DELETE RESTRICT,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT designaciones_vigencia_coherente
        CHECK (vigente_hasta IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT designaciones_horas_positivas
        CHECK (horas > 0),
    CONSTRAINT designaciones_dedicacion_valida CHECK (dedicacion IS NULL OR dedicacion IN (
        'Categoría 0', 'Categoría 1', 'Categoría 2', 'Categoría 3',
        'Categoría 4', 'Categoría 5', 'Categoría 6'))
);

-- Una persona no puede tener dos designaciones solapadas sobre la misma materia.
-- Más fuerte que "a lo sumo una abierta": también rechaza dos cerradas que se
-- pisen, que sería un historial imposible.
--
-- daterange '[)' con vigente_hasta NULL es no acotado por derecha, así que dos
-- designaciones abiertas siempre se solapan y la segunda se rechaza. Una cerrada
-- seguida de una que arranca después no se solapa y se acepta.
ALTER TABLE designaciones.designaciones
    ADD CONSTRAINT designaciones_sin_solapamiento
    EXCLUDE USING gist (
        persona_id WITH =,
        materia_id WITH =,
        daterange(vigente_desde, vigente_hasta, '[)') WITH &&
    );

-- "Estado vigente del docente": alimenta el panel de datos actuales del form.
CREATE INDEX designaciones_persona_vigente_idx
    ON designaciones.designaciones (persona_id)
    WHERE vigente_hasta IS NULL;

-- "Plantel vigente de la cátedra".
CREATE INDEX designaciones_materia_vigente_idx
    ON designaciones.designaciones (materia_id)
    WHERE vigente_hasta IS NULL;

-- Trazabilidad inversa: de un pedido aprobado a lo que produjo.
CREATE INDEX designaciones_origen_pedido_idx
    ON designaciones.designaciones (origen_pedido_id)
    WHERE origen_pedido_id IS NOT NULL;

SELECT audit.attach('designaciones.designaciones');
