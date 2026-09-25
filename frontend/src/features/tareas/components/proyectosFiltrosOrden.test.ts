import { describe, it, expect } from "vitest";
import { aplicarFiltrosProyectos, FILTROS_PROYECTOS_INICIALES } from "./filtrosProyectos";
import { ordenarProyectos } from "./ordenProyectos";
import type { Proyecto } from "../types";

function proyecto(id: string, numero: number, estado: string, fechaInicio: string): Proyecto {
  return {
    id,
    numero,
    nombre: `Proyecto ${id}`,
    descripcion: "",
    fechaInicio,
    fechaFin: "2026-12-31",
    estado,
    estadoNombre: estado,
    admiteTareas: estado === "abierto",
    responsable: { id: "r", nombre: "Lucía Fernández", rol: "Secretaría Académica" },
  };
}

const CATALOGO = ["abierto", "finalizado", "cancelado"];
const proyectos = [
  proyecto("cancelado-temprano", 1, "cancelado", "2026-01-01"),
  proyecto("abierto-tarde", 2, "abierto", "2026-06-01"),
  proyecto("finalizado", 3, "finalizado", "2026-03-01"),
  proyecto("abierto-temprano", 4, "abierto", "2026-02-01"),
];

describe("proyectos: orden y filtros", () => {
  it("por defecto ordena por estado (orden del catálogo) y luego por fecha de inicio", () => {
    expect(ordenarProyectos(proyectos, null, CATALOGO).map((p) => p.id)).toEqual([
      "abierto-temprano",
      "abierto-tarde",
      "finalizado",
      "cancelado-temprano",
    ]);
  });

  it("con una columna elegida, los empates siguen el orden por defecto", () => {
    const porFin = ordenarProyectos(
      proyectos,
      { columna: "fechaFin", direccion: "desc" },
      CATALOGO,
    );
    expect(porFin.map((p) => p.id)).toEqual([
      "abierto-temprano",
      "abierto-tarde",
      "finalizado",
      "cancelado-temprano",
    ]);
  });

  it("filtra por estado y por texto del nombre", () => {
    const abiertos = aplicarFiltrosProyectos(proyectos, {
      ...FILTROS_PROYECTOS_INICIALES,
      estado: ["abierto"],
    });
    expect(abiertos).toHaveLength(2);

    const porNombre = aplicarFiltrosProyectos(proyectos, {
      ...FILTROS_PROYECTOS_INICIALES,
      nombre: "FINALIZADO",
    });
    expect(porNombre.map((p) => p.id)).toEqual(["finalizado"]);
  });
});
