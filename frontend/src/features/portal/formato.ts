import type { Periodo } from "./types";

/** "2015 – 2020", o "2022 – actual" cuando el período sigue vigente. */
export function rangoPeriodo(p: Periodo): string {
  return `${p.desde} – ${p.hasta ?? "actual"}`;
}

/** Días desde hoy hasta la fecha ISO dada. Negativo si ya pasó. */
function diasHasta(fechaIso: string): number {
  const MS_POR_DIA = 24 * 60 * 60 * 1000;
  const hoy = new Date();
  hoy.setHours(0, 0, 0, 0);
  return Math.round((new Date(`${fechaIso}T00:00:00`).getTime() - hoy.getTime()) / MS_POR_DIA);
}

/** Umbral para avisar que una certificación está por vencer. */
const DIAS_AVISO_VENCIMIENTO = 90;

export interface EstadoVencimiento {
  estado: "green" | "yellow" | "red";
  detalle: string;
}

/**
 * Traduce el vencimiento de una certificación al semáforo de la librería.
 * Devuelve null si la certificación no vence.
 */
export function vencimientoDeCertificacion(vencimiento: string | null): EstadoVencimiento | null {
  if (!vencimiento) return null;
  const dias = diasHasta(vencimiento);
  if (dias < 0) return { estado: "red", detalle: `Vencida el ${vencimiento}` };
  if (dias <= DIAS_AVISO_VENCIMIENTO)
    return { estado: "yellow", detalle: `Vence el ${vencimiento}` };
  return { estado: "green", detalle: `Vigente hasta ${vencimiento}` };
}
