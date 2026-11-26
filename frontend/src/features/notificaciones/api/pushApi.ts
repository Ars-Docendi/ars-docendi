import { isAxiosError } from "axios";

import { apiClient } from "../../../shared/api/client";

/** VAPID public key, or `null` when the backend has Web Push disabled (404). */
export async function obtenerClavePublicaPush(): Promise<string | null> {
  try {
    const { data } = await apiClient.get<{ clavePublica: string }>(
      "/api/notificaciones/push/clave-publica",
    );
    return data.clavePublica;
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 404) return null;
    throw error;
  }
}

/** Upsert: a device already held by another user is reassigned to the session user. */
export async function registrarSuscripcionPush(suscripcion: PushSubscriptionJSON): Promise<void> {
  await apiClient.post("/api/notificaciones/push/suscripciones", suscripcion);
}

export async function cancelarSuscripcionPush(endpoint: string): Promise<void> {
  await apiClient.delete("/api/notificaciones/push/suscripciones", { data: { endpoint } });
}

export interface PreferenciaPush {
  modulo: string;
  habilitado: boolean;
}

export interface PreferenciasPush {
  modulos: PreferenciaPush[];
}

export async function obtenerPreferenciasPush(): Promise<PreferenciasPush> {
  const { data } = await apiClient.get<PreferenciasPush>("/api/notificaciones/push/preferencias");
  return data;
}

export async function guardarPreferenciasPush(
  cambios: PreferenciaPush[],
): Promise<PreferenciasPush> {
  const { data } = await apiClient.put<PreferenciasPush>("/api/notificaciones/push/preferencias", {
    modulos: cambios,
  });
  return data;
}
