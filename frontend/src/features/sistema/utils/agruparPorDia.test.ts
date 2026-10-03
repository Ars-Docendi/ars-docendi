import { describe, expect, it } from "vitest";

import { agruparPorDia } from "./agruparPorDia";

const AHORA = new Date("2026-09-27T01:16:48.000Z"); // Hoy 26/9 22:16 ART

interface Evento {
  id: number;
  cambiadoEn: string;
}

describe("agruparPorDia", () => {
  it("agrupa por día calendario en Buenos Aires, en el orden de llegada", () => {
    const eventos: Evento[] = [
      { id: 1, cambiadoEn: "2026-09-27T01:10:00.000Z" }, // Hoy
      { id: 2, cambiadoEn: "2026-09-27T00:10:00.000Z" }, // Hoy (21:10 ART)
      { id: 3, cambiadoEn: "2026-09-26T01:00:00.000Z" }, // Ayer (25/9 22:00 ART)
      { id: 4, cambiadoEn: "2026-09-23T17:30:00.000Z" }, // Martes
    ];

    const grupos = agruparPorDia(eventos, (e) => e.cambiadoEn, AHORA);

    expect(grupos.map((g) => g.titulo)).toEqual([
      "Hoy · sábado 26 de septiembre",
      "Ayer · viernes 25 de septiembre",
      "Miércoles 23 de septiembre",
    ]);
    expect(grupos[0].elementos.map((e) => e.id)).toEqual([1, 2]);
    expect(grupos[1].elementos.map((e) => e.id)).toEqual([3]);
    expect(grupos[2].elementos.map((e) => e.id)).toEqual([4]);
  });

  it("una lista vacía produce cero grupos", () => {
    expect(agruparPorDia<Evento>([], (e) => e.cambiadoEn, AHORA)).toEqual([]);
  });
});
