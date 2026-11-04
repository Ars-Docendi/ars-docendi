import { isAxiosError } from "axios";
import { apiClient } from "../api/client";

/** El ingreso con Microsoft se habilita por ambiente, igual que el selector de desarrollo. */
export const microsoftLoginEnabled = import.meta.env.VITE_MICROSOFT_LOGIN_ENABLED === "true";

export const RUTA_SESION = "/api/auth/sesion";

export const sesionKeys = {
  actual: ["auth", "sesion"] as const,
  desarrollo: ["desarrollo", "sesion"] as const,
};

/** Asignación de rol: el rol y dónde rige. */
export interface AsignacionApi {
  id: string;
  rolCodigo: string;
  rolNombre: string;
  ambitoTipo: "global" | "materia" | "carrera";
  ambitoNombre: string | null;
}

/**
 * Sesión tal como la devuelve el backend (por cookie o, en desarrollo, por headers).
 * Siempre opera con una asignación; `requiereElegirRol` indica que es la de por
 * defecto y la persona todavía no eligió.
 */
export interface SesionApi {
  usuarioId: string;
  nombreParaMostrar: string;
  upn: string;
  rolCodigo: string;
  rolNombre: string;
  permisos: string[];
  asignacion: AsignacionApi;
  asignaciones: AsignacionApi[];
  requiereElegirRol: boolean;
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

/** Elige o cambia la asignación con la que opera la sesión por cookie. */
export async function elegirAsignacion(asignacionId: string): Promise<SesionApi> {
  return (await apiClient.post<SesionApi>("/api/auth/asignacion", { asignacionId })).data;
}

export async function cerrarSesion(): Promise<void> {
  await apiClient.post("/api/auth/logout");
}

/** El backend inicia el ingreso y vuelve a `destino`; rutas externas las descarta. */
export function urlIngresoMicrosoft(destino: string): string {
  return `/api/auth/login?returnUrl=${encodeURIComponent(destino)}`;
}
