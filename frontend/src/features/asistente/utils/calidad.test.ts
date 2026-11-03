import { describe, expect, it } from "vitest";

import { detalleDeProblema, formatearDia, formatearTasa, textoDeBarra } from "./calidad";

describe("calidad — formato es-AR", () => {
  it("una tasa nula es un guion, nunca 0", () => {
    expect(formatearTasa(null)).toBe("—");
    expect(formatearTasa(undefined)).toBe("—");
  });

  it("el día no se corre por zona horaria", () => {
    expect(formatearDia("2026-10-01")).toBe("01/10");
  });
});

describe("calidad — gráfico", () => {
  it("el texto de la barra distingue un día sin votos", () => {
    const sinVotos = { dia: "2026-10-01", turnos: 3, votos: 0, negativos: 0, tasaNegativa: null };
    expect(textoDeBarra(sinVotos, false)).toBe("01/10: sin valoraciones");
    const conVotos = { dia: "2026-10-01", turnos: 3, votos: 4, negativos: 1, tasaNegativa: 0.25 };
    expect(textoDeBarra(conVotos, true)).toBe("Semana del 01/10: 25,0% (1 de 4)");
  });
});

describe("calidad — detalle de un problema", () => {
  it("un fallo muestra su causa y una reemplazada su explicación", () => {
    expect(detalleDeProblema("fallo", "timeout_proveedor")).toBe("El modelo tardó demasiado");
    expect(detalleDeProblema("reemplazo", null)).toBe("El usuario editó y reenvió la pregunta");
  });

  it("un código desconocido se muestra tal cual en vez de esconderse", () => {
    expect(detalleDeProblema("fallo", "causa_nueva")).toBe("causa_nueva");
  });
});
