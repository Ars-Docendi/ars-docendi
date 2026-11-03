// Service worker of Ars Docendi. Its only job is Web Push: show the notification and, on click,
// take the person to the notice. It caches nothing and has no fetch handler.
//
// The payload is `{ titulo, mensaje, url, id }`, encrypted end to end (RFC 8291): the push
// services cannot read it. Never log it.

const MENSAJE_ABRIR = "ars-docendi:abrir";

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
      // Best effort: marks the notice as read with the session cookie. Fails silently without a
      // session or when the notice belongs to another profile.
      if (id) {
        fetch(`/api/notificaciones/${encodeURIComponent(id)}/leida`, {
          method: "PATCH",
          credentials: "same-origin",
        }).catch(() => undefined);
      }
      const ventanas = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
      const abierta = ventanas.find((v) => new URL(v.url).origin === self.location.origin);
      if (abierta) {
        // The open app navigates by itself (no reload) and refreshes its counters.
        abierta.postMessage({ tipo: MENSAJE_ABRIR, url: ruta });
        await abierta.focus();
        return;
      }
      await self.clients.openWindow(ruta);
    })(),
  );
});
