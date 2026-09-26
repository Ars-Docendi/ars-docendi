import { describe, it, expect } from "vitest";

import { ordenarFilas } from "./ordenarFilas";

describe("ordenarFilas", () => {
  it("sin orden devuelve los índices originales, sin tocar nada", () => {
    const filas = [["c"], ["a"], ["b"]];

    expect(ordenarFilas(filas, null)).toEqual([0, 1, 2]);
  });

  // ============================================================
  // Números: numérico, no lexical.
  // ============================================================

  it("ordena números numéricamente y no lexicalmente", () => {
    const filas = [[100], [9], [10]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "ascendente" });

    expect(orden.map((i) => filas[i][0])).toEqual([9, 10, 100]);
  });

  it("descendente invierte el orden numérico", () => {
    const filas = [[9], [100], [10]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "descendente" });

    expect(orden.map((i) => filas[i][0])).toEqual([100, 10, 9]);
  });

  it("una columna con un solo valor no numérico deja de tratarse como numérica", () => {
    // Sólo se infiere «numero» cuando TODAS las celdas no vacías lo son.
    const filas = [["9"], ["10"], ["N/D"]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "ascendente" });

    // Como texto, "10" < "9" lexicalmente en un collator sin `numeric`... pero
    // el collator de esta función SIEMPRE usa `numeric: true`, así que incluso
    // como texto compara "9" antes que "10". Lo que se prueba acá es que no
    // revienta con un valor no numérico mezclado, y que ese valor entra en la
    // comparación de texto en vez de `NaN`.
    expect(orden.map((i) => filas[i][0])).toEqual(["9", "10", "N/D"]);
  });

  // ============================================================
  // Fechas: cronológico.
  // ============================================================

  it("ordena fechas ISO cronológicamente", () => {
    const filas = [["2026-03-01"], ["2025-12-15"], ["2026-01-10"]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "ascendente" });

    expect(orden.map((i) => filas[i][0])).toEqual(["2025-12-15", "2026-01-10", "2026-03-01"]);
  });

  it("ordena timestamps ISO con hora cronológicamente", () => {
    const filas = [["2026-01-10T23:00:00Z"], ["2026-01-10T05:00:00Z"], ["2026-01-09T12:00:00Z"]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "ascendente" });

    expect(orden.map((i) => filas[i][0])).toEqual([
      "2026-01-09T12:00:00Z",
      "2026-01-10T05:00:00Z",
      "2026-01-10T23:00:00Z",
    ]);
  });

  // ============================================================
  // Vacíos: siempre al final, en las dos direcciones.
  // ============================================================

  it("las celdas vacías quedan al final ascendiendo", () => {
    const filas = [[null], [3], [1], [undefined], [2]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "ascendente" });

    expect(orden.map((i) => filas[i][0])).toEqual([1, 2, 3, null, undefined]);
  });

  it("las celdas vacías quedan al final también descendiendo", () => {
    const filas = [[null], [3], [1], [undefined], [2]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "descendente" });

    expect(orden.map((i) => filas[i][0])).toEqual([3, 2, 1, null, undefined]);
  });

  // ============================================================
  // Texto en español, y una celda enmascarada.
  // ============================================================

  it("ordena texto en español sin distinguir mayúsculas ni acentos", () => {
    const filas = [["Álvarez"], ["baez"], ["Cortez"]];

    const orden = ordenarFilas(filas, {
      columna: 0,
      direccion: "ascendente",
    });

    expect(orden.map((i) => filas[i][0])).toEqual(["Álvarez", "baez", "Cortez"]);
  });

  it("una celda enmascarada ordena por lo que se ve, no por nada subyacente", () => {
    const filas = [["«documento 3»"], ["«documento 1»"], ["«documento 2»"]];

    const orden = ordenarFilas(filas, {
      columna: 0,
      direccion: "ascendente",
    });

    expect(orden.map((i) => filas[i][0])).toEqual([
      "«documento 1»",
      "«documento 2»",
      "«documento 3»",
    ]);
  });

  // ============================================================
  // Estabilidad: claves iguales conservan el orden original.
  // ============================================================

  it("es estable: filas con la misma clave conservan su orden relativo original", () => {
    const filas = [
      ["Materias", "a"],
      ["Materias", "b"],
      ["Docentes", "c"],
      ["Materias", "d"],
    ];

    const orden = ordenarFilas(filas, {
      columna: 0,
      direccion: "ascendente",
    });

    // "Docentes" antes que "Materias"; dentro de "Materias", a, b, d en ese orden.
    expect(orden.map((i) => filas[i][1])).toEqual(["c", "a", "b", "d"]);
  });

  it("la estabilidad no depende de la dirección: descendente también conserva el orden relativo", () => {
    const filas = [
      ["Materias", "a"],
      ["Materias", "b"],
      ["Docentes", "c"],
      ["Materias", "d"],
    ];

    const orden = ordenarFilas(filas, {
      columna: 0,
      direccion: "descendente",
    });

    // "Materias" antes que "Docentes"; dentro de "Materias", sigue a, b, d.
    expect(orden.map((i) => filas[i][1])).toEqual(["a", "b", "d", "c"]);
  });

  // ============================================================
  // Los índices son sobre la fila original: `vinculos` sigue siendo válido.
  // ============================================================

  it("devuelve índices originales y no una copia de las filas", () => {
    const filas = [["z"], ["a"]];

    const orden = ordenarFilas(filas, { columna: 0, direccion: "ascendente" });

    expect(orden).toEqual([1, 0]);
  });
});
