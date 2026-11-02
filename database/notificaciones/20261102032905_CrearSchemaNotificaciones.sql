-- 20261102032905_CrearSchemaNotificaciones (Notificaciones).
-- Do not edit after publishing: create another incremental migration.
--
-- Change notificaciones-in-app.
--
-- One row per recipient user (fan-out at event time): several Jefes de Cátedra of one
-- materia each get their own row and their own read state. Notifications are never
-- deleted: only leida_en changes after insert.
--
-- destinatario_usuario_id references identity.users: same documented exception as
-- aulas.solicitudes_reserva and designaciones.pedidos (identity is transversal
-- infrastructure, not a business module - amended invariant #4).
--
-- entidad_id has NO foreign key on purpose: it points to tables of three different
-- modules (aula requests, designation requests, tasks) and an FK would couple this module
-- to all of them.
--
-- rol_destino is the profile under which the notice is visible; NULL means any profile.
-- Free text (role codes are data), like aulas.solicitudes_reserva.rol_solicitante.
--
-- No audit.attach, on purpose: the only mutation after insert is leida_en (one audit row
-- per click), the source event is already audited in the emitting module and the message
-- snapshots copy names that the audit would duplicate. See design.md (decision 4).

CREATE SCHEMA IF NOT EXISTS notificaciones;

CREATE TABLE notificaciones.notificaciones (
    id                       UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    destinatario_usuario_id  UUID         NOT NULL REFERENCES identity.users(id) ON DELETE RESTRICT,
    rol_destino              TEXT         NULL,
    tipo_evento              TEXT         NOT NULL,
    tipo_entidad             TEXT         NOT NULL,
    entidad_id               UUID         NOT NULL,
    titulo                   TEXT         NOT NULL,
    mensaje                  TEXT         NOT NULL,
    contexto                 JSONB        NOT NULL DEFAULT '{}'::jsonb,
    leida_en                 TIMESTAMPTZ  NULL,
    creada_en                TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT notificaciones_tipo_entidad_valido CHECK (tipo_entidad IN (
        'SOLICITUD_AULA', 'PEDIDO_DESIGNACION', 'TAREA')),
    CONSTRAINT notificaciones_texto_no_vacio CHECK (
        btrim(tipo_evento) <> '' AND btrim(titulo) <> '' AND btrim(mensaje) <> ''),
    CONSTRAINT notificaciones_rol_destino_no_vacio CHECK (
        rol_destino IS NULL OR btrim(rol_destino) <> ''),
    CONSTRAINT notificaciones_contexto_objeto CHECK (jsonb_typeof(contexto) = 'object')
);

-- Unread count and the two lists (No leídas / Leídas) of a user, newest first.
CREATE INDEX notificaciones_destinatario_lectura_idx
    ON notificaciones.notificaciones (destinatario_usuario_id, leida_en, creada_en DESC);
