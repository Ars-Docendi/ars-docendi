import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it } from "vitest";

import type { Tarea } from "../types";
import { TablaTareas } from "./TablaTareas";

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
    responsable: { id: "u1", nombre: "Ana", rol: "Docente" },
    creadoPor: { id: "u2", nombre: "Beto", rol: "Decanato" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
  };
}

const TAREAS = [tarea("t1", 1, "Alfa"), tarea("t2", 2, "Beta")];

function montar(inicial: string) {
  const router = createMemoryRouter(
    [
      { path: "/tareas", element: <TablaTareas tareas={TAREAS} prefijoURL="t" /> },
      { path: "/tareas/:id", element: <p>Detalle</p> },
    ],
    { initialEntries: [inicial] },
  );
  render(<RouterProvider router={router} />);
  return router;
}

describe("TablaTareas", () => {
  it("restaura el filtro de columna desde la URL", () => {
    montar("/tareas?t.titulo=beta");
    expect(screen.getByRole("link", { name: /Beta/ })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Alfa/ })).not.toBeInTheDocument();
  });

  it("abre el detalle desde el enlace de la celda principal recordando el origen con sus filtros", async () => {
    const router = montar("/tareas?t.titulo=beta");

    await userEvent.click(screen.getByRole("link", { name: /Beta/ }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/tareas/t2"));
    expect(router.state.location.state).toEqual({ volverA: "/tareas?t.titulo=beta" });
  });

  it("la fila se abre con el teclado", async () => {
    const router = montar("/tareas");
    const fila = screen.getByRole("link", { name: /Alfa/ }).closest("tr")!;
    fila.focus();

    await userEvent.keyboard("{Enter}");

    await waitFor(() => expect(router.state.location.pathname).toBe("/tareas/t1"));
  });
});
