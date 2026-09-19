import { describe, it, expect } from "vitest";

import { tablaComoTsv } from "./portapapeles";
import type { ColumnaDelResultado } from "../types";

// ============================================================
// La tabla como texto para pegar en una planilla: una fila de cabecera y una
// por resultado, con las celdas separadas por tabulaciones.
// ============================================================

function columnas(...nombres: string[]): ColumnaDelResultado[] {
  return nombres.map((nombre) => ({ nombre, sensible: false }));
}

describe("tablaComoTsv", () => {
  it("produce la cabecera y una fila por resultado, separadas por tabulaciones", () => {
    const tsv = tablaComoTsv(columnas("apellido", "documento", "horas"), [
      ["Gómez", "28341567", 42],
      ["Pérez", "30111222", 8],
    ]);

    expect(tsv).toBe("apellido\tdocumento\thoras\nGómez\t28341567\t42\nPérez\t30111222\t8");
  });

  it("copia las celdas como se ven en la tabla: vacío como raya, booleanos como Sí y No", () => {
    expect(tablaComoTsv(columnas("a", "b", "c"), [[null, true, false]])).toBe("a\tb\tc\n—\tSí\tNo");
  });

  it("un tabulador o un salto de línea dentro de una celda no rompe la grilla", () => {
    expect(tablaComoTsv(columnas("nota"), [["línea 1\nlínea\t2"]])).toBe("nota\nlínea 1 línea 2");
  });
});
