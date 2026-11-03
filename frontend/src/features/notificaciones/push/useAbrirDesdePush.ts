import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";

import { notificacionesKeys } from "../hooks/useNotificaciones";

/** Message the service worker posts to an open window when a push notification is tapped. */
export const MENSAJE_ABRIR_PUSH = "ars-docendi:abrir";

/**
 * With the app already open, a tap on a push notification must not reload it: the service worker
 * hands over the in-app route and the router goes there. The unread counters are refreshed because
 * the worker marks the notice as read.
 */
export function useAbrirDesdePush() {
  const navigate = useNavigate();
  const cliente = useQueryClient();

  useEffect(() => {
    if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;
    function alRecibir(evento: MessageEvent) {
      const datos = evento.data as { tipo?: string; url?: unknown } | null;
      if (datos?.tipo !== MENSAJE_ABRIR_PUSH) return;
      // Only in-app paths: a message must never take the router to another origin.
      if (typeof datos.url !== "string" || !datos.url.startsWith("/") || datos.url.startsWith("//"))
        return;
      navigate(datos.url);
      void cliente.invalidateQueries({ queryKey: notificacionesKeys.all });
    }
    navigator.serviceWorker.addEventListener("message", alRecibir);
    return () => navigator.serviceWorker.removeEventListener("message", alRecibir);
  }, [navigate, cliente]);
}
