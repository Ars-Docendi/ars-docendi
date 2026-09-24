import { describe, it, expect } from "vitest";
import { aplicarAlcance, esPropia } from "./alcanceTareas";
import type { Tarea } from "../types";

function tarea(id: string, creadoPorId: string, responsableId: string): Tarea {
  return {
    id,
    numero: 1,
    titulo: id,
    descripcion: "",
    fechaInicio: "2026-01-01",
    fechaFin: "2026-01-10",
    prioridad: "media",
    tipo: "administrativa",
    estado: "pendiente",
    porcentajeAvance: 0,
    responsable: { id: responsableId, nombre: "R", rol: "Docente" },
    creadoPor: { id: creadoPorId, nombre: "C", rol: "Decanato" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
  };
}

describe("alcance del listado", () => {
  const tareas = [
    tarea("creada", "yo", "otro"),
    tarea("asignada", "otro", "yo"),
    tarea("ajena", "otro", "otro"),
  ];

  it("una tarea es propia si la creaste o la tenés asignada", () => {
    expect(esPropia(tareas[0], "yo")).toBe(true);
    expect(esPropia(tareas[1], "yo")).toBe(true);
    expect(esPropia(tareas[2], "yo")).toBe(false);
  });

  it("Propias deja solo las creadas o asignadas al usuario", () => {
    expect(aplicarAlcance(tareas, "propias", "yo").map((t) => t.id)).toEqual([
      "creada",
      "asignada",
    ]);
  });

  it("Todas no acota", () => {
    expect(aplicarAlcance(tareas, "todas", "yo")).toHaveLength(3);
  });
});
