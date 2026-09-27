import { describe, expect, it } from "vitest";

import { construirCsvDeUso, nombreDeArchivoDeUso } from "./exportarUsoCsv";
import type { UsoAgregado } from "../types";

const FILA: UsoAgregado = {
  clave: "11111111-1111-4111-8111-111111111111",
  nombreParaMostrar: "Marina Díaz",
  turnos: 12,
  porEstado: { respondida: 12 },
  llamadasAlModelo: 20,
  tokensDeEntrada: 4000,
  tokensDeSalida: 1200,
  tokensDeCache: 300,
  latenciaPromedioMs: 850.4,
  latenciaP95Ms: 1500.6,
  proveedores: ["anthropic"],
  costoEstimado: 0.4219,
  esEstimado: true,
  turnosSinPrecio: 0,
  codigosDeRol: [],
  cupoEfectivo: null,
  origenDeCupo: null,
};

describe("construirCsvDeUso", () => {
  it("arma el encabezado «Usuario» y una fila por cada usuario, con las mismas columnas de la tabla", () => {
    const csv = construirCsvDeUso([FILA], true);
    const lineas = csv.replace(/^\uFEFF/, "").split("\r\n");

    expect(lineas[0]).toBe(
      "Usuario,Sesiones,Llamadas al modelo,Tokens de entrada,Tokens de salida,Tokens de caché," +
        "Latencia promedio (ms),Latencia p95 (ms),Costo estimado (USD),Turnos sin precio",
    );
    expect(lineas[1]).toBe("Marina Díaz,12,20,4000,1200,300,850,1501,0.42,0");
  });

  it("usa «Rol» como encabezado de la primera columna en la vista por rol", () => {
    const csv = construirCsvDeUso([{ ...FILA, nombreParaMostrar: null, clave: "docente" }], false);

    expect(
      csv
        .replace(/^\uFEFF/, "")
        .split("\r\n")[0]
        .startsWith("Rol,"),
    ).toBe(true);
    expect(csv).toContain("docente,12");
  });

  it("nunca más PII que el nombre para mostrar ya visible: sin correo, sin id crudo cuando hay nombre", () => {
    const csv = construirCsvDeUso([FILA], true);

    expect(csv).not.toContain(FILA.clave);
  });

  it("una coma o comilla en el nombre no rompe el CSV (RFC 4180)", () => {
    const csv = construirCsvDeUso([{ ...FILA, nombreParaMostrar: 'Ana, "Pepa" Paz' }], true);

    expect(csv).toContain('"Ana, ""Pepa"" Paz"');
  });

  it("lleva BOM UTF-8 al principio, para que Excel no rompa los acentos", () => {
    const csv = construirCsvDeUso([FILA], true);

    expect(csv.charCodeAt(0)).toBe(0xfeff);
  });
});

describe("nombreDeArchivoDeUso", () => {
  it("combina el período y la fecha en formato AAAA-MM-DD", () => {
    const nombre = nombreDeArchivoDeUso("semana", new Date("2026-09-27T15:00:00Z"));

    expect(nombre).toBe("uso-asistente-semana-2026-09-27.csv");
  });
});
