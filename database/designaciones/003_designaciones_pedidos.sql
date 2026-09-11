-- designaciones.pedidos
-- El trámite. Inmutable en su significado una vez enviado: lo que el pedido dice
-- es lo que decía el día que se firmó, no lo que es cierto hoy (ver `snapshot`).
--
-- FKs cross-schema hacia identity: son la EXCEPCIÓN documentada a la política de
-- data-model.md ("evitar FKs cross-schema, usar soft reference"). Se justifican
-- porque identity NO es un módulo de negocio sino infraestructura transversal que
-- vive en ArsDocendi.Shared y de la que dependen los 4 módulos (invariante #4
-- enmendado), y porque un pedido apuntando a una persona o materia inexistente es
-- un registro legal roto — el costo de la inconsistencia es máximo.
--
-- Un pedido cubre EXACTAMENTE UNA materia: la cátedra sobre la que opera el Jefe
-- de Cátedra. La carrera se deriva de identity.materias.carrera_id y por eso NO
-- se desnormaliza acá (resuelve un único Coordinador competente, BR-designaciones-009).

CREATE TABLE designaciones.pedidos (
    id                    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    numero                TEXT         NOT NULL UNIQUE,
    periodo_id            UUID         NOT NULL REFERENCES designaciones.periodos(id) ON DELETE RESTRICT,
    persona_id            UUID         NOT NULL REFERENCES identity.personas(id)      ON DELETE RESTRICT,
    materia_id            UUID         NOT NULL REFERENCES identity.materias(id)      ON DELETE RESTRICT,

    novedad               TEXT         NOT NULL,
    estado                TEXT         NOT NULL DEFAULT 'borrador',
    prioritario           BOOLEAN      NOT NULL DEFAULT FALSE,

    -- Lo solicitado. NULL en "Sin novedad" y en "Baja" (no piden cambio de cargo).
    cargo_solicitado_id   UUID         NULL     REFERENCES designaciones.cargos(id)   ON DELETE RESTRICT,
    dedicacion_solicitada TEXT         NULL,
    horas                 INTEGER      NULL,

    -- Del docente, no de la materia (así lo define la spec vigente). No son
    -- ambiguas porque BR-designaciones-001 admite un solo pedido vivo por docente
    -- y período, sin importar la cátedra.
    horas_investigacion   INTEGER      NULL,
    horas_externas        INTEGER      NULL,

    justificacion         TEXT         NULL,
    tipo_baja             TEXT         NULL,
    tipo_baja_detalle     TEXT         NULL,

    -- Sólo con estado = 'devuelto': a qué etapa vuelve al reenviar y quién corrige.
    etapa_retorno         TEXT         NULL,
    propietario_actual    TEXT         NULL,

    -- Congelado AL ENVIAR, no al crear: mientras el pedido está en borrador vale
    -- el estado vigente del docente; una vez enviado, el trámite conserva su verdad
    -- histórica aunque la designación cambie mientras recorre la cadena.
    -- Forma: { cargo, dedicacion, horas, materia, horas_investigacion, horas_externas }
    snapshot              JSONB        NULL,

    created_at            TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT pedidos_novedad_valida CHECK (novedad IN (
        'Sin novedad', 'Alta', 'Baja', 'Cambio de cargo o dedicación')),
    CONSTRAINT pedidos_estado_valido CHECK (estado IN (
        'borrador', 'en_revision_coordinador', 'en_revision_secretaria',
        'en_revision_decanato', 'devuelto', 'en_lote', 'rechazado', 'cancelado')),
    -- Escala descendente: Categoría 0 es la de mayor jerarquía. Es un CHECK y no
    -- un catálogo porque, a diferencia de los cargos, la escala es estable y no
    -- tiene tres vocabularios en disputa.
    CONSTRAINT pedidos_dedicacion_valida CHECK (dedicacion_solicitada IS NULL OR dedicacion_solicitada IN (
        'Categoría 0', 'Categoría 1', 'Categoría 2', 'Categoría 3',
        'Categoría 4', 'Categoría 5', 'Categoría 6')),
    CONSTRAINT pedidos_tipo_baja_valido CHECK (tipo_baja IS NULL OR tipo_baja IN (
        'Renuncia', 'Jubilación', 'Otro')),
    CONSTRAINT pedidos_horas_no_negativas CHECK (
        (horas               IS NULL OR horas               >= 0) AND
        (horas_investigacion IS NULL OR horas_investigacion >= 0) AND
        (horas_externas      IS NULL OR horas_externas      >= 0))
);

-- BR-designaciones-001 — un pedido por docente por período, SIN IMPORTAR LA CÁTEDRA.
-- La base es la autoridad: es lo único que sobrevive a dos requests concurrentes.
-- El backend valida antes para dar el mensaje del spec y traduce la violación de
-- este índice al mismo error (defensa en profundidad).
--
-- Los terminales quedan excluidos, así que tras un rechazo o una cancelación se
-- puede volver a presentar. Los borradores se borran físicamente (la spec dice
-- "el pedido deja de existir"), por eso no hace falta contemplar soft-delete.
CREATE UNIQUE INDEX pedidos_uno_por_docente_periodo
    ON designaciones.pedidos (periodo_id, persona_id)
    WHERE estado NOT IN ('rechazado', 'cancelado');

-- Tablero del Coordinador/Secretaría: filtrar por etapa dentro del período.
CREATE INDEX pedidos_periodo_estado_idx
    ON designaciones.pedidos (periodo_id, estado);

-- Guard de ámbito del Jefe de Cátedra y derivación de la carrera vía la materia.
CREATE INDEX pedidos_materia_idx
    ON designaciones.pedidos (materia_id);

CREATE INDEX pedidos_persona_idx
    ON designaciones.pedidos (persona_id);

SELECT audit.attach('designaciones.pedidos');
