import { describe, it, expect } from "vitest";

import { agruparPorFecha } from "./agruparPorFecha";
import type { ConversacionResumen } from "../types";

// ============================================================
// Agrupamiento por fecha relativa de la lista de conversaciones (rediseño de
// la UX del historial): Hoy / Ayer / Últimos 7 días / Anteriores, sin
// buckets vacíos.
// ============================================================

const AHORA = new Date("2026-06-15T12:00:00");

function conversacion(id: string, ultimaActividad: string): ConversacionResumen {
  return { id, titulo: `Conversación ${id}`, creadoEn: ultimaActividad, ultimaActividad };
}

describe("agruparPorFecha", () => {
  it("sin conversaciones no devuelve ningún grupo", () => {
    expect(agruparPorFecha([], AHORA)).toEqual([]);
  });

  it("agrupa en Hoy, Ayer, Últimos 7 días y Anteriores", () => {
    const hoy = conversacion("1", "2026-06-15T08:00:00");
    const ayer = conversacion("2", "2026-06-14T23:59:00");
    const haceCincoDias = conversacion("3", "2026-06-10T10:00:00");
    const haceUnMes = conversacion("4", "2026-05-01T10:00:00");

    const grupos = agruparPorFecha([hoy, ayer, haceCincoDias, haceUnMes], AHORA);

    expect(grupos).toEqual([
      { etiqueta: "Hoy", conversaciones: [hoy] },
      { etiqueta: "Ayer", conversaciones: [ayer] },
      { etiqueta: "Últimos 7 días", conversaciones: [haceCincoDias] },
      { etiqueta: "Anteriores", conversaciones: [haceUnMes] },
    ]);
  });

  it("un grupo sin conversaciones no aparece", () => {
    const soloHoy = conversacion("1", "2026-06-15T08:00:00");

    const grupos = agruparPorFecha([soloHoy], AHORA);

    expect(grupos).toEqual([{ etiqueta: "Hoy", conversaciones: [soloHoy] }]);
  });

  it("el límite del día es de calendario local, no 24hs atrás", () => {
    // «Ayer a las 23:59» está a 12 minutos de «hoy a las 12:00», y sin
    // embargo cae en Ayer: el corte es la medianoche, no una ventana móvil.
    const finDeAyer = conversacion("1", "2026-06-14T23:59:00");

    const grupos = agruparPorFecha([finDeAyer], AHORA);

    expect(grupos).toEqual([{ etiqueta: "Ayer", conversaciones: [finDeAyer] }]);
  });

  it("conserva el orden recibido dentro de cada grupo", () => {
    const primera = conversacion("1", "2026-06-15T08:00:00");
    const segunda = conversacion("2", "2026-06-15T09:00:00");

    const grupos = agruparPorFecha([segunda, primera], AHORA);

    expect(grupos[0].conversaciones).toEqual([segunda, primera]);
  });
});
