import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
  obtenerClavePublicaPush: vi.fn(),
  registrarSuscripcionPush: vi.fn(),
  cancelarSuscripcionPush: vi.fn(),
}));
vi.mock("../api/pushApi", () => api);

import { desuscribirDelServidor, sincronizarSuscripcion } from "./dispositivoPush";

const suscripcion = {
  endpoint: "https://fcm.googleapis.com/fcm/send/abc",
  toJSON: () => ({
    endpoint: "https://fcm.googleapis.com/fcm/send/abc",
    keys: { p256dh: "p", auth: "a" },
  }),
  unsubscribe: vi.fn(),
};

/** A browser with Web Push whose service worker holds `existente` (or nothing). */
function navegadorConPush(existente: unknown, permiso: NotificationPermission) {
  const registro = { pushManager: { getSubscription: vi.fn().mockResolvedValue(existente) } };
  vi.stubGlobal("PushManager", class {});
  vi.stubGlobal("Notification", { permission: permiso });
  Object.defineProperty(navigator, "serviceWorker", {
    configurable: true,
    value: {
      register: vi.fn().mockResolvedValue(registro),
      ready: Promise.resolve(registro),
      getRegistration: vi.fn().mockResolvedValue(registro),
    },
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  api.obtenerClavePublicaPush.mockResolvedValue("clave-vapid");
  api.registrarSuscripcionPush.mockResolvedValue(undefined);
  api.cancelarSuscripcionPush.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.unstubAllGlobals();
  Reflect.deleteProperty(navigator, "serviceWorker");
});

describe("sincronizarSuscripcion (al cargar la app o cambiar de usuario)", () => {
  it("registra para el usuario de la sesión la suscripción que el navegador ya tiene", async () => {
    navegadorConPush(suscripcion, "granted");
    await sincronizarSuscripcion();
    expect(api.registrarSuscripcionPush).toHaveBeenCalledWith(suscripcion.toJSON());
  });

  it("no pide permiso ni registra nada si el permiso no está concedido o no hay suscripción", async () => {
    navegadorConPush(suscripcion, "default");
    await sincronizarSuscripcion();
    navegadorConPush(null, "granted");
    await sincronizarSuscripcion();
    expect(api.registrarSuscripcionPush).not.toHaveBeenCalled();
  });

  it("no hace nada si el servidor tiene el push apagado, y un fallo no se propaga", async () => {
    navegadorConPush(suscripcion, "granted");
    api.obtenerClavePublicaPush.mockResolvedValue(null);
    await sincronizarSuscripcion();
    expect(api.registrarSuscripcionPush).not.toHaveBeenCalled();

    api.obtenerClavePublicaPush.mockResolvedValue("clave");
    api.registrarSuscripcionPush.mockRejectedValue(new Error("red"));
    await expect(sincronizarSuscripcion()).resolves.toBeUndefined();
  });
});

describe("desuscribirDelServidor (al cerrar sesión)", () => {
  it("da de baja en el servidor el endpoint de este dispositivo, sin desuscribir el navegador", async () => {
    navegadorConPush(suscripcion, "granted");
    await desuscribirDelServidor();
    expect(api.cancelarSuscripcionPush).toHaveBeenCalledWith(suscripcion.endpoint);
    expect(suscripcion.unsubscribe).not.toHaveBeenCalled();
  });

  it("nunca bloquea el cierre de sesión: sin suscripción o con el servidor caído resuelve igual", async () => {
    navegadorConPush(null, "granted");
    await desuscribirDelServidor();
    expect(api.cancelarSuscripcionPush).not.toHaveBeenCalled();

    navegadorConPush(suscripcion, "granted");
    api.cancelarSuscripcionPush.mockRejectedValue(new Error("500"));
    await expect(desuscribirDelServidor()).resolves.toBeUndefined();
  });
});
