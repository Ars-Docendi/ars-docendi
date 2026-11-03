import {
  cancelarSuscripcionPush,
  obtenerClavePublicaPush,
  registrarSuscripcionPush,
} from "../api/pushApi";

/** The browser layer of Web Push: feature detection, the service worker and the subscription. */

export function soportaPush(): boolean {
  return (
    typeof window !== "undefined" &&
    "serviceWorker" in navigator &&
    "PushManager" in window &&
    "Notification" in window
  );
}

export function esIos(): boolean {
  if (typeof navigator === "undefined") return false;
  // iPadOS 13+ reports itself as a Mac with a touch screen.
  return (
    /iPhone|iPad|iPod/.test(navigator.userAgent) ||
    (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1)
  );
}

/** Opened from the Home Screen icon: the only place iOS exposes Web Push. */
export function esAppInstalada(): boolean {
  const iosStandalone = (navigator as Navigator & { standalone?: boolean }).standalone === true;
  return iosStandalone || window.matchMedia?.("(display-mode: standalone)").matches === true;
}

export function claveAUint8Array(base64Url: string): Uint8Array<ArrayBuffer> {
  const relleno = "=".repeat((4 - (base64Url.length % 4)) % 4);
  const binario = atob((base64Url + relleno).replace(/-/g, "+").replace(/_/g, "/"));
  return Uint8Array.from(binario, (c) => c.charCodeAt(0));
}

async function registrarServiceWorker(): Promise<ServiceWorkerRegistration> {
  const registro = await navigator.serviceWorker.register("/sw.js");
  await navigator.serviceWorker.ready;
  return registro;
}

/** The subscription this browser already holds, if any. */
export async function suscripcionActual(): Promise<PushSubscription | null> {
  return (await registrarServiceWorker()).pushManager.getSubscription();
}

/** Asks for permission (from a user gesture), subscribes and registers the device on the server. */
export async function activarEnDispositivo(
  clavePublica: string,
): Promise<"activo" | "bloqueado" | "desactivado"> {
  const permiso = await Notification.requestPermission();
  if (permiso === "denied") return "bloqueado";
  if (permiso !== "granted") return "desactivado";
  const registro = await registrarServiceWorker();
  const suscripcion =
    (await registro.pushManager.getSubscription()) ??
    (await registro.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: claveAUint8Array(clavePublica),
    }));
  await registrarSuscripcionPush(suscripcion.toJSON());
  return "activo";
}

/** Removes the device from the server and from the browser. */
export async function desactivarEnDispositivo(): Promise<void> {
  const suscripcion = await suscripcionActual();
  if (!suscripcion) return;
  await cancelarSuscripcionPush(suscripcion.endpoint);
  await suscripcion.unsubscribe();
}

/**
 * Session start or change: if this browser already holds a subscription and the permission is
 * granted, registers it for the session user (the server reassigns a device to whoever is logged
 * in). Never prompts and never throws: push is an enhancement.
 */
export async function sincronizarSuscripcion(): Promise<void> {
  try {
    if (!soportaPush() || Notification.permission !== "granted") return;
    if ((await obtenerClavePublicaPush()) === null) return;
    const suscripcion = await suscripcionActual();
    if (suscripcion) await registrarSuscripcionPush(suscripcion.toJSON());
  } catch {
    // Best effort.
  }
}

/**
 * Before logout: tells the server this device is no longer the session user's. The browser keeps
 * its subscription, so the next login only re-registers it. Never throws and never blocks logout.
 */
export async function desuscribirDelServidor(): Promise<void> {
  try {
    if (!soportaPush()) return;
    const registro = await navigator.serviceWorker.getRegistration();
    const suscripcion = await registro?.pushManager.getSubscription();
    if (suscripcion) await cancelarSuscripcionPush(suscripcion.endpoint);
  } catch {
    // The session ends anyway.
  }
}
