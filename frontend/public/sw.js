// Service worker of Ars Docendi. Its only job is Web Push: show the notification and, on click,
// take the person to the notice. It caches nothing and has no fetch handler.
//
// The payload is `{ titulo, mensaje, url, id }`, encrypted end to end (RFC 8291): the push
// services cannot read it. Never log it.

const MENSAJE_ABRIR = "ars-docendi:abrir";

// Where the tapped notice is parked until the app consumes it. A message posted to a suspended or
// still-booting window is lost (iOS standalone web apps), but Cache Storage survives a cold start
// and both this worker and the page can read it. Keep in sync with `useAbrirDesdePush`.
const CACHE_PENDIENTE = "ars-docendi-push";
const CLAVE_PENDIENTE = "/__push-pendiente";

async function guardarPendiente(ruta, id) {
  try {
    const cache = await caches.open(CACHE_PENDIENTE);
    await cache.put(
      CLAVE_PENDIENTE,
      new Response(JSON.stringify({ url: ruta, id: id || null, en: Date.now() }), {
        headers: { "Content-Type": "application/json" },
      }),
    );
  } catch {
    // Without Cache Storage the message and the opened URL are the fallbacks.
  }
}

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => event.waitUntil(self.clients.claim()));

self.addEventListener("push", (event) => {
  let datos = {};
  try {
    datos = event.data ? event.data.json() : {};
  } catch {
    datos = {};
  }
  event.waitUntil(
    self.registration.showNotification(datos.titulo || "Ars Docendi", {
      body: datos.mensaje || "Tenés un aviso nuevo.",
      icon: "/icons/icon-192.png",
      // One tag per notice: two pushes never replace each other.
      tag: datos.id || undefined,
      data: { url: datos.url || "/", id: datos.id || null },
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const { url, id } = event.notification.data || {};
  const destino = new URL(url || "/", self.location.origin);
  // Same-origin links only, whatever the payload says.
  const ruta = destino.origin === self.location.origin ? destino.pathname + destino.search : "/";

  event.waitUntil(
    (async () => {
      await guardarPendiente(ruta, id);
      // Best effort: marks the notice as read with the session cookie. Fails silently without a
      // session or when the notice belongs to another profile. The app marks it too when it
      // consumes the pending target, in case this request is lost.
      const leida = id
        ? fetch(`/api/notificaciones/${encodeURIComponent(id)}/leida`, {
            method: "PATCH",
            credentials: "same-origin",
          }).catch(() => undefined)
        : Promise.resolve();
      const ventanas = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      const abierta = ventanas.find((v) => new URL(v.url).origin === self.location.origin);
      if (abierta) {
        // The open app navigates by itself (no reload); the pending target covers a window that
        // missed the message. Both are idempotent in the app.
        abierta.postMessage({ tipo: MENSAJE_ABRIR, url: ruta });
        try {
          await abierta.focus();
          await abierta.navigate(ruta);
        } catch {
          // navigate() is not supported everywhere (nor for uncontrolled windows).
        }
      } else {
        await self.clients.openWindow(ruta);
      }
      await leida;
    })(),
  );
});
