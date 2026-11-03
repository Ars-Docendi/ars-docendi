/**
 * Target of a tapped push notification, parked by the service worker (`public/sw.js`) in Cache
 * Storage. On iOS the app can cold-start at its start page and a message posted to it is lost, so
 * the page reads this on mount and when it becomes visible. Keep the cache name and key in sync
 * with the worker.
 */
export const CACHE_PENDIENTE = "ars-docendi-push";
export const CLAVE_PENDIENTE = "/__push-pendiente";
/** A target older than this is a stale tap: ignored. */
export const VIGENCIA_PENDIENTE_MS = 2 * 60 * 1000;

export interface DestinoPush {
  url: string;
  id: string | null;
}

/** Only in-app paths: a target must never take the router to another origin. */
export function esRutaInterna(url: unknown): url is string {
  return typeof url === "string" && url.startsWith("/") && !url.startsWith("//");
}

/** Reads and deletes the pending target; `null` when there is none, it is stale or it is invalid. */
export async function consumirPendientePush(ahora = Date.now()): Promise<DestinoPush | null> {
  try {
    if (typeof caches === "undefined") return null;
    const cache = await caches.open(CACHE_PENDIENTE);
    const respuesta = await cache.match(CLAVE_PENDIENTE);
    if (!respuesta) return null;
    // Deleted before use: two concurrent consumers (mount + visibility) navigate only once.
    if (!(await cache.delete(CLAVE_PENDIENTE))) return null;
    const dato = (await respuesta.json()) as { url?: unknown; id?: unknown; en?: unknown };
    if (typeof dato.en !== "number" || ahora - dato.en > VIGENCIA_PENDIENTE_MS) return null;
    if (!esRutaInterna(dato.url)) return null;
    return { url: dato.url, id: typeof dato.id === "string" ? dato.id : null };
  } catch {
    return null;
  }
}
