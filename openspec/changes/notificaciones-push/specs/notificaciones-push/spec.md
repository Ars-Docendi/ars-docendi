## ADDED Requirements

### Requirement: Push subscriptions belong to the session user

The system SHALL store one Web Push subscription per device endpoint (`endpoint`, `p256dh`, `auth`, owner, creation instant). `POST /api/notificaciones/push/suscripciones` SHALL be an upsert by endpoint: when the endpoint already exists it MUST be reassigned to the session user and its keys refreshed. `DELETE /api/notificaciones/push/suscripciones` with `{ endpoint }` MUST remove the row only when it belongs to the session user and MUST answer 204 either way. All routes MUST require an authenticated session.

#### Scenario: A device changes hands

- **GIVEN** a device endpoint subscribed by user A
- **WHEN** user B logs in on that device and subscribes the same endpoint
- **THEN** exactly one row SHALL exist for the endpoint
- **AND** it SHALL belong to user B, so user A no longer receives pushes there

#### Scenario: Deleting another user's subscription

- **GIVEN** a subscription owned by user A
- **WHEN** user B sends `DELETE` with that endpoint
- **THEN** the response SHALL be 204 and the row SHALL remain owned by user A

### Requirement: Endpoint allowlist

A subscription endpoint MUST be an `https` URL, without credentials and on the default port, whose host is `fcm.googleapis.com` or `updates.push.services.mozilla.com`, or ends in `.push.apple.com`, `.notify.windows.com` or `.push.services.mozilla.com`. Any other endpoint MUST be rejected with a 400 validation error and nothing stored. The keys `p256dh` and `auth` are required.

#### Scenario: Unknown host

- **WHEN** a client subscribes `https://evil.example.com/fcm.googleapis.com`, `https://fcm.googleapis.com.evil.com/x` or `http://fcm.googleapis.com/x`
- **THEN** the response SHALL be 400 and no row SHALL be written

#### Scenario: Known push services

- **WHEN** a client subscribes an endpoint on `fcm.googleapis.com`, `web.push.apple.com`, `updates.push.services.mozilla.com` or `wns2-par02p.notify.windows.com`
- **THEN** the response SHALL be 204

### Requirement: Descriptive push content

For every in-app notice the system SHALL send each recipient's subscribed devices a push with the JSON payload `{ titulo, mensaje, url, id }`, where `titulo` and `mensaje` are the notice's real title and message (message truncated to 240 characters, title to 80), `url` is the in-app route of the entity (`/aulas/solicitudes/:id`, `/designaciones/pedidos/:id`, `/tareas/:id`) and `id` is the notice id. The service worker SHALL use the id as notification tag. Titles SHALL name the entity (aula, pedido number or task number) and messages SHALL say what happened. The system MUST NOT log titles or messages.

#### Scenario: Room assigned

- **WHEN** a room is assigned to a teacher's request
- **THEN** her subscribed devices SHALL receive a push titled with the room and a message naming the date, materia and commission
- **AND** `url` SHALL point to the request

### Requirement: Per-type push preferences

`GET /api/notificaciones/push/preferencias` SHALL answer, for the session user, every known event type with its `habilitado` state, all enabled by default. `PUT` with `{ tipos: [{ tipoEvento, habilitado }] }` SHALL persist the given states and MUST reject with 400, writing nothing, a body that contains an unknown event type. The system MUST NOT send a push of a type the recipient disabled; the in-app notice MUST be created regardless.

#### Scenario: Opt-out

- **GIVEN** a user who disabled `TAREA_ASIGNADA`
- **WHEN** a task is assigned to them
- **THEN** the notice SHALL exist in the panel and no push SHALL be sent
- **AND** an `AULA_ASIGNADA` push to the same user SHALL still be sent

#### Scenario: Unknown type

- **WHEN** the body contains `{ "tipoEvento": "INVENTADO", "habilitado": false }`
- **THEN** the response SHALL be 400 and no preference SHALL change

### Requirement: Delivery never blocks the business request

`INotificador` SHALL return once the in-app notices are stored; pushes MUST be delivered by a background worker through a bounded queue, each send with a timeout. A subscription whose push service answers 404 or 410 MUST be deleted. Any other failure MUST be logged with the status code and the push service's reason only and MUST NOT affect other devices or the business flow.

#### Scenario: Slow push service

- **GIVEN** a subscription whose push service never answers
- **WHEN** a notice is emitted
- **THEN** `NotificarAsync` SHALL complete without waiting for the send

#### Scenario: Expired subscription

- **WHEN** the push service answers 410 for a subscription
- **THEN** that subscription SHALL be deleted

### Requirement: VAPID configuration

Web Push SHALL be configured with `Notificaciones:WebPush:PublicKey`, `PrivateKey` and `Subject`. Without keys the feature MUST be disabled: `GET /api/notificaciones/push/clave-publica` and the subscription `POST` answer 404 and nothing is enqueued. The subject MUST be a `mailto:` or `https:` URI on a non-reserved domain; reserved domains (`.local`, `.localhost`, `.test`, `.example`, `.invalid`, `example.com|org|net`) are rejected by Apple with 403. An invalid configuration MUST stop the host at startup in Production; outside Production it MUST disable the feature and log the reason, except a reserved-domain subject, which only logs a warning.

#### Scenario: Reserved domain in Production

- **GIVEN** Production with valid keys and the subject `mailto:ops@ars-docendi.local`
- **WHEN** the host starts
- **THEN** startup SHALL fail naming the setting, without printing the keys

### Requirement: Readable notice titles

The notice builders MUST produce short titles that name the entity: `Aula asignada: {aula}`, `Aula actualizada: {aula}`, `Solicitud de aula rechazada`, `Reserva de aula cancelada`, `Solicitud de aula cancelada`, `Pedido {número} para revisar`, `Pedido {número} rechazado`, `Pedido {número} devuelto`, `Tarea N° {número} asignada`, `Tarea N° {número}: cambio de estado` and `Tarea N° {número}: nuevo comentario`. The message keeps naming the aula, pedido or task.

#### Scenario: Returned order

- **WHEN** a pedido is returned to its Jefe
- **THEN** the title SHALL be `Pedido 2026-9002 devuelto` and the message SHALL contain the comment
