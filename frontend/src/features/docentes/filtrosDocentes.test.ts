import { describe, expect, it } from "vitest";

import type { DocenteMock } from "./models";
import {
  aplicarFiltrosDocentes,
  aplicarFiltrosYOrdenDocentes,
  siguienteOrdenDocentes,
  type FiltrosDocentes,
} from "./filtrosDocentes";

const base = (cambios: Partial<DocenteMock>): DocenteMock => ({
  id: "id",
  nombre: "Ana",
  apellido: "López",
  documento: "10",
  legajo: "10",
  cuil: "",
  fecha_nacimiento: "",
  telefono: "",
  upn: "ana@unlam.edu.ar",
  is_active: true,
  roles: ["Docente"],
  membresias: [],
  asignaciones: [],
  tieneCuenta: true,
  ...cambios,
});

const vacios: FiltrosDocentes = {
  apellidoNombre: "",
  documento: "",
  legajo: "",
  rol: [],
  ambitos: [],
  asignaciones: "",
  cuenta: [],
  estado: [],
};

describe("filtrosDocentes", () => {
  it("busca materia, cargo y abreviatura y combina filtros por pertenencia", () => {
    const docentes = [
      base({
        id: "1",
        roles: ["Jefe de Cátedra"],
        membresias: [
          {
            id: "m",
            rolId: "r",
            codigo: "r",
            nombre: "",
            ambito: "carrera",
            materiaId: null,
            carreraId: "c",
          },
        ],
        asignaciones: [
          {
            materia: { id: "m", codigo: "03500", nombre: "Software" },
            cargo: "Profesor Adjunto",
            cargoAbreviatura: "JTP",
            horas: 1,
          },
        ],
      }),
      base({ id: "2", apellido: "Gómez", roles: ["Docente"], tieneCuenta: false }),
    ];

    expect(
      aplicarFiltrosDocentes(docentes, {
        ...vacios,
        asignaciones: "jtp",
        rol: ["Jefe de Cátedra"],
        ambitos: ["carrera"],
      }).map((docente) => docente.id),
    ).toEqual(["1"]);
  });

  it("ordena legajos numéricos y quita el orden al tercer estado", () => {
    const docentes = [base({ id: "1", legajo: "10" }), base({ id: "2", legajo: "2" })];

    expect(
      aplicarFiltrosYOrdenDocentes(docentes, vacios, { columna: "legajo", direccion: "asc" }).map(
        (docente) => docente.id,
      ),
    ).toEqual(["2", "1"]);
    expect(siguienteOrdenDocentes({ columna: "legajo", direccion: "desc" }, "legajo")).toBeNull();
  });
});
