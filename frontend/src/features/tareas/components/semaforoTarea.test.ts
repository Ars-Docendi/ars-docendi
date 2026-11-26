import { describe, it, expect } from "vitest";
import {
  estadoSemaforo,
  etiquetaSemaforo,
  muestraSemaforo,
  porcentajeTranscurrido,
} from "./semaforoTarea";

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

describe("etiquetaSemaforo", () => {
  // Hora local: a las 22 h en Argentina ya es el día siguiente en UTC.
  const hoy = new Date(2026, 0, 10, 22, 0);

  it("no dice vencido si la tarea vence hoy o mañana, aunque el semáforo esté en rojo", () => {
    expect(estadoSemaforo("2026-01-01", "2026-01-10", hoy)).toBe("red");
    expect(etiquetaSemaforo("2026-01-01", "2026-01-10", hoy)).toBe("Próximo a vencer");
    expect(estadoSemaforo("2026-01-01", "2026-01-11", hoy)).toBe("red");
    expect(etiquetaSemaforo("2026-01-01", "2026-01-11", hoy)).toBe("Próximo a vencer");
  });

  it("dice vencido recién cuando la fecha de fin ya pasó", () => {
    expect(etiquetaSemaforo("2026-01-01", "2026-01-09", hoy)).toBe("Vencido");
  });

  it("una tarea de un solo día no está vencida ese mismo día", () => {
    expect(etiquetaSemaforo("2026-01-10", "2026-01-10", hoy)).toBe("Próximo a vencer");
  });

  it("acompaña a los otros estados del semáforo", () => {
    expect(etiquetaSemaforo("2026-01-10", "2026-02-10", hoy)).toBe("A tiempo");
    expect(etiquetaSemaforo("2026-01-01", "2026-01-17", hoy)).toBe("Próximo a vencer");
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
