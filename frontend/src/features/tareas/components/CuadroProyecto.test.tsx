import { render, screen, within } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it } from "vitest";

import type { Proyecto, Tarea } from "../types";
import { CuadroProyecto } from "./CuadroProyecto";
import { prefijoDeCuadro } from "./agrupacionProyectos";

function tarea(id: string, numero: number, titulo: string): Tarea {
  return {
    version: 1,
    id,
    numero,
    titulo,
    descripcion: "",
    fechaInicio: "2026-03-01",
    fechaFin: "2026-04-01",
    prioridad: "media",
    tipo: "academica",
    estado: "pendiente",
    porcentajeAvance: 0,

    avanceCalculado: false,
    responsable: { id: "u1", nombre: "Ana", rol: "Docente" },
    creadoPor: { id: "u2", nombre: "Beto", rol: "Decanato" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
  };
}

const PROYECTO = {
  id: "abcd1234-0000-4000-8000-000000000001",
  nombre: "Proyecto Uno",
} as Proyecto;

function montar(inicial: string) {
  const router = createMemoryRouter(
    [
      {
        path: "/tareas",
        element: (
          <>
            <CuadroProyecto
              proyecto={null}
              tareas={[tarea("t1", 1, "Alfa"), tarea("t2", 2, "Beta")]}
              prefijoURL={prefijoDeCuadro(null)}
            />
            <CuadroProyecto
              proyecto={PROYECTO}
              tareas={[tarea("t3", 3, "Gamma"), tarea("t4", 4, "Delta")]}
              prefijoURL={prefijoDeCuadro(PROYECTO)}
            />
          </>
        ),
      },
    ],
    { initialEntries: [inicial] },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("CuadroProyecto — filtros de columna en la URL", () => {
  it("cada cuadro tiene su prefijo y no pisa al otro", () => {
    expect(prefijoDeCuadro(null)).toBe("g");
    expect(prefijoDeCuadro(PROYECTO)).toBe("pabcd1234000040008000000000000001");
  });

  it("restaura el filtro de cada cuadro desde su propio prefijo", () => {
    montar("/tareas?g.titulo=beta&pabcd1234000040008000000000000001.titulo=delta");
    const generales = within(screen.getByRole("region", { name: "Generales" }));
    const proyecto = within(screen.getByRole("region", { name: "Proyecto Uno" }));
    expect(generales.getByRole("link", { name: /Beta/ })).toBeInTheDocument();
    expect(generales.queryByRole("link", { name: /Alfa/ })).not.toBeInTheDocument();
    expect(proyecto.getByRole("link", { name: /Delta/ })).toBeInTheDocument();
    expect(proyecto.queryByRole("link", { name: /Gamma/ })).not.toBeInTheDocument();
  });
});
