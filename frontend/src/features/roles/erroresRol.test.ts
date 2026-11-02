import { describe, expect, it } from "vitest";
import { mensajeConflictoRol } from "./erroresRol";

describe("conflictos de rol", () => {
  it.each([
    ["identity-role-name-conflict", /otro rol activo.*nombre/i],
    ["identity-role-scope-conflict", /asignaciones vigentes/i],
    ["concurrency-conflict", /actualizá.*volvé/i],
  ])("traduce %s sin reintento ciego", (codigo, esperado) => {
    expect(
      mensajeConflictoRol({
        isAxiosError: true,
        response: { status: 409, data: { type: `https://ars-docendi.test/errors/${codigo}` } },
      }),
    ).toMatch(esperado);
  });
});
