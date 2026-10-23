import { describe, it, expect } from "vitest";

import { ordenarFilas } from "./ordenarFilas";
import { nombreDelArchivoCsv, tablaComoCsv, tablaComoTsv } from "./portapapeles";
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

// ============================================================
// tablaComoCsv: RFC 4180 quoting, formula-injection guarding, BOM and CRLF,
// and the truncation disclosure — see asistente-exportacion-csv's spec.
// ============================================================

describe("tablaComoCsv", () => {
  const BOM = "﻿";

  it("starts with a UTF-8 byte-order mark and uses CRLF line endings", () => {
    const csv = tablaComoCsv(columnas("nombre"), [["Ana"]], false);

    expect(csv.startsWith(BOM)).toBe(true);
    expect(csv).toBe(`${BOM}nombre\r\nAna`);
  });

  it("quotes a cell containing a comma", () => {
    const csv = tablaComoCsv(columnas("apellido"), [["Gómez, hijo"]], false);

    expect(csv).toBe(`${BOM}apellido\r\n"Gómez, hijo"`);
  });

  it("doubles internal double quotes and wraps the cell", () => {
    const csv = tablaComoCsv(columnas("apodo"), [['El "Colo"']], false);

    expect(csv).toBe(`${BOM}apodo\r\n"El ""Colo"""`);
  });

  it("neutralizes a cell that starts with a formula character", () => {
    const csv = tablaComoCsv(columnas("nota"), [["=SUM(A1:A2)"]], false);

    expect(csv).toBe(`${BOM}nota\r\n'=SUM(A1:A2)`);
  });

  it("neutralizes a negative number the same way as any other leading-dash cell", () => {
    const csv = tablaComoCsv(columnas("saldo"), [[-42]], false);

    expect(csv).toBe(`${BOM}saldo\r\n'-42`);
  });

  it("round-trips accented characters without altering them", () => {
    const csv = tablaComoCsv(columnas("carrera"), [["Ingeniería en Informática"]], false);

    expect(csv).toContain("Ingeniería en Informática");
  });

  it("appends a trailing notice row, stating no count, when the result is truncated", () => {
    const csv = tablaComoCsv(columnas("id"), [[1], [2]], true);
    const filas = csv.slice(BOM.length).split("\r\n");

    expect(filas.at(-1)).toBe(
      "Resultado truncado: se muestran menos filas de las que cumplen la consulta.",
    );
    expect(csv).not.toMatch(/\d+ fila/);
  });

  it("adds no trailing row when the result is not truncated", () => {
    const csv = tablaComoCsv(columnas("id"), [[1], [2]], false);
    const filas = csv.slice(BOM.length).split("\r\n");

    expect(filas).toEqual(["id", "1", "2"]);
  });
});

// ============================================================
// El orden mostrado, no el original: `tablaComoCsv`/`tablaComoTsv` no
// ordenan nada — reciben las filas YA en el orden a exportar
// (`ordenarFilas`, ver TablaDeResultado y TablaAmpliada), y lo que se prueba
// acá es exactamente ese contrato de "recibe el orden ya resuelto"
// (asistente-tabla-de-resultado, asistente-exportacion-csv).
// ============================================================

describe("el orden mostrado se exporta, no el original", () => {
  const filasOriginales = [
    ["Pérez", 3],
    ["Gómez", 1],
    ["Alonso", 2],
  ];

  function ordenadas(direccion: "ascendente" | "descendente") {
    const orden = ordenarFilas(filasOriginales, {
      columna: 1,
      direccion,
    });
    return orden.map((indice) => filasOriginales[indice]);
  }

  it("tablaComoCsv exporta ascendente cuando ése es el orden mostrado", () => {
    const csv = tablaComoCsv(columnas("docente", "horas"), ordenadas("ascendente"), false);

    expect(csv).toContain("Gómez,1\r\nAlonso,2\r\nPérez,3");
  });

  it("tablaComoCsv exporta descendente cuando ése es el orden mostrado", () => {
    const csv = tablaComoCsv(columnas("docente", "horas"), ordenadas("descendente"), false);

    expect(csv).toContain("Pérez,3\r\nAlonso,2\r\nGómez,1");
  });

  it("tablaComoTsv exporta en el mismo orden mostrado", () => {
    const tsv = tablaComoTsv(columnas("docente", "horas"), ordenadas("descendente"));

    expect(tsv).toBe("docente\thoras\nPérez\t3\nAlonso\t2\nGómez\t1");
  });

  it("sin ordenar, se exporta el orden original — ni tablaComoCsv ni tablaComoTsv lo tocan", () => {
    const orden = ordenarFilas(filasOriginales, null);
    const enOrdenOriginal = orden.map((indice) => filasOriginales[indice]);

    expect(tablaComoTsv(columnas("docente", "horas"), enOrdenOriginal)).toBe(
      "docente\thoras\nPérez\t3\nGómez\t1\nAlonso\t2",
    );
  });
});

describe("nombreDelArchivoCsv", () => {
  const hilo = "abcdef12-0000-4000-8000-000000000001";
  const fecha = new Date("2026-09-25T10:00:00Z");

  it("follows the asistente-resultado-<hilo-corto>-<fecha-ISO>.csv pattern", () => {
    expect(nombreDelArchivoCsv(hilo, fecha, false)).toBe(
      "asistente-resultado-abcdef12-2026-09-25.csv",
    );
  });

  it("adds a -truncado suffix when the result was truncated", () => {
    expect(nombreDelArchivoCsv(hilo, fecha, true)).toBe(
      "asistente-resultado-abcdef12-2026-09-25-truncado.csv",
    );
  });

  it("contains only ASCII-safe characters", () => {
    expect(nombreDelArchivoCsv(hilo, fecha, true)).toMatch(/^[A-Za-z0-9._-]+$/);
  });
});
