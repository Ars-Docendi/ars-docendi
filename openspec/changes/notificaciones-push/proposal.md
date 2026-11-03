# Proposal

## Why

In-app notifications only help while the app is open. Teachers, Jefes and Secretaría work from their phones: an assigned room, a returned order or a reassigned task must reach the lock screen. A throwaway spike proved Web Push end to end on a real iPhone (installed PWA, Apple's push service), but it sent a content-free wake-up, blocked the business request up to 10 s on a stale endpoint, had no way to choose what arrives and an unfinished UI.

## What Changes

- **Web Push subscriptions**: one row per user and device (`notificaciones.suscripciones_push`). Subscribing an endpoint already stored by another user REASSIGNS it to the session user (a device belongs to whoever is logged in). The browser re-syncs its subscription on app load and session change and removes it on logout.
- **Endpoint allowlist**: only `https` endpoints on known push services are accepted.
- **Descriptive pushes**: the push carries the notice's real title and message plus a deep link and the notice id (`{ titulo, mensaje, url, id }`), encrypted end to end (RFC 8291). The notice builders of Aulas, Designaciones and Tareas get shorter, more concrete titles.
- **Per-module preferences** (push only; the in-app notice is always kept): `GET/PUT /api/notificaciones/push/preferencias`, default all enabled, opt-outs persisted per user in `notificaciones.preferencias_push`.
- **Delivery off the request path**: `Notificador` enqueues a job in a bounded in-memory queue; a hosted service delivers with a per-send timeout. 404/410 delete the subscription.
- **VAPID configuration** `Notificaciones:WebPush:{PublicKey,PrivateKey,Subject}`, validated at startup; without keys push is disabled. Wired through Compose and the deploy workflows.
- **UI**: a settings button in the notifications panel opens "Preferencias de notificaciones" (device switch with state text, one switch per module with a short description, help link), a dismissible hint in the panel, and an explanation for iPhone without Home Screen install.
- **PWA basics**: `sw.js`, web manifest and PNG icons; nginx serves the service worker without caching.
- **Centro de ayuda**: new article "Activar las notificaciones en el celular".

## Capabilities

### New Capabilities

- `notificaciones-push`: subscriptions, endpoint allowlist, push content, per-module preferences, asynchronous delivery and VAPID configuration.
- `preferencias-notificaciones-push`: the preferences view of the notifications panel, the activation hint, PWA basics and the help article.

### Modified Capabilities

None. The behaviour of the in-app capabilities of `notificaciones-in-app` (not archived yet) is unchanged except for the wording of the notice titles, which this change owns in `notificaciones-push`.

## Impact

- Backend: `Modules.Notificaciones` (new tables, endpoints, queue, sender, hosted service), the notice builders of `Modules.Aulas`, `Modules.Designaciones` and `Modules.Tareas` (titles only). New NuGet dependency `WebPush` 1.0.13 in `Modules.Notificaciones` only; no new project reference, dependency graph unchanged.
- Database: one incremental migration for two tables; no published migration is edited.
- Frontend: `features/notificaciones`, `shared/auth` (logout hook), `public/` (service worker, manifest, icons), `index.html`, nginx config, Centro de ayuda.
- Ops: three new settings (`WEBPUSH_PUBLIC_KEY`, `WEBPUSH_PRIVATE_KEY`, `WEBPUSH_SUBJECT`) in Compose and the deploy workflows; documented in `docs/operations`.
- Docs: `api-contracts`, `data-model`, `domains/notificaciones`, `stack`, the notificaciones design spec and the help article (rule 9).
- Rollback: revert the commits; the two tables can stay unused, and without VAPID keys the feature is off. No data of existing tables changes.
