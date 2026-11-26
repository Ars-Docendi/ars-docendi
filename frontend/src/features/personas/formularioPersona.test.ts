import { describe, expect, it } from "vitest";

import type { CatalogosPersonas, Persona } from "./api/personasApi";
import { aGuardar, formularioDesde } from "./formularioPersona";

const catalogos = {
  roles: [
    { id: "r-docente", codigo: "docente", nombre: "Docente", ambito: "materia", esSistema: true },
    {
      id: "r-jefe",
      codigo: "jefe_catedra",
      nombre: "Jefe de Cátedra",
      ambito: "materia",
      esSistema: true,
    },
  ],
  materias: [],
  materiasPlan: [],
  carreras: [],
  cargos: [],
  dedicaciones: [],
  sectoresExternos: [],
} as CatalogosPersonas;

/** Persona que la conciliación dejó con su designación y todavía sin mail. */
function sinCuenta(): Persona {
  return {
    personaId: "p1",
    usuarioId: null,
    nombre: "Ana",
    apellido: "Paz",
    documento: "30123456",
    legajo: "0421",
    cuil: null,
    fechaNacimiento: "1990-01-01",
    telefono: null,
    upn: null,
    tieneCuenta: false,
    activo: false,
    version: null,
    roles: [],
    membresias: [],
    asignaciones: [
      {
        id: "d1",
        materiaId: "m1",
        materiaCodigo: "101",
        materiaNombre: "Análisis",
        carreraId: "c1",
        cargoId: "k1",
        cargoNombre: "JTP",
        cargoAbreviatura: "JTP",
        dedicacion: null,
        dedicacionId: "x1",
        horas: 8,
      },
    ],
  } as Persona;
}

describe("Persona sin cuenta con designaciones sincronizadas", () => {
  it("precarga el rol Docente de su designación y la cuenta se crea con él al cargar el mail", () => {
    const form = formularioDesde(sinCuenta(), catalogos);
    form.datos.upn = "ana.paz@unlam.edu.ar";

    expect(aGuardar(form, catalogos).membresias).toEqual([
      { rolId: "r-docente", materiaId: "m1", carreraId: "c1" },
    ]);
  });

  it("si el operador quita el rol precargado, se guarda sin él", () => {
    const form = formularioDesde(sinCuenta(), catalogos);
    form.datos.upn = "ana.paz@unlam.edu.ar";
    form.membresias = [];

    expect(aGuardar(form, catalogos).membresias).toEqual([]);
  });

  it("una persona con cuenta conserva sus roles sin precargas", () => {
    const persona = { ...sinCuenta(), tieneCuenta: true, usuarioId: "u1" };

    expect(formularioDesde(persona, catalogos).membresias).toEqual([
      { rolId: "", materiaId: "", carreraId: "" },
    ]);
  });
});
