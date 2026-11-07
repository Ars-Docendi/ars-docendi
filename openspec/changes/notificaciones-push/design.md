# Design

## Context

`INotificador` is called by Aulas, Designaciones and Tareas after their transaction committed. It resolves recipients, inserts one `notificaciones.notificaciones` row per user and is best effort by contract. The Web Push spike hooked a synchronous sender into it. Verified facts from the spike: Apple's push service (`web.push.apple.com`) delivers to an installed PWA on iOS 16.4+, rejects a VAPID subject on a reserved domain such as `.local` with `403 BadJwtToken`, and a stale endpoint made the request wait 10 s.

## Decisions

**D1. Content over the push services.** The payload is `{ titulo, mensaje, url, id }`. Web Push payloads are encrypted with the subscription keys (RFC 8291, `aes128gcm`), so Google, Mozilla, Apple and Microsoft only see ciphertext. This replaces the spike's content-free wake-up, which showed a generic text and made the notification useless on a lock screen. Titles and messages are NEVER logged: logs carry only the status code and the push service's reason. The message is truncated to 240 characters (the payload limit is about 4 KB) and the title to 80.

**D2. Queue + hosted service, not a request-path call.** `Notificador` writes the rows and, only if Web Push is enabled, `TryWrite`s one job per notice (recipients with their notice ids, plus the content) into a bounded `Channel<T>` (capacity 1000, `DropWrite`; a drop is logged with a count only). `ProcesadorPush` (`BackgroundService`) drains it in its own DI scope, loads the opt-outs and subscriptions of the recipients, and sends each push with a 10 s per-send timeout linked to shutdown. The in-memory queue accepts losing jobs on a crash, exactly like the in-app notice accepted losing a notice on failure (no outbox: same decision as `notificaciones-in-app`). `Notificador` never awaits delivery.

**D3. External boundary interface.** `IEnviadorPush.EnviarAsync(destino, carga, ct)` returns `Enviado | Caducada | Fallo` and is the only place that talks to `WebPush.WebPushClient`. It is justified as a real external boundary (HTTPS to third-party services) and lets tests assert what would be sent without a network. 404 and 410 map to `Caducada` and the processor deletes the subscription; any other failure logs `status + reason` and keeps it.

**D4. Subscriptions per device, ownership by session.** Unique `endpoint`. `POST` is an upsert that reassigns `usuario_id` and refreshes the keys. `DELETE` removes the row only when `usuario_id` is the session user, so one user cannot unsubscribe another's device. The client syncs on app load and when the session user changes, and sends `DELETE` for its endpoint before logging out; the browser keeps its `PushSubscription` so the next login only re-registers it.

**D5. Endpoint allowlist.** The endpoint is an arbitrary URL the server later POSTs to, so it is an SSRF vector. Accept only `https`, default port, no credentials, and a host equal to `fcm.googleapis.com` or `updates.push.services.mozilla.com`, or ending in `.push.apple.com`, `.notify.windows.com` or `.push.services.mozilla.com`. Anything else is a validation error (400). The check is a pure function.

**D6. Preferences.** One on/off switch per module, not per event type. Table `notificaciones.preferencias_push (usuario_id, modulo, habilitado)` with a CHECK on `modulo` (`AULAS`, `DESIGNACIONES`, `TAREAS`); a missing row means enabled. The module of a notice is derived from its `TipoEntidad` (`SOLICITUD_AULA` to Aulas, `PEDIDO_DESIGNACION` to Designaciones, `TAREA` to Tareas), so every event type of a module follows the same switch and a new event type needs no schema change. `GET` returns every module with its state; `PUT` accepts a partial list and upserts it; an unknown module is a 400 and nothing is written. The sender skips a recipient who disabled the notice's module; the in-app notice is unaffected. Preferences are per user, not per device.

**D6b. Atomic subscription upsert.** The browser may re-sync the same endpoint from several places at once, so the upsert is one `INSERT ... ON CONFLICT (endpoint) DO UPDATE` statement: concurrent requests never raise the unique violation. The frontend also shares one in-flight re-sync per user and skips a subscription already registered for that user.

**D7. VAPID configuration.** `Notificaciones:WebPush:{PublicKey,PrivateKey,Subject}`. Empty keys: feature off (the public-key endpoint answers 404 and the UI says it is unavailable). Only one key set, malformed keys or a subject that is not `mailto:`/`https:` is an error; so is a subject on a reserved domain (`.local`, `.localhost`, `.test`, `.example`, `.invalid`, `example.com|org|net`) because Apple answers 403. In Production an error stops the host at startup (same stance as the sealing keys); elsewhere the feature is disabled and the error logged, except the reserved-domain case, which only warns outside Production because Chrome and Firefox accept it. Keys come from the environment, never from the repository.

**D8. Notice texts.** Titles carry the entity (`Aula asignada: Aula 204`, `Pedido 2026-9002 devuelto`, `Tarea N° 12: nuevo comentario`) so the lock screen is readable; messages keep naming the aula, pedido or tarea. Same builders feed the in-app panel, so the snapshot rule (text frozen at emission) is unchanged.

**D9. Deep link.** `url` is the same route the panel uses (`rutaDeEntidad`): `/aulas/solicitudes/:id`, `/designaciones/pedidos/:id`, `/tareas/:id`. The service worker uses `tag = id` so notices never collapse, focuses an open window and navigates it, or opens the URL, and marks the notice read through `PATCH /api/notificaciones/{id}/leida` (same-origin cookie; failures are ignored).

**D10. UI.** A gear button in the panel header switches the panel body to a "Preferencias de notificaciones" view (back button returns to the list). Device switch states: `no disponible`, `iPhone sin instalar`, `bloqueadas`, `desactivadas`, `activas`. Type switches are grouped by module and disabled while the device is off; groups are shown only when the active role's permissions can receive them (`aulas.solicitar|aulas.aprobar`, `designaciones.gestionar|designaciones.revisar`, `tareas.ver`). Without push active the panel shows one small dismissible hint (dismissal kept in `localStorage`, best effort). On iOS Safari without `PushManager` the view explains Home Screen installation and links to the help article. The switch is a native `button role="switch"`, because `@ars-docendi/ui` has no switch.

**D11. PWA and hosting.** `public/sw.js` (scope `/`), `manifest.webmanifest` (standalone, theme color, 192/512 and maskable icons, plus an `apple-touch-icon`). nginx serves `sw.js` and the manifest with `Cache-Control: no-cache` so a new worker is picked up on the next load, and the SPA fallback must not return `index.html` for them (they exist as files). Vite dev serves `public/` as is.

## Risks / Trade-offs

- Queue jobs are lost on a process crash: accepted, push is a convenience over the durable in-app notice.
- A single instance consumes the queue; with several replicas each one sends for its own requests, so there is no duplication.
- Content now crosses push services, encrypted. A device that is compromised or a lock screen can show it; the user controls per-module delivery and the OS controls lock-screen previews.
- `WebPush` 1.0.13 is a small, widely used library; the VAPID/encryption code is not reimplemented.

## Migration / Rollback

One incremental SQL migration (`CrearSuscripcionesYPreferenciasPushNotificaciones`) creating two tables with FKs to `identity.users` (`ON DELETE CASCADE`). Rollback: revert the commits and leave the tables; without keys nothing runs.

## Open Questions

- Per-event granularity could later be per-device if users ask for it.
