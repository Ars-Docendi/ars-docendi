-- 20261103190503_CrearSuscripcionesYPreferenciasPushNotificaciones (Notificaciones).
-- Do not edit after publishing: create another incremental migration.
--
-- Change notificaciones-push.
--
-- suscripciones_push: one row per browser/device Web Push subscription. The endpoint (the push
-- service URL) identifies the subscription and is unique: subscribing an endpoint that another
-- user holds REASSIGNS the row to the session user (a device belongs to whoever is logged in).
-- Rows are deleted on unsubscribe, when the push service answers 404/410, and with the user.
--
-- preferencias_push: per-user, per-event-type switch for the PUSH channel only (the in-app
-- notice is always created). A missing row means enabled. tipo_evento has no CHECK on purpose:
-- the closed list lives in Modules.Notificaciones.Contracts (TiposEventoNotificacion) and the
-- API rejects unknown types, so a new event type needs no schema change.
--
-- Like notificaciones.notificaciones, no audit.attach: these are device and UI settings, and the
-- keys of a subscription must not be copied into the audit log.

CREATE TABLE notificaciones.suscripciones_push (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id  UUID         NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    endpoint    TEXT         NOT NULL,
    p256dh      TEXT         NOT NULL,
    auth        TEXT         NOT NULL,
    creada_en   TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT suscripciones_push_endpoint_unico UNIQUE (endpoint),
    CONSTRAINT suscripciones_push_texto_no_vacio CHECK (
        btrim(endpoint) <> '' AND btrim(p256dh) <> '' AND btrim(auth) <> ''),
    CONSTRAINT suscripciones_push_longitudes CHECK (
        length(endpoint) <= 2048 AND length(p256dh) <= 256 AND length(auth) <= 256)
);

-- Fan-out looks up the subscriptions of a set of recipients.
CREATE INDEX suscripciones_push_usuario_idx ON notificaciones.suscripciones_push (usuario_id);

CREATE TABLE notificaciones.preferencias_push (
    usuario_id   UUID         NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    tipo_evento  TEXT         NOT NULL,
    habilitado   BOOLEAN      NOT NULL,
    actualizada_en TIMESTAMPTZ NOT NULL DEFAULT now(),

    PRIMARY KEY (usuario_id, tipo_evento),
    CONSTRAINT preferencias_push_tipo_no_vacio CHECK (btrim(tipo_evento) <> '')
);
