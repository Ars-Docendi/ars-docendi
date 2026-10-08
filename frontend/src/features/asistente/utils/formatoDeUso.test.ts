import { describe, expect, it } from "vitest";

import { formatearEntero, formatearLatenciaMs, formatearUsd } from "./formatoDeUso";

describe("formatoDeUso", () => {
  it("formatearUsd muestra dos decimales y el símbolo de la moneda", () => {
    expect(formatearUsd(12.3)).toContain("12,30");
    expect(formatearUsd(0)).toContain("0,00");
  });

  it("formatearEntero redondea y separa miles al estilo es-AR", () => {
    expect(formatearEntero(1234.6)).toBe("1.235");
    expect(formatearEntero(0)).toBe("0");
  });

  it("formatearLatenciaMs convierte milisegundos a segundos con un decimal", () => {
    expect(formatearLatenciaMs(1500)).toBe("1,5 s");
    expect(formatearLatenciaMs(850)).toBe("0,9 s");
  });
});
