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
 * Agrupa conversaciones por fecha relativa de última actividad: Hoy, Ayer,
 * Últimos 7 días, Anteriores.
 *
 * ES EL PATRÓN QUE COMPARTEN CHATGPT, CLAUDE.AI, GEMINI Y COPILOT para una
 * lista de conversaciones (research previo al rediseño de esta lista: el
 * agrupamiento por fecha relativa, con encabezados fijos y sin bucket vacío,
 * es lo que hace escaneable una lista larga sin que cada fila tenga que
 * repetir su fecha completa).
 *
 * UN GRUPO VACÍO NO APARECE: nadie necesita un título de sección sin filas
 * debajo. Los límites son de calendario local —el día empieza a las 00:00,
 * no 24 horas atrás—, así que «ayer a las 23:59» cae en Ayer aunque hayan
 * pasado sólo minutos, y no en Hoy.
 *
 * El orden DENTRO de cada grupo es el que ya trae `conversaciones`: esta
 * función no reordena, sólo separa en baldes.
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
