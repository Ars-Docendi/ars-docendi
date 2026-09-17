import { limpiarSesionDesarrollo, obtenerSesionDesarrollo } from "./dev/session";
import { developmentAuthEnabled } from "./developmentAuth";

/** Adaptador temporal: desarrollo usa la selección sembrada; producción espera SSO. */
export function isAuthenticated(): boolean {
  return developmentAuthEnabled && obtenerSesionDesarrollo() !== null;
}

export function clearToken(): void {
  if (developmentAuthEnabled) limpiarSesionDesarrollo();
}
