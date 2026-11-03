import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { Tarea } from "../types";
import { TablaTareas } from "./TablaTareas";

function tarea(id: string, titulo: string, estado: Tarea["estado"], fin: string): Tarea {
  return {
    version: 1,
    id,
    numero: 1,
    titulo,
    descripcion: "",
    fechaInicio: "2020-01-01",
    fechaFin: fin,
    prioridad: "media",
    tipo: "academica",
    estado,
    porcentajeAvance: 0,
    responsable: { id: "u1", nombre: "Ana", rol: "Docente" },
    creadoPor: { id: "u2", nombre: "Beto", rol: "Decanato" },
    comentarios: [],
    historial: [],
    tareasRelacionadasIds: [],
  };
}

beforeEach(() => {
  vi.stubGlobal("matchMedia", (consulta: string) => ({
    matches: consulta.includes("559px"),
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
});
afterEach(() => vi.unstubAllGlobals());

describe("Tareas en teléfono", () => {
  it("tiñe la tarjeta con el mismo semáforo de la fila y no tiñe las terminadas", () => {
    render(
      <MemoryRouter>
        <TablaTareas
          tareas={[
            tarea("t1", "Vencida", "pendiente", "2020-02-01"),
            tarea("t2", "Cerrada", "resuelta", "2020-02-01"),
          ]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("link", { name: /Vencida/ }).closest("li")).toHaveClass("vencida");
    expect(screen.getByRole("link", { name: /Cerrada/ }).closest("li")).not.toHaveClass("vencida");
  });
});
