const USUARIO_KEY = "adoc.dev.userId";
const ROL_KEY = "adoc.dev.roleCode";

export interface SesionDesarrollo {
  usuarioId: string;
  rolCodigo: string;
}

const oyentes = new Set<() => void>();
let claveAnterior = "";
let snapshot: SesionDesarrollo | null = null;

export function obtenerSesionDesarrollo(): SesionDesarrollo | null {
  const usuarioId = localStorage.getItem(USUARIO_KEY) ?? "";
  const rolCodigo = localStorage.getItem(ROL_KEY) ?? "";
  const clave = `${usuarioId}|${rolCodigo}`;
  if (clave === claveAnterior) return snapshot;
  claveAnterior = clave;
  snapshot = usuarioId && rolCodigo ? { usuarioId, rolCodigo } : null;
  return snapshot;
}

export function seleccionarSesionDesarrollo(usuarioId: string, rolCodigo: string): void {
  localStorage.setItem(USUARIO_KEY, usuarioId);
  localStorage.setItem(ROL_KEY, rolCodigo);
  notificar();
}

export function limpiarSesionDesarrollo(): void {
  localStorage.removeItem(USUARIO_KEY);
  localStorage.removeItem(ROL_KEY);
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
