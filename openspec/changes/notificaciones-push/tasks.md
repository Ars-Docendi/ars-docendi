# Tasks

## 1. Database and domain

- [x] 1.1 Scaffold the migration with `node scripts/migrations/scaffold.mjs Notificaciones CrearSuscripcionesYPreferenciasPushNotificaciones`; create `suscripciones_push` and `preferencias_push` (FKs to `identity.users`, unique endpoint, `modulo` CHECK, checks). Keep the EF model and snapshot in sync.
- [x] 1.2 Add the `WebPush` package to `Modules.Notificaciones` and the entities `SuscripcionPush` and `PreferenciaPush`.

## 2. Backend

- [x] 2.1 `OpcionesWebPush` with startup validation (subject scheme and reserved domains, key pair) and a singleton that exposes the public key or "disabled".
- [x] 2.2 `EndpointsPush` allowlist (pure function) and the subscription service: atomic `INSERT ... ON CONFLICT` upsert that reassigns, delete only own rows.
- [x] 2.3 Preferences service and `GET/PUT push/preferencias` (one switch per module, validated against `ModulosPush`; the module comes from the notice's `TipoEntidad`).
- [x] 2.4 `IEnviadorPush` + `EnviadorWebPush`, `ColaPush` and `ProcesadorPush` with the per-send timeout, 404/410 cleanup, content-free logging and an Information log (status and push-service host) for accepted pushes; `Notificador` enqueues after saving.
- [x] 2.5 Rewrite the notice titles in `AvisosAulas`, `AvisosPedido`, `AvisosTarea` and update the emission tests.

## 3. Backend tests

- [x] 3.1 Opt-out respected and payload content and deep link per entity type.
- [x] 3.2 Subscription ownership (upsert reassigns, delete only own) and endpoint allowlist.
- [x] 3.3 Per-module preferences validation and defaults, module-wide opt-out suppressing push while keeping the in-app notice, concurrent upserts of one endpoint; key endpoint with and without keys.
- [x] 3.4 Delivery does not block `NotificarAsync`; a 410 deletes the subscription.
- [x] 3.5 VAPID configuration rules.

## 4. Frontend

- [x] 4.1 `sw.js`, manifest, PNG icons, `index.html` links; nginx no-cache headers for the worker and manifest.
- [x] 4.2 `pushApi`, device hook (support detection, permission, subscribe, sync on session change, unsubscribe on logout) and preferences hook; the re-sync is deduplicated per user and subscription.
- [x] 4.3 Preferences view, gear button, dismissible hint, iOS-not-installed explanation; CSS at 390 px.
- [x] 4.4 Frontend tests: switch states, disabled groups, unsupported and iOS-not-installed messages, hint dismissal.
- [x] 4.5 Robust deep link on iOS cold start: the worker parks the tapped target in Cache Storage and the app consumes it on mount and on visibility (test: recent navigates once, stale ignored).

## 5. Docs and operations

- [x] 5.1 Centro de ayuda article "Activar las notificaciones en el celular" and its registration; link from the view.
- [x] 5.2 Design spec, `api-contracts`, `data-model`, `domains/notificaciones`, `stack`.
- [x] 5.3 Compose and workflow wiring (`WEBPUSH_*`), `docs/operations` and README note about the dev subject.

## 6. Verification

- [x] 6.1 Run every check of `AGENTS.md → Verificación` that applies and record the result.
