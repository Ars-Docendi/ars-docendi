import { isAxiosError } from "axios";
import { apiClient } from "../api/client";

/** El ingreso con Microsoft se habilita por ambiente, igual que el selector de desarrollo. */
export const microsoftLoginEnabled = import.meta.env.VITE_MICROSOFT_LOGIN_ENABLED === "true";

export const RUTA_SESION = "/api/auth/sesion";

export const sesionKeys = {
  actual: ["auth", "sesion"] as const,
};

/** Sesión por cookie tal como la devuelve el backend. */
export interface SesionApi {
  usuarioId: string;
  nombreParaMostrar: string;
  upn: string;
  rolCodigo: string;
  rolNombre: string;
  permisos: string[];
}

/** Sesión vigente, o `null` si no hay cookie válida. */
export async function obtenerSesion(): Promise<SesionApi | null> {
  try {
    const respuesta = await apiClient.get<SesionApi>(RUTA_SESION);
    return respuesta.data;
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 401) return null;
    throw error;
  }
}

export async function cerrarSesion(): Promise<void> {
  await apiClient.post("/api/auth/logout");
}

/** El backend inicia el ingreso y vuelve a `destino`; rutas externas las descarta. */
export function urlIngresoMicrosoft(destino: string): string {
  return `/api/auth/login?returnUrl=${encodeURIComponent(destino)}`;
}
