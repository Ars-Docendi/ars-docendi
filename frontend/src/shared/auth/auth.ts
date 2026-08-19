import { limpiarSesionDesarrollo, obtenerSesionDesarrollo } from "./dev/session";

/** Adaptador temporal: desarrollo usa la selección sembrada; producción espera SSO. */
export function isAuthenticated(): boolean {
  return import.meta.env.DEV && obtenerSesionDesarrollo() !== null;
}

export function clearToken(): void {
  if (import.meta.env.DEV) limpiarSesionDesarrollo();
}
