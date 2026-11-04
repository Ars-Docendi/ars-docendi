import { describe, expect, it } from "vitest";
import {
  fechaISOATexto,
  fechaTextoAISO,
  formatearFecha,
  formatearHora,
  horaTextoValida,
} from "./camposFechaHora";

describe("camposFechaHora", () => {
  it("inserts slashes while typing a date and does not re-insert one that was just deleted", () => {
    expect(formatearFecha("25122026")).toBe("25/12/2026");
    expect(formatearFecha("3112")).toBe("31/12/");
    expect(formatearFecha("31/12", true)).toBe("31/12");
    expect(formatearFecha("31/1", true)).toBe("31/1");
  });

  it("converts a pasted ISO date", () => {
    expect(formatearFecha("2027-03-05")).toBe("05/03/2027");
  });

  it("inserts the colon while typing a time", () => {
    expect(formatearHora("0930")).toBe("09:30");
    expect(formatearHora("09")).toBe("09:");
    expect(formatearHora("09:", true)).toBe("09");
  });

  it("converts a valid date to ISO and rejects impossible ones", () => {
    expect(fechaTextoAISO("20/11/2026")).toBe("2026-11-20");
    expect(fechaTextoAISO("29/02/2028")).toBe("2028-02-29");
    expect(fechaTextoAISO("29/02/2026")).toBeNull();
    expect(fechaTextoAISO("31/04/2026")).toBeNull();
    expect(fechaTextoAISO("99/99/2026")).toBeNull();
    expect(fechaTextoAISO("20/11/1899")).toBeNull();
    expect(fechaTextoAISO("20/11/26")).toBeNull();
  });

  it("converts an ISO date back to text", () => {
    expect(fechaISOATexto("2026-11-20")).toBe("20/11/2026");
    expect(fechaISOATexto("")).toBe("");
  });

  it("accepts only 24 h times", () => {
    expect(horaTextoValida("23:59")).toBe("23:59");
    expect(horaTextoValida("24:00")).toBeNull();
    expect(horaTextoValida("10:60")).toBeNull();
    expect(horaTextoValida("9:30")).toBeNull();
  });
});
