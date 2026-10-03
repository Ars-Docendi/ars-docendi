import { describe, it, expect } from "vitest";
import { puedeCambiarEstado, puedeEditarAvance, puedeEditarCampos } from "./maquinaEstadosTarea";
import type { ActorTarea, Tarea } from "../types";

const SECRETARIA: ActorTarea = { nombre: "Lucía Fernández", rol: "Secretaría Académica" };
const RESPONSABLE: ActorTarea = { nombre: "Gustavo Ruiz", rol: "Jefe de Cátedra" };
const OTRO: ActorTarea = { nombre: "Marina Díaz", rol: "Coordinador de Carrera" };
const ADMINISTRADOR: ActorTarea = { nombre: "Ernesto Vidal", rol: "Administrador de Sistemas" };

function tarea(overrides: Partial<Tarea> = {}): Tarea {
  return {
    id: "t1",
    numero: 1,
    titulo: "Revisar aulas del turno noche",
    descripcion: "Chequear disponibilidad para el próximo cuatrimestre.",
    fechaInicio: "2026-01-01",
    fechaFin: "2026-01-10",
    prioridad: "media",
    tipo: "administrativa",
    estado: "pendiente",
    porcentajeAvance: 0,
    responsable: { id: "u2", ...RESPONSABLE },
    creadoPor: { id: "u4", ...SECRETARIA },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
    ...overrides,
  };
}

describe("puedeEditarCampos", () => {
  it("solo la autoridad creadora", () => {
    expect(puedeEditarCampos(tarea(), SECRETARIA)).toBe(true);
    expect(puedeEditarCampos(tarea(), RESPONSABLE)).toBe(false);
    expect(puedeEditarCampos(tarea(), OTRO)).toBe(false);
  });
});

describe("Administrador de Sistemas", () => {
  it("puede todo aunque no sea autor ni Responsable", () => {
    expect(puedeEditarCampos(tarea(), ADMINISTRADOR)).toBe(true);
    expect(puedeEditarAvance(tarea(), ADMINISTRADOR)).toBe(true);
    expect(puedeCambiarEstado(tarea(), ADMINISTRADOR, "cancelada")).toBe(true);
    expect(puedeCambiarEstado(tarea({ estado: "resuelta" }), ADMINISTRADOR, "en_curso")).toBe(true);
  });
});

describe("puedeEditarAvance", () => {
  it("el Responsable o la autoridad creadora", () => {
    expect(puedeEditarAvance(tarea(), RESPONSABLE)).toBe(true);
    expect(puedeEditarAvance(tarea(), SECRETARIA)).toBe(true);
    expect(puedeEditarAvance(tarea(), OTRO)).toBe(false);
  });
});

describe("puedeCambiarEstado", () => {
  it("cancelar es exclusivo de la autoridad creadora", () => {
    expect(puedeCambiarEstado(tarea(), SECRETARIA, "cancelada")).toBe(true);
    expect(puedeCambiarEstado(tarea(), RESPONSABLE, "cancelada")).toBe(false);
  });

  it("no se puede cancelar una tarea ya cerrada", () => {
    expect(puedeCambiarEstado(tarea({ estado: "resuelta" }), SECRETARIA, "cancelada")).toBe(false);
  });

  it("el Responsable mueve libremente entre los estados no terminales", () => {
    expect(puedeCambiarEstado(tarea(), RESPONSABLE, "en_curso")).toBe(true);
    expect(puedeCambiarEstado(tarea(), OTRO, "en_curso")).toBe(false);
  });

  it("un estado terminal solo lo reabre la autoridad creadora", () => {
    const resuelta = tarea({ estado: "resuelta" });
    expect(puedeCambiarEstado(resuelta, RESPONSABLE, "en_curso")).toBe(false);
    expect(puedeCambiarEstado(resuelta, SECRETARIA, "en_curso")).toBe(true);
  });
});
