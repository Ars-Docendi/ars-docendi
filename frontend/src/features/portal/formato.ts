import type { Periodo } from "./types";

/**
 * Las fechas de período se guardan como "YYYY" o "YYYY-MM". El mes es opcional:
 * solo hace falta cuando dos ítems caen en el mismo año y hay que desempatar.
 */
export const MESES = [
  { valor: "01", nombre: "ene" },
  { valor: "02", nombre: "feb" },
  { valor: "03", nombre: "mar" },
  { valor: "04", nombre: "abr" },
  { valor: "05", nombre: "may" },
  { valor: "06", nombre: "jun" },
  { valor: "07", nombre: "jul" },
  { valor: "08", nombre: "ago" },
  { valor: "09", nombre: "sep" },
  { valor: "10", nombre: "oct" },
  { valor: "11", nombre: "nov" },
  { valor: "12", nombre: "dic" },
] as const;

export function anioDe(fecha: string): string {
  return fecha.slice(0, 4);
}

export function mesDe(fecha: string): string {
  return fecha.length > 4 ? fecha.slice(5, 7) : "";
}

export function componerFecha(anio: string, mes: string): string {
  const a = anio.trim();
  if (!a) return "";
  return mes ? `${a}-${mes}` : a;
}

/** "2014" o "mar 2014" según tenga mes. */
export function formatearFecha(fecha: string): string {
  if (!fecha) return "";
  const mes = mesDe(fecha);
  if (!mes) return anioDe(fecha);
  const nombre = MESES.find((m) => m.valor === mes)?.nombre ?? mes;
  return `${nombre} ${anioDe(fecha)}`;
}

/** "2015 – 2020", "mar 2014 – actual" cuando el período sigue vigente. */
export function rangoPeriodo(p: Periodo): string {
  return `${formatearFecha(p.desde)} – ${p.hasta ? formatearFecha(p.hasta) : "actual"}`;
}

/** Clave comparable: el año sin mes ordena antes que cualquier mes del mismo año. */
function clave(fecha: string): string {
  return fecha.length > 4 ? fecha : `${fecha}-00`;
}

/**
 * Ordena de más actual a más antiguo: primero lo que sigue en curso, después
 * por fecha de fin descendente, y a igual fin por fecha de inicio descendente.
 */
export function ordenarPorPeriodo<T extends Periodo>(lista: T[]): T[] {
  return [...lista].sort((a, b) => {
    const finA = a.hasta === null ? "9999-99" : clave(a.hasta);
    const finB = b.hasta === null ? "9999-99" : clave(b.hasta);
    if (finA !== finB) return finB.localeCompare(finA);
    return clave(b.desde).localeCompare(clave(a.desde));
  });
}

/** Ordena por una fecha ISO simple, de más reciente a más antigua. */
export function ordenarPorFecha<T extends { fecha: string }>(lista: T[]): T[] {
  return [...lista].sort((a, b) => b.fecha.localeCompare(a.fecha));
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
