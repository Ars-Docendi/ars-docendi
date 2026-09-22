import { describe, it, expect } from "vitest";
import {
  aplicarFiltrosColumnas,
  FILTROS_COLUMNAS_INICIALES,
  opcionesColumnasTareas,
} from "./filtrosTareas";
import type { Tarea } from "../types";

function tarea(overrides: Partial<Tarea> = {}): Tarea {
  return {
    id: overrides.id ?? "t1",
    numero: 1,
    titulo: "Actualizar el padrón",
    descripcion: "",
    fechaInicio: "2026-01-01",
    fechaFin: "2026-01-10",
    prioridad: "media",
    estado: "pendiente",
    porcentajeAvance: 0,
    responsable: { nombre: "G. Ruiz", rol: "Jefe de Cátedra" },
    creadoPor: { nombre: "L. Fernández", rol: "Secretaría" },
    comentarios: [],
    historial: [],
    ...overrides,
  };
}

describe("opcionesColumnasTareas", () => {
  it("deriva autores y responsables únicos y ordenados de las tareas visibles", () => {
    const tareas = [
      tarea({ id: "a", creadoPor: { nombre: "R. Sosa", rol: "Decanato" } }),
      tarea({ id: "b", creadoPor: { nombre: "L. Fernández", rol: "Secretaría" } }),
      tarea({ id: "c", creadoPor: { nombre: "L. Fernández", rol: "Secretaría" } }),
    ];
    const opciones = opcionesColumnasTareas(tareas);
    expect(opciones.autores).toEqual(["L. Fernández", "R. Sosa"]);
  });
});

describe("aplicarFiltrosColumnas", () => {
  it("sin filtros activos no acota nada", () => {
    const tareas = [tarea({ id: "a" }), tarea({ id: "b" })];
    expect(aplicarFiltrosColumnas(tareas, FILTROS_COLUMNAS_INICIALES)).toHaveLength(2);
  });

  it("filtra por título (texto, sin distinguir mayúsculas/acentos)", () => {
    const tareas = [
      tarea({ id: "a", titulo: "Revisar aulas" }),
      tarea({ id: "b", titulo: "Cargar novedades" }),
    ];
    const resultado = aplicarFiltrosColumnas(tareas, {
      ...FILTROS_COLUMNAS_INICIALES,
      titulo: "REVISAR",
    });
    expect(resultado.map((t) => t.id)).toEqual(["a"]);
  });

  it("filtra por Autor con selección múltiple (checkbox)", () => {
    const tareas = [
      tarea({ id: "a", creadoPor: { nombre: "L. Fernández", rol: "Secretaría" } }),
      tarea({ id: "b", creadoPor: { nombre: "R. Sosa", rol: "Decanato" } }),
      tarea({ id: "c", creadoPor: { nombre: "P. Gómez", rol: "Administración" } }),
    ];
    const resultado = aplicarFiltrosColumnas(tareas, {
      ...FILTROS_COLUMNAS_INICIALES,
      autor: ["L. Fernández", "P. Gómez"],
    });
    expect(resultado.map((t) => t.id)).toEqual(["a", "c"]);
  });

  it("filtra por Fecha de Inicio como texto sobre la fecha formateada", () => {
    const tareas = [
      tarea({ id: "a", fechaInicio: "2026-03-05" }),
      tarea({ id: "b", fechaInicio: "2026-04-10" }),
    ];
    const resultado = aplicarFiltrosColumnas(tareas, {
      ...FILTROS_COLUMNAS_INICIALES,
      fechaInicio: "03/2026",
    });
    expect(resultado.map((t) => t.id)).toEqual(["a"]);
  });

  it("filtra por % Avance exacto", () => {
    const tareas = [
      tarea({ id: "a", porcentajeAvance: 40 }),
      tarea({ id: "b", porcentajeAvance: 90 }),
    ];
    const resultado = aplicarFiltrosColumnas(tareas, {
      ...FILTROS_COLUMNAS_INICIALES,
      avance: "40",
    });
    expect(resultado.map((t) => t.id)).toEqual(["a"]);
  });

  it("filtra por Estado con selección múltiple", () => {
    const tareas = [
      tarea({ id: "a", estado: "pendiente" }),
      tarea({ id: "b", estado: "en_curso" }),
      tarea({ id: "c", estado: "cancelada" }),
    ];
    const resultado = aplicarFiltrosColumnas(tareas, {
      ...FILTROS_COLUMNAS_INICIALES,
      estado: ["pendiente", "en_curso"],
    });
    expect(resultado.map((t) => t.id)).toEqual(["a", "b"]);
  });
});
