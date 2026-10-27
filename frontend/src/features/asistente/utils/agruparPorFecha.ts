import type { ConversacionResumen } from "../types";

export interface GrupoDeConversaciones {
  etiqueta: string;
  conversaciones: ConversacionResumen[];
}

const UN_DIA_MS = 24 * 60 * 60 * 1000;

function inicioDelDia(fecha: Date): number {
  return new Date(fecha.getFullYear(), fecha.getMonth(), fecha.getDate()).getTime();
}

/**
 * Agrupa conversaciones por fecha relativa de última actividad: Hoy, Ayer, Últimos 7 días, Anteriores.
 */
export function agruparPorFecha(
  conversaciones: ConversacionResumen[],
  ahora: Date = new Date(),
): GrupoDeConversaciones[] {
  const hoy = inicioDelDia(ahora);
  const ayer = hoy - UN_DIA_MS;
  const haceUnaSemana = hoy - 7 * UN_DIA_MS;

  const etiquetas = ["Hoy", "Ayer", "Últimos 7 días", "Anteriores"] as const;
  const baldes: Record<(typeof etiquetas)[number], ConversacionResumen[]> = {
    Hoy: [],
    Ayer: [],
    "Últimos 7 días": [],
    Anteriores: [],
  };

  for (const conversacion of conversaciones) {
    const inicio = inicioDelDia(new Date(conversacion.ultimaActividad));
    if (inicio >= hoy) baldes.Hoy.push(conversacion);
    else if (inicio >= ayer) baldes.Ayer.push(conversacion);
    else if (inicio >= haceUnaSemana) baldes["Últimos 7 días"].push(conversacion);
    else baldes.Anteriores.push(conversacion);
  }

  return etiquetas
    .filter((etiqueta) => baldes[etiqueta].length > 0)
    .map((etiqueta) => ({ etiqueta, conversaciones: baldes[etiqueta] }));
}
