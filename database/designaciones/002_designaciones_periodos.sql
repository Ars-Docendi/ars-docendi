-- designaciones.periodos
-- Ventana de carga de pedidos + rango real de impacto de las designaciones que
-- ese período produce (p. ej. 2do cuatrimestre: se carga en junio, impacta de
-- agosto a diciembre).
--
-- `carga_hasta` es un límite BLANDO: pasada esa fecha se sigue permitiendo
-- cargar, porque el cierre real es manual vía `activo`. La UI lo usa para avisar,
-- no para bloquear.

CREATE TABLE designaciones.periodos (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre         TEXT         NOT NULL,
    carga_desde    DATE         NOT NULL,
    carga_hasta    DATE         NOT NULL,
    impacto_desde  DATE         NOT NULL,
    impacto_hasta  DATE         NOT NULL,
    activo         BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT periodos_carga_coherente   CHECK (carga_hasta   >= carga_desde),
    CONSTRAINT periodos_impacto_coherente CHECK (impacto_hasta >= impacto_desde)
);

-- A lo sumo un período activo a la vez. Índice único parcial sobre una constante:
-- todas las filas con activo = TRUE colisionan entre sí, las demás no participan.
CREATE UNIQUE INDEX periodos_unico_activo
    ON designaciones.periodos ((TRUE))
    WHERE activo;

SELECT audit.attach('designaciones.periodos');
