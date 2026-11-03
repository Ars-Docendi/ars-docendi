import { useEffect } from "react";

import { sincronizarSuscripcion } from "./dispositivoPush";

/**
 * Re-registers this browser's existing push subscription for the session user, on app load and
 * whenever the user changes, so a device always belongs to whoever is logged in on it.
 */
export function useSincronizarPush(usuarioId: string) {
  useEffect(() => {
    void sincronizarSuscripcion();
  }, [usuarioId]);
}
