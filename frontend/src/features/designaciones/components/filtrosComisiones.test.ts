import { describe, expect, it } from "vitest";
import type { Comision } from "../api/comisionesApi";
import {
  aplicarFiltrosComisiones,
  aulasDeComision,
  FILTROS_COMISIONES_INICIALES,
  formatearBanda,
} from "./filtrosComisiones";

function comision(parcial: Partial<Comision> & { id: string }): Comision {
  return {
    nombre: "01-1234",
    materia: { codigo: "03500", nombre: "Ingeniería de Software", id: null },
    carreras: [{ codigo: "INF", nombre: "Ingeniería en Informática", id: null }],
    docentes: [],
    horarios: [],
    modalidadCodigo: "A",
    modalidad: "Presencial",
    ubicacion: "San Justo",
    ...parcial,
  };
}

const comisiones: Comision[] = [
  comision({
    id: "1",
    docentes: [{ nombre: "PÉREZ JUAN", responsabilidad: "Titular", personaId: null }],
  }),
  comision({
    id: "2",
    materia: { codigo: "03620", nombre: "Algoritmos", id: null },
    carreras: [{ codigo: "IND", nombre: "Ingeniería Industrial", id: null }],
    docentes: [{ nombre: "GÓMEZ ANA", responsabilidad: null, personaId: null }],
    modalidadCodigo: "E",
    modalidad: "Virtual - Sincrónico",
  }),
];

describe("filtros de comisiones", () => {
  it("sin filtros devuelve todas", () => {
    expect(aplicarFiltrosComisiones(comisiones, FILTROS_COMISIONES_INICIALES)).toHaveLength(2);
  });

  it("filtra por docente sin distinguir mayúsculas ni tildes", () => {
    const visibles = aplicarFiltrosComisiones(comisiones, {
      ...FILTROS_COMISIONES_INICIALES,
      docente: "perez",
    });
    expect(visibles.map((c) => c.id)).toEqual(["1"]);
  });

  it("filtra materia y carrera por nombre o código", () => {
    expect(
      aplicarFiltrosComisiones(comisiones, { ...FILTROS_COMISIONES_INICIALES, materia: "03620" }),
    ).toHaveLength(1);
    expect(
      aplicarFiltrosComisiones(comisiones, {
        ...FILTROS_COMISIONES_INICIALES,
        carrera: "industrial",
      }),
    ).toHaveLength(1);
  });

  it("filtra modalidad por descripción o código", () => {
    expect(
      aplicarFiltrosComisiones(comisiones, {
        ...FILTROS_COMISIONES_INICIALES,
        modalidad: "virtual",
      }),
    ).toHaveLength(1);
    expect(
      aplicarFiltrosComisiones(comisiones, { ...FILTROS_COMISIONES_INICIALES, modalidad: "a" })[0]
        .id,
    ).toBe("1");
  });
});

describe("formato de bandas y aulas", () => {
  it("muestra día, rango y aula, y omite el aula si no hay", () => {
    expect(
      formatearBanda({
        asignacionId: "1",
        dia: 3,
        desde: "08:00",
        hasta: "12:00",
        aula: "Aula 101",
      }),
    ).toBe("Miércoles 08:00–12:00 · Aula 101");
    expect(
      formatearBanda({ asignacionId: "2", dia: 6, desde: "09:00", hasta: "10:00", aula: null }),
    ).toBe("Sábado 09:00–10:00");
  });

  it("lista las aulas distintas de las bandas", () => {
    const c = comision({
      id: "3",
      horarios: [
        { asignacionId: "1", dia: 1, desde: "08:00", hasta: "10:00", aula: "Aula 1" },
        { asignacionId: "2", dia: 4, desde: "08:00", hasta: "10:00", aula: "Aula 1" },
        { asignacionId: "3", dia: 5, desde: "08:00", hasta: "10:00", aula: null },
        { asignacionId: "4", dia: 5, desde: "08:00", hasta: "10:00", aula: "Lab 2" },
      ],
    });
    expect(aulasDeComision(c)).toEqual(["Aula 1", "Lab 2"]);
  });
});
