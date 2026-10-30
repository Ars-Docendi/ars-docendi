const USUARIO_KEY = "adoc.dev.userId";
const ASIGNACION_KEY = "adoc.dev.asignacionId";

/**
 * Sesión de desarrollo guardada en el navegador: quién ingresó y, una vez elegida,
 * la asignación de rol con la que opera. Viajan en headers en cada solicitud.
 */
export interface SesionDesarrollo {
  usuarioId: string;
  asignacionId: string | null;
}

const oyentes = new Set<() => void>();
let claveAnterior = "";
let snapshot: SesionDesarrollo | null = null;

export function obtenerSesionDesarrollo(): SesionDesarrollo | null {
  const usuarioId = localStorage.getItem(USUARIO_KEY) ?? "";
  const asignacionId = localStorage.getItem(ASIGNACION_KEY) || null;
  const clave = `${usuarioId}|${asignacionId ?? ""}`;
  if (clave === claveAnterior) return snapshot;
  claveAnterior = clave;
  snapshot = usuarioId ? { usuarioId, asignacionId } : null;
  return snapshot;
}

/** Ingresa como esa identidad. El rol se elige después, como con Microsoft. */
export function seleccionarSesionDesarrollo(usuarioId: string): void {
  localStorage.setItem(USUARIO_KEY, usuarioId);
  localStorage.removeItem(ASIGNACION_KEY);
  notificar();
}

export function elegirAsignacionDesarrollo(asignacionId: string): void {
  localStorage.setItem(ASIGNACION_KEY, asignacionId);
  notificar();
}

export function limpiarSesionDesarrollo(): void {
  localStorage.removeItem(USUARIO_KEY);
  localStorage.removeItem(ASIGNACION_KEY);
  notificar();
}

export function suscribirSesionDesarrollo(oyente: () => void): () => void {
  oyentes.add(oyente);
  return () => oyentes.delete(oyente);
}

function notificar(): void {
  claveAnterior = "";
  oyentes.forEach((oyente) => oyente());
}
