import { describe, it, expect } from "vitest";
import {
  puedeAsignarComoResponsableProyecto,
  puedeCambiarEstadoProyecto,
  puedeCrearProyecto,
} from "./maquinaEstadosProyecto";
import type { ActorTarea } from "../types";

const SECRETARIA: ActorTarea = { nombre: "L. Fernández", rol: "Secretaría Académica" };
const DECANATO: ActorTarea = { nombre: "R. Sosa", rol: "Decanato" };
const ADMINISTRACION: ActorTarea = { nombre: "P. Gómez", rol: "Administrativo" };
const DOCENTE: ActorTarea = { nombre: "C. López", rol: "Docente" };

describe("puedeCrearProyecto", () => {
  it("permite a Decanato y Secretaría Académica", () => {
    expect(puedeCrearProyecto(DECANATO)).toBe(true);
    expect(puedeCrearProyecto(SECRETARIA)).toBe(true);
  });

  it("rechaza Administrativo, aunque sí pueda crear tareas", () => {
    expect(puedeCrearProyecto(ADMINISTRACION)).toBe(false);
  });

  it("rechaza Docente", () => {
    expect(puedeCrearProyecto(DOCENTE)).toBe(false);
  });
});

describe("puedeCambiarEstadoProyecto", () => {
  it("permite a Decanato y Secretaría Académica, por rol y no por ownership", () => {
    expect(puedeCambiarEstadoProyecto(DECANATO)).toBe(true);
    expect(puedeCambiarEstadoProyecto(SECRETARIA)).toBe(true);
  });

  it("rechaza Administrativo", () => {
    expect(puedeCambiarEstadoProyecto(ADMINISTRACION)).toBe(false);
  });
});

describe("puedeAsignarComoResponsableProyecto", () => {
  it("Decanato puede asignarse el proyecto a sí mismo o a Secretaría Académica", () => {
    expect(puedeAsignarComoResponsableProyecto(DECANATO, DECANATO)).toBe(true);
    expect(puedeAsignarComoResponsableProyecto(DECANATO, SECRETARIA)).toBe(true);
  });

  it("Secretaría Académica solo puede asignárselo a sí misma, no a Decanato", () => {
    expect(puedeAsignarComoResponsableProyecto(SECRETARIA, SECRETARIA)).toBe(true);
    expect(puedeAsignarComoResponsableProyecto(SECRETARIA, DECANATO)).toBe(false);
  });

  it("rechaza cualquier candidato fuera de {Decanato, Secretaría Académica}, aunque la jerarquía lo permitiría", () => {
    expect(puedeAsignarComoResponsableProyecto(DECANATO, ADMINISTRACION)).toBe(false);
    expect(puedeAsignarComoResponsableProyecto(DECANATO, DOCENTE)).toBe(false);
  });
});
