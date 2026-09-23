CREATE SCHEMA IF NOT EXISTS aulas;

-- aulas.solicitudes_reserva
-- Primera tabla real del schema `aulas`. Una solicitud de reserva de aula o
-- laboratorio para una mesa de examen. El circuito es de dos pasos sin retorno:
-- `pendiente` -> `aprobada` (Administrativo asigna aula) o `pendiente` -> `cancelada`
-- (Docente dueño). Ninguno de los dos es reversible desde esta pantalla.
--
-- FK cross-schema hacia identity.personas e identity.materias: misma excepción
-- documentada que designaciones.pedidos (identity es infraestructura transversal,
-- no un módulo de negocio — invariante #4 enmendado). identity.materias no es
-- "el catálogo de Designaciones": vive en el schema transversal `identity`
-- (ver data-model.md), así que referenciarlo no acopla Aulas a Designaciones.
--
-- materia_id referencia identity.materias — acotada en el backend a las materias
-- que el docente solicitante tiene asignadas (vía identity.user_roles), no a
-- cualquier materia del catálogo. comision sigue siendo TEXT libre: no hay
-- catálogo de comisiones. Ver openspec/changes/reserva-aulas/design.md (decisión 3,
-- actualizada tras revisión).

CREATE TABLE aulas.solicitudes_reserva (
    id                       UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    docente_id               UUID         NOT NULL REFERENCES identity.personas(id) ON DELETE RESTRICT,

    dia                      DATE         NOT NULL,
    horario_desde            TIME         NOT NULL,
    horario_hasta            TIME         NOT NULL,
    cantidad_alumnos_aprox   INTEGER      NOT NULL,
    materia_id               UUID         NOT NULL REFERENCES identity.materias(id) ON DELETE RESTRICT,
    comision                 TEXT         NOT NULL,

    estado                   TEXT         NOT NULL DEFAULT 'pendiente',
    -- NULL hasta que un Administrativo aprueba. Texto libre: no hay catálogo de
    -- aulas/laboratorios todavía (fuera de alcance, ver design.md).
    aula_asignada            TEXT         NULL,
    -- NULL salvo cuando estado = 'rechazada'. Obligatorio al rechazar (ver design.md, decisión 9).
    motivo_rechazo           TEXT         NULL,

    created_at               TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT solicitudes_reserva_estado_valido CHECK (estado IN (
        'pendiente', 'aprobada', 'rechazada', 'cancelada')),
    CONSTRAINT solicitudes_reserva_horario_valido CHECK (horario_hasta > horario_desde),
    CONSTRAINT solicitudes_reserva_alumnos_valido CHECK (cantidad_alumnos_aprox > 0),
    -- Consistencia del propio circuito: solo una solicitud Aprobada tiene aula.
    CONSTRAINT solicitudes_reserva_aula_si_aprobada CHECK (
        (estado = 'aprobada' AND aula_asignada IS NOT NULL) OR
        (estado <> 'aprobada' AND aula_asignada IS NULL)),
    -- Consistencia del propio circuito: solo una solicitud Rechazada tiene motivo.
    CONSTRAINT solicitudes_reserva_motivo_si_rechazada CHECK (
        (estado = 'rechazada' AND motivo_rechazo IS NOT NULL) OR
        (estado <> 'rechazada' AND motivo_rechazo IS NULL))
);

-- Vista "Mis solicitudes" del Docente.
CREATE INDEX solicitudes_reserva_docente_idx
    ON aulas.solicitudes_reserva (docente_id);

-- Vista "Todas las solicitudes" del Administrativo, y el filtro de Estado.
CREATE INDEX solicitudes_reserva_estado_idx
    ON aulas.solicitudes_reserva (estado);

CREATE INDEX solicitudes_reserva_materia_idx
    ON aulas.solicitudes_reserva (materia_id);

SELECT audit.attach('aulas.solicitudes_reserva');
