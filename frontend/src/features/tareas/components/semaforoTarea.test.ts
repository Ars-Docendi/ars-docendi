import { describe, it, expect } from "vitest";
import { estadoSemaforo, muestraSemaforo, porcentajeTranscurrido } from "./semaforoTarea";

describe("porcentajeTranscurrido", () => {
  it("calcula el % transcurrido entre inicio y fin", () => {
    const hoy = new Date("2026-01-03T00:00:00.000Z");
    const pct = porcentajeTranscurrido("2026-01-01", "2026-01-11", hoy);
    expect(pct).toBeCloseTo(20, 0);
  });
});

describe("estadoSemaforo", () => {
  it("verde por debajo del 50% transcurrido", () => {
    const hoy = new Date("2026-01-01T00:00:00.000Z");
    expect(estadoSemaforo("2025-12-24", "2026-01-11", hoy)).toBe("green");
  });

  it("amarillo entre 50% y 80% transcurrido", () => {
    const hoy = new Date("2026-01-07T00:00:00.000Z");
    expect(estadoSemaforo("2026-01-01", "2026-01-11", hoy)).toBe("yellow");
  });

  it("rojo desde el 80% transcurrido", () => {
    const hoy = new Date("2026-01-10T00:00:00.000Z");
    expect(estadoSemaforo("2026-01-01", "2026-01-11", hoy)).toBe("red");
  });

  it("rojo cuando ya está vencida", () => {
    const hoy = new Date("2026-02-01T00:00:00.000Z");
    expect(estadoSemaforo("2026-01-01", "2026-01-11", hoy)).toBe("red");
  });
});

describe("muestraSemaforo", () => {
  it("true para estados no terminales", () => {
    expect(muestraSemaforo("pendiente")).toBe(true);
    expect(muestraSemaforo("en_curso")).toBe(true);
    expect(muestraSemaforo("pausa")).toBe(true);
  });

  it("false para estados terminales", () => {
    expect(muestraSemaforo("resuelta")).toBe(false);
    expect(muestraSemaforo("cancelada")).toBe(false);
  });
});
