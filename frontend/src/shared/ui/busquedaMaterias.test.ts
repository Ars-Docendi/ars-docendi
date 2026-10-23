import { describe, expect, it } from "vitest";
import {
  FILTROS_MATERIA_VACIOS,
  buscarMaterias,
  describirSeleccion,
  totalOpciones,
  type OpcionMateriaCarrera,
} from "./busquedaMaterias";

const am1 = { id: "m1", codigo: "01023", nombre: "ANALISIS MATEMATICO I" };
const fisica = { id: "m2", codigo: "01035", nombre: "FISICA II" };
const opciones: OpcionMateriaCarrera[] = [
  { ...am1, carreraId: "c1", carreraCodigo: "201", carreraNombre: "Ingeniería en Informática" },
  { ...am1, carreraId: "c2", carreraCodigo: "207", carreraNombre: "Ingeniería Civil" },
  { ...fisica, carreraId: "c1", carreraCodigo: "201", carreraNombre: "Ingeniería en Informática" },
  // Mismo par repetido por dos planes de la misma carrera.
  { ...fisica, carreraId: "c1", carreraCodigo: "201", carreraNombre: "Ingeniería en Informática" },
];

describe("buscarMaterias", () => {
  it("lista un resultado por par materia–carrera sin repetir planes", () => {
    const resultados = buscarMaterias(opciones, FILTROS_MATERIA_VACIOS, "par");
    expect(resultados.map((r) => r.clave)).toEqual(["m1|c2", "m1|c1", "m2|c1"]);
  });

  it("combina los filtros y no distingue mayúsculas ni tildes", () => {
    const resultados = buscarMaterias(
      opciones,
      { ...FILTROS_MATERIA_VACIOS, materiaNombre: "análisis", carreraNombre: "INFORMATICA" },
      "par",
    );
    expect(resultados).toHaveLength(1);
    expect(resultados[0].carrera?.codigo).toBe("201");
  });

  it("filtra por código de carrera", () => {
    const resultados = buscarMaterias(
      opciones,
      { ...FILTROS_MATERIA_VACIOS, carreraCodigo: "207" },
      "par",
    );
    expect(resultados.map((r) => r.materiaCodigo)).toEqual(["01023"]);
  });

  it("en modo materia agrupa las carreras y conserva la materia si alguna coincide", () => {
    const resultados = buscarMaterias(
      opciones,
      { ...FILTROS_MATERIA_VACIOS, carreraCodigo: "207" },
      "materia",
    );
    expect(resultados).toHaveLength(1);
    expect(resultados[0].clave).toBe("m1");
    expect(resultados[0].carrera).toBeUndefined();
    expect(resultados[0].carreras.map((c) => c.codigo)).toEqual(["201", "207"]);
  });

  it("en modo materia ofrece materias canónicas sin carrera mientras no se filtre por carrera", () => {
    const conCanonica = [...opciones, { id: "m3", codigo: "09999", nombre: "SEMINARIO" }];
    expect(totalOpciones(conCanonica, "materia")).toBe(3);
    expect(totalOpciones(conCanonica, "par")).toBe(3);
    const filtrando = buscarMaterias(
      conCanonica,
      { ...FILTROS_MATERIA_VACIOS, carreraNombre: "civil" },
      "materia",
    );
    expect(filtrando.map((r) => r.materiaId)).toEqual(["m1"]);
  });
});

describe("describirSeleccion", () => {
  it("muestra materia y carrera con sus códigos", () => {
    expect(describirSeleccion(opciones, { materiaId: "m1", carreraId: "c2" }, "par")).toBe(
      "ANALISIS MATEMATICO I (01023) — Ingeniería Civil (207)",
    );
    expect(describirSeleccion(opciones, { materiaId: "m1" }, "materia")).toBe(
      "ANALISIS MATEMATICO I (01023) — todas sus carreras",
    );
    expect(describirSeleccion(opciones, null, "par")).toBe("");
  });
});
