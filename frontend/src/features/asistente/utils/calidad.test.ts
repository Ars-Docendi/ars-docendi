import { describe, expect, it } from "vitest";

import {
  detalleDeProblema,
  formatearDia,
  formatearPuntos,
  formatearTasa,
  maximoDeEscala,
  textoDeBarra,
} from "./calidad";

describe("calidad — formato es-AR", () => {
  it("formatea una fracción con una decimal y coma", () => {
    expect(formatearTasa(0.1234)).toBe("12,3%");
    expect(formatearTasa(0)).toBe("0,0%");
    expect(formatearTasa(1)).toBe("100,0%");
  });

  it("una tasa nula es un guion, nunca 0", () => {
    expect(formatearTasa(null)).toBe("—");
    expect(formatearTasa(undefined)).toBe("—");
  });

  it("los puntos de variación van en valor absoluto", () => {
    expect(formatearPuntos(-0.052)).toBe("5,2");
  });

  it("el día no se corre por zona horaria", () => {
    expect(formatearDia("2026-10-01")).toBe("01/10");
  });
});

describe("calidad — gráfico", () => {
  it("la escala sube de a 5 puntos y nunca baja de 5", () => {
    const punto = (t: number | null) => ({
      dia: "2026-10-01",
      turnos: 1,
      votos: 1,
      negativos: 0,
      tasaNegativa: t,
    });
    expect(maximoDeEscala([punto(0.01)])).toBe(5);
    expect(maximoDeEscala([punto(0.121)])).toBe(15);
    expect(maximoDeEscala([punto(null)])).toBe(5);
    expect(maximoDeEscala([])).toBe(5);
  });

  it("el texto de la barra distingue un día sin votos", () => {
    const sinVotos = { dia: "2026-10-01", turnos: 3, votos: 0, negativos: 0, tasaNegativa: null };
    expect(textoDeBarra(sinVotos, false)).toBe("01/10: sin valoraciones");
    const conVotos = { dia: "2026-10-01", turnos: 3, votos: 4, negativos: 1, tasaNegativa: 0.25 };
    expect(textoDeBarra(conVotos, true)).toBe("Semana del 01/10: 25,0% (1 de 4)");
  });
});

describe("calidad — detalle de un problema", () => {
  it("traduce los motivos de un voto negativo, uno por uno", () => {
    expect(detalleDeProblema("negativa", "datos_incorrectos,otro")).toBe("Datos incorrectos, Otro");
  });

  it("un voto sin motivo dice que se omitió", () => {
    expect(detalleDeProblema("negativa", null)).toBe("Sin motivo (omitió)");
  });

  it("un fallo muestra su causa y una reemplazada su explicación", () => {
    expect(detalleDeProblema("fallo", "timeout_proveedor")).toBe("El modelo tardó demasiado");
    expect(detalleDeProblema("reemplazo", null)).toBe("El usuario editó y reenvió la pregunta");
  });

  it("un código desconocido se muestra tal cual en vez de esconderse", () => {
    expect(detalleDeProblema("fallo", "causa_nueva")).toBe("causa_nueva");
  });
});
