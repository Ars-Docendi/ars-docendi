import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
  claveDiaEnZona,
  datetimeLocalAIso,
  fechaHoraCompletaEnZona,
  horaCortaEnZona,
  horaHmsEnZona,
  limiteDesdeParaPeriodo,
  tituloDiaEnZona,
} from "./zonaHoraria";

// Reloj fijo: viernes 26/9/2026 22:16:48 en Buenos Aires (UTC-3) == 27/9 01:16:48Z.
const AHORA = new Date("2026-09-27T01:16:48.000Z");
const TZ_ORIGINAL = process.env.TZ;

describe.each(["UTC", "Europe/Madrid"])("con TZ del proceso = %s", (tz) => {
  beforeEach(() => {
    process.env.TZ = tz;
  });
  afterEach(() => {
    process.env.TZ = TZ_ORIGINAL;
  });

  it("horaCortaEnZona: Hoy/Ayer/d-m siempre en hora de Buenos Aires", () => {
    expect(horaCortaEnZona(AHORA, AHORA)).toBe("Hoy 22:16");

    const ayer2200 = new Date("2026-09-26T01:00:00.000Z"); // 25/9 22:00 ART
    expect(horaCortaEnZona(ayer2200, AHORA)).toBe("Ayer 22:00");

    const martes = new Date("2026-09-23T17:30:00.000Z"); // 23/9 14:30 ART
    expect(horaCortaEnZona(martes, AHORA)).toBe("23/9 14:30");
  });

  it("tituloDiaEnZona: Hoy/Ayer con día de la semana en minúscula, el resto capitalizado", () => {
    // El 26/9/2026 cae sábado en el calendario real (la muestra del canvas usa
    // «viernes» sólo como ilustración, no como fecha real).
    expect(tituloDiaEnZona(AHORA, AHORA)).toBe("Hoy · sábado 26 de septiembre");

    const ayer = new Date("2026-09-25T15:00:00.000Z"); // 25/9 12:00 ART
    expect(tituloDiaEnZona(ayer, AHORA)).toBe("Ayer · viernes 25 de septiembre");

    const martes = new Date("2026-09-23T17:30:00.000Z");
    expect(tituloDiaEnZona(martes, AHORA)).toBe("Miércoles 23 de septiembre");
  });

  it("horaHmsEnZona y fechaHoraCompletaEnZona resuelven en Buenos Aires", () => {
    expect(horaHmsEnZona(AHORA)).toBe("22:16:48");
    expect(fechaHoraCompletaEnZona(AHORA)).toBe("26/9/2026 22:16:48");
  });

  it("claveDiaEnZona agrupa por el día calendario de Buenos Aires, no el de TZ", () => {
    // 00:30 ART del 26/9 es 03:30Z, un día "distinto" en UTC puro si no se
    // convirtiera a la zona de la institución.
    const madrugada = new Date("2026-09-26T03:30:00.000Z");
    expect(claveDiaEnZona(madrugada)).toBe("2026-09-26");
  });

  it("limiteDesdeParaPeriodo: hoy/7d/30d/todo, contando el día de hoy", () => {
    expect(limiteDesdeParaPeriodo("todo", AHORA)).toBeUndefined();
    expect(limiteDesdeParaPeriodo("hoy", AHORA)).toBe(datetimeLocalAIso("2026-09-26T00:00"));
    // 7 días incluyendo hoy = arranca 6 días atrás (20/9).
    expect(limiteDesdeParaPeriodo("7d", AHORA)).toBe(datetimeLocalAIso("2026-09-20T00:00"));
    expect(limiteDesdeParaPeriodo("30d", AHORA)).toBe(datetimeLocalAIso("2026-08-28T00:00"));
  });

  it("datetimeLocalAIso interpreta la hora de pared como Buenos Aires (UTC-3)", () => {
    expect(datetimeLocalAIso("2026-09-26T00:00")).toBe("2026-09-26T03:00:00.000Z");
  });
});
