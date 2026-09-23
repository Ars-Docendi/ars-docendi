import { describe, it, expect } from "vitest";
import { agruparTareasPorProyecto } from "./agrupacionProyectos";
import type { Proyecto, Tarea } from "../types";

function tarea(overrides: Partial<Tarea> = {}): Tarea {
  return {
    id: overrides.id ?? "t1",
    numero: 1,
    titulo: "Tarea",
    descripcion: "",
    fechaInicio: "2026-01-01",
    fechaFin: "2026-01-10",
    prioridad: "media",
    tipo: "administrativa",
    estado: "pendiente",
    porcentajeAvance: 0,
    responsable: { nombre: "G. Ruiz", rol: "Jefe de Cátedra" },
    creadoPor: { nombre: "L. Fernández", rol: "Secretaría Académica" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
    ...overrides,
  };
}

function proyecto(overrides: Partial<Proyecto> = {}): Proyecto {
  return {
    id: overrides.id ?? "p1",
    numero: 1,
    nombre: "Proyecto",
    descripcion: "",
    fechaInicio: "2026-01-01",
    fechaFin: "2026-06-01",
    estado: "abierto",
    responsable: { nombre: "R. Sosa", rol: "Decanato" },
    ...overrides,
  };
}

describe("agruparTareasPorProyecto", () => {
  it('"Generales" aparece primero, incluso vacío', () => {
    const resultado = agruparTareasPorProyecto([], []);
    expect(resultado).toHaveLength(1);
    expect(resultado[0].proyecto).toBeNull();
    expect(resultado[0].tareas).toEqual([]);
  });

  it("agrupa las tareas sin proyecto en el cuadro fijo", () => {
    const tareas = [tarea({ id: "a" }), tarea({ id: "b", proyectoId: "p1" })];
    const proyectos = [proyecto({ id: "p1" })];
    const resultado = agruparTareasPorProyecto(tareas, proyectos);
    expect(resultado[0].proyecto).toBeNull();
    expect(resultado[0].tareas.map((t) => t.id)).toEqual(["a"]);
  });

  it("un Proyecto sin tareas no genera cuadro", () => {
    const proyectos = [proyecto({ id: "p1" })];
    const resultado = agruparTareasPorProyecto([], proyectos);
    expect(resultado).toHaveLength(1); // solo "Generales"
  });

  it("un Proyecto Finalizado no genera cuadro aunque tenga tareas", () => {
    const tareas = [tarea({ id: "a", proyectoId: "p1" })];
    const proyectos = [proyecto({ id: "p1", estado: "finalizado" })];
    const resultado = agruparTareasPorProyecto(tareas, proyectos);
    expect(resultado).toHaveLength(1);
  });

  it("un Proyecto Cancelado no genera cuadro aunque tenga tareas", () => {
    const tareas = [tarea({ id: "a", proyectoId: "p1" })];
    const proyectos = [proyecto({ id: "p1", estado: "cancelado" })];
    const resultado = agruparTareasPorProyecto(tareas, proyectos);
    expect(resultado).toHaveLength(1);
  });

  it("los cuadros de Proyecto se ordenan por Fecha de Fin más reciente", () => {
    const tareas = [tarea({ id: "a", proyectoId: "p1" }), tarea({ id: "b", proyectoId: "p2" })];
    const proyectos = [
      proyecto({ id: "p1", fechaFin: "2026-06-01" }),
      proyecto({ id: "p2", fechaFin: "2026-12-01" }),
    ];
    const resultado = agruparTareasPorProyecto(tareas, proyectos);
    expect(resultado.map((c) => c.proyecto?.id ?? null)).toEqual([null, "p2", "p1"]);
  });
});
