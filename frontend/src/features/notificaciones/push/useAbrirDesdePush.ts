import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";

import { marcarLeida } from "../api/notificacionesApi";
import { notificacionesKeys } from "../hooks/useNotificaciones";
import { consumirPendientePush, esRutaInterna } from "./pendientePush";

/** Message the service worker posts to an open window when a push notification is tapped. */
export const MENSAJE_ABRIR_PUSH = "ars-docendi:abrir";

/**
 * Takes the person to the notice they tapped in a push notification, without reloading when the app
 * is already open. Two paths reach the same place and are idempotent:
 * - the service worker's message to an open window;
 * - the pending target the worker parks in Cache Storage, consumed (deleted) on mount and whenever
 *   the page becomes visible: it survives the cold start in which a message is lost (iOS).
 * The unread counters are refreshed because the notice gets marked as read.
 */
export function useAbrirDesdePush() {
  const navigate = useNavigate();
  const cliente = useQueryClient();

  useEffect(() => {
    function ir(url: string) {
      // Already there (the other path got here first): do not navigate twice.
      if (window.location.pathname + window.location.search !== url) {
        navigate(url, { replace: true });
      }
      void cliente.invalidateQueries({ queryKey: notificacionesKeys.all });
    }

    async function consumir() {
      const destino = await consumirPendientePush();
      if (!destino) return;
      ir(destino.url);
      // The worker's own request may have been lost; marking is idempotent.
      if (destino.id) void marcarLeida(destino.id).catch(() => undefined);
    }

    function alVolverVisible() {
      if (document.visibilityState === "visible") void consumir();
    }

    function alRecibir(evento: MessageEvent) {
      const datos = evento.data as { tipo?: string; url?: unknown } | null;
      if (datos?.tipo !== MENSAJE_ABRIR_PUSH || !esRutaInterna(datos.url)) return;
      ir(datos.url);
      // The message already took us there: drop the pending copy so it is not replayed.
      void consumirPendientePush();
    }

    void consumir();
    document.addEventListener("visibilitychange", alVolverVisible);
    const sw = typeof navigator !== "undefined" ? navigator.serviceWorker : undefined;
    sw?.addEventListener("message", alRecibir);
    return () => {
      document.removeEventListener("visibilitychange", alVolverVisible);
      sw?.removeEventListener("message", alRecibir);
    };
  }, [navigate, cliente]);
}
