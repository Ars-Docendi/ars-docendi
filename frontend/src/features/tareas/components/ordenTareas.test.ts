import { describe, it, expect } from "vitest";
import { ordenarTareas, siguienteOrden } from "./ordenTareas";
import type { Tarea } from "../types";

function tarea(overrides: Partial<Tarea> = {}): Tarea {
  return {
    id: overrides.id ?? "t1",
    numero: 1,
    titulo: "Tarea",
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

describe("ordenarTareas", () => {
  it("con orden null (sin elegir columna) ordena por Fecha Inicio ascendente", () => {
    const tareas = [
      tarea({ id: "b", fechaInicio: "2026-03-01" }),
      tarea({ id: "a", fechaInicio: "2026-01-01" }),
      tarea({ id: "c", fechaInicio: "2026-02-01" }),
    ];
    const resultado = ordenarTareas(tareas, null);
    expect(resultado.map((t) => t.id)).toEqual(["a", "c", "b"]);
  });

  it("no muta el array original", () => {
    const tareas = [
      tarea({ id: "b", fechaInicio: "2026-03-01" }),
      tarea({ id: "a", fechaInicio: "2026-01-01" }),
    ];
    const original = [...tareas];
    ordenarTareas(tareas, null);
    expect(tareas).toEqual(original);
  });

  it("ordena por número descendente", () => {
    const tareas = [
      tarea({ id: "a", numero: 1 }),
      tarea({ id: "b", numero: 3 }),
      tarea({ id: "c", numero: 2 }),
    ];
    const resultado = ordenarTareas(tareas, { columna: "numero", direccion: "desc" });
    expect(resultado.map((t) => t.id)).toEqual(["b", "c", "a"]);
  });

  it("ordena por prioridad respetando el rango (no alfabético)", () => {
    const tareas = [
      tarea({ id: "media", prioridad: "media" }),
      tarea({ id: "baja", prioridad: "baja" }),
      tarea({ id: "alta", prioridad: "alta" }),
    ];
    const resultado = ordenarTareas(tareas, { columna: "prioridad", direccion: "asc" });
    expect(resultado.map((t) => t.id)).toEqual(["baja", "media", "alta"]);
  });

  it("ordena por título alfabéticamente sin distinguir mayúsculas", () => {
    const tareas = [tarea({ id: "z", titulo: "Zebra" }), tarea({ id: "a", titulo: "ana" })];
    const resultado = ordenarTareas(tareas, { columna: "titulo", direccion: "asc" });
    expect(resultado.map((t) => t.id)).toEqual(["a", "z"]);
  });
});

describe("siguienteOrden", () => {
  it("clickear una columna nueva arranca en ascendente", () => {
    expect(siguienteOrden(null, "titulo")).toEqual({ columna: "titulo", direccion: "asc" });
  });

  it("clickear la misma columna alterna asc → desc → null (vuelve al default)", () => {
    const primero = siguienteOrden(null, "titulo");
    const segundo = siguienteOrden(primero, "titulo");
    expect(segundo).toEqual({ columna: "titulo", direccion: "desc" });
    const tercero = siguienteOrden(segundo, "titulo");
    expect(tercero).toBeNull();
  });

  it("clickear otra columna mientras hay una activa reinicia en ascendente", () => {
    const activa = siguienteOrden(null, "titulo");
    expect(siguienteOrden(activa, "estado")).toEqual({ columna: "estado", direccion: "asc" });
  });
});
