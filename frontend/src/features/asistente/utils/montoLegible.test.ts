import { describe, expect, it } from "vitest";

import { montoLegible } from "./modeloDelAsistente";

describe("montoLegible", () => {
  it.each([
    [0.6, "0,60"],
    [0.15, "0,15"],
    [3, "3,00"],
    [15, "15,00"],
    [0.000125, "0,000125"],
    [1234.567891, "1234,567891"],
  ])("%s se muestra «%s»", (valor, esperado) => {
    expect(montoLegible(valor)).toBe(esperado);
  });
});
