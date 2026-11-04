import { useCallback, useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";

import { obtenerClavePublicaPush } from "../api/pushApi";
import {
  activarEnDispositivo,
  desactivarEnDispositivo,
  esAppInstalada,
  esIos,
  soportaPush,
  suscripcionActual,
} from "./dispositivoPush";

/**
 * - `cargando`: still checking.
 * - `activo` / `desactivado`: this device can receive pushes; it does / does not.
 * - `bloqueado`: the browser permission is denied.
 * - `ios-sin-instalar`: iPhone/iPad Safari tab; Web Push exists only once added to the Home Screen.
 * - `sin-soporte`: this browser has no Web Push.
 * - `servidor-apagado`: the backend has no VAPID keys.
 * - `error`: the check itself failed.
 */
export type EstadoDispositivo =
  | "cargando"
  | "activo"
  | "desactivado"
  | "bloqueado"
  | "ios-sin-instalar"
  | "sin-soporte"
  | "servidor-apagado"
  | "error";

export interface DispositivoPush {
  estado: EstadoDispositivo;
  /** An activation or deactivation is in flight. */
  trabajando: boolean;
  /** Last failure of an activation or deactivation, in Spanish. */
  error: string | null;
  activar: () => Promise<void>;
  desactivar: () => Promise<void>;
}

export const claveDePushKey = ["notificaciones", "push", "clave"] as const;

/** Push state of THIS browser, derived once per mount and updated by `activar` / `desactivar`. */
export function useDispositivoPush(): DispositivoPush {
  const clave = useQuery({
    queryKey: claveDePushKey,
    queryFn: obtenerClavePublicaPush,
    staleTime: Infinity,
    retry: false,
  });
  const [detectado, setDetectado] = useState<EstadoDispositivo>("cargando");
  const [forzado, setForzado] = useState<EstadoDispositivo | null>(null);
  const [trabajando, setTrabajando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (clave.data === undefined) return;
    let vigente = true;
    void (async () => {
      let estado: EstadoDispositivo;
      if (clave.data === null) estado = "servidor-apagado";
      else if (!soportaPush())
        estado = esIos() && !esAppInstalada() ? "ios-sin-instalar" : "sin-soporte";
      else if (Notification.permission === "denied") estado = "bloqueado";
      else {
        try {
          const existente = await suscripcionActual();
          estado = existente && Notification.permission === "granted" ? "activo" : "desactivado";
        } catch {
          estado = "error";
        }
      }
      if (vigente) setDetectado(estado);
    })();
    return () => {
      vigente = false;
    };
  }, [clave.data]);

  const activar = useCallback(async () => {
    if (!clave.data) return;
    setError(null);
    setTrabajando(true);
    try {
      setForzado(await activarEnDispositivo(clave.data));
    } catch {
      setError("No pudimos activar las notificaciones. Probá de nuevo en un rato.");
    } finally {
      setTrabajando(false);
    }
  }, [clave.data]);

  const desactivar = useCallback(async () => {
    setError(null);
    setTrabajando(true);
    try {
      await desactivarEnDispositivo();
      setForzado("desactivado");
    } catch {
      setError("No pudimos desactivar las notificaciones. Probá de nuevo en un rato.");
    } finally {
      setTrabajando(false);
    }
  }, []);

  const estado: EstadoDispositivo = clave.isError ? "error" : (forzado ?? detectado);
  return { estado, trabajando, error, activar, desactivar };
}
